using System.Net;
using System.Text.Json;
using Google.Api.Gax.Grpc;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Gmail.v1;
using Google.Apis.Gmail.v1.Data;
using Google.Apis.Services;
using Google.Cloud.Iam.V1;
using Google.Cloud.PubSub.V1;
using Google.Protobuf;
using Grpc.Core;
using Infrastructure.Broker.Kafka.Contracts.Interfaces;
using SmtpConnector.Api.Factory;
using SmtpConnector.Api.Options;
using SmtpConnector.Dal.History.Interfaces;

namespace SmtpConnector.Api;

internal sealed class GmailOptionsValidator
{
    public TopicName ValidateAndGetTopicName(GmailOptions gmailOptions, PubSubOptions pubSubOptions)
    {
        if (string.IsNullOrWhiteSpace(gmailOptions.ClientId))
        {
            throw new InvalidOperationException("Не задан GmailOptions.ClientId");
        }

        if (string.IsNullOrWhiteSpace(gmailOptions.ClientSecret))
        {
            throw new InvalidOperationException("Не задан GmailOptions.ClientSecret");
        }

        if (string.IsNullOrWhiteSpace(gmailOptions.RefreshToken))
        {
            throw new InvalidOperationException("Не задан GmailOptions.RefreshToken");
        }

        if (string.IsNullOrWhiteSpace(gmailOptions.TopicName))
        {
            throw new InvalidOperationException("Не задан GmailOptions.TopicName");
        }

        if (string.IsNullOrWhiteSpace(pubSubOptions.ProjectId))
        {
            throw new InvalidOperationException("Не задан PubSubOptions.ProjectId");
        }

        if (string.IsNullOrWhiteSpace(pubSubOptions.SubscriptionId))
        {
            throw new InvalidOperationException("Не задан PubSubOptions.SubscriptionId");
        }

        if (string.IsNullOrWhiteSpace(pubSubOptions.ServiceAccountJsonPath))
        {
            throw new InvalidOperationException("Не задан PubSubOptions.ServiceAccountJsonPath");
        }

        var topicName = ParseTopicName(gmailOptions.TopicName);

        if (string.Equals(topicName.ProjectId, pubSubOptions.ProjectId, StringComparison.Ordinal) is false)
        {
            throw new InvalidOperationException("GmailOptions.TopicName должен использовать тот же ProjectId, что и PubSubOptions.ProjectId");
        }

        return topicName;
    }

    private static TopicName ParseTopicName(string value)
    {
        try
        {
            return TopicName.Parse(value);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "Некорректный GmailOptions.TopicName. Ожидается формат projects/<projectId>/topics/<topicId>",
                ex);
        }
    }
}

internal sealed class GmailServiceFactory
{
    private readonly GmailOptions _gmailOptions;

    public GmailServiceFactory(GmailOptions gmailOptions)
    {
        _gmailOptions = gmailOptions;
    }

    public GmailService CreateReadonly()
    {
        var secrets = new ClientSecrets
        {
            ClientId = _gmailOptions.ClientId,
            ClientSecret = _gmailOptions.ClientSecret,
        };

        var flow = new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
        {
            ClientSecrets = secrets,
            Scopes = new[]
            {
                GmailService.Scope.GmailReadonly
            }
        });

        var token = new Google.Apis.Auth.OAuth2.Responses.TokenResponse
        {
            RefreshToken = _gmailOptions.RefreshToken,
        };

        var credential = new UserCredential(flow, "user", token);

        return new GmailService(new BaseClientService.Initializer
        {
            ApplicationName = _gmailOptions.ApplicationName,
            HttpClientInitializer = credential,
        });
    }
}

internal sealed class HistoryStateFacade
{
    private readonly IServiceScopeFactory _scopeFactory;

    public HistoryStateFacade(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task<string?> GetLastHistoryIdAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IHistoryStateStore>();
        return await store.GetLastHistoryIdAsync(cancellationToken);
    }

    public async Task SaveLastHistoryIdAsync(string historyId, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IHistoryStateStore>();
        await store.SaveLastHistoryIdAsync(historyId, cancellationToken);
    }
}

internal sealed class PubSubInfrastructureEnsurer
{
    private const string GmailPublisherMember = "serviceAccount:gmail-api-push@system.gserviceaccount.com";
    private const string PubSubPublisherRole = "roles/pubsub.publisher";

    private readonly PubSubOptions _pubSubOptions;
    private readonly ILogger _logger;

    public PubSubInfrastructureEnsurer(PubSubOptions pubSubOptions, ILogger logger)
    {
        _pubSubOptions = pubSubOptions;
        _logger = logger;
    }

    public async Task EnsureAsync(TopicName topicName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_pubSubOptions.ServiceAccountJsonPath))
        {
            throw new InvalidOperationException("Не задан PubSubOptions.ServiceAccountJsonPath");
        }

        if (File.Exists(_pubSubOptions.ServiceAccountJsonPath) is false)
        {
            throw new InvalidOperationException($"Файл сервисного аккаунта не найден: {_pubSubOptions.ServiceAccountJsonPath}");
        }

        var json = await File.ReadAllTextAsync(_pubSubOptions.ServiceAccountJsonPath, cancellationToken);

        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidOperationException($"Файл сервисного аккаунта пустой: {_pubSubOptions.ServiceAccountJsonPath}");
        }

        try
        {
            using var doc = JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty("type", out var typeProp) is false)
            {
                throw new InvalidOperationException("В файле сервисного аккаунта отсутствует поле type");
            }

            var type = typeProp.GetString();

            if (string.Equals(type, "service_account", StringComparison.Ordinal) is false)
            {
                throw new InvalidOperationException("Файл не является ключом сервисного аккаунта. Поле type должно быть service_account");
            }
        }
        catch (JsonException)
        {
            throw new InvalidOperationException("Файл сервисного аккаунта содержит некорректный JSON");
        }

        var subscriptionName = SubscriptionName.FromProjectSubscription(_pubSubOptions.ProjectId, _pubSubOptions.SubscriptionId);

        GoogleCredential credential;

        try
        {
            credential = GoogleCredential.FromJson(json);
        }
        catch
        {
            throw new InvalidOperationException("Не удалось загрузить учетные данные из файла сервисного аккаунта");
        }

        var publisherApi = await new PublisherServiceApiClientBuilder
        {
            Credential = credential
        }.BuildAsync(cancellationToken);

        var subscriberApi = await new SubscriberServiceApiClientBuilder
        {
            Credential = credential
        }.BuildAsync(cancellationToken);

        await EnsureTopicAsync(publisherApi, topicName, cancellationToken);
        await EnsureTopicIamAsync(publisherApi, topicName, cancellationToken);
        await EnsureSubscriptionAsync(subscriberApi, subscriptionName, topicName, cancellationToken);
    }

    private async Task EnsureTopicAsync(PublisherServiceApiClient publisherApi, TopicName topicName, CancellationToken cancellationToken)
    {
        try
        {
            _ = await publisherApi.GetTopicAsync(topicName, cancellationToken: cancellationToken);
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
        {
            _ = await publisherApi.CreateTopicAsync(topicName, cancellationToken: cancellationToken);
            _logger.LogInformation("Pub/Sub topic создан: {Topic}", topicName.ToString());
        }
    }

    private async Task EnsureSubscriptionAsync(
        SubscriberServiceApiClient subscriberApi,
        SubscriptionName subscriptionName,
        TopicName topicName,
        CancellationToken cancellationToken)
    {
        try
        {
            _ = await subscriberApi.GetSubscriptionAsync(subscriptionName, cancellationToken: cancellationToken);
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
        {
            var subscription = new Subscription
            {
                SubscriptionName = subscriptionName,
                TopicAsTopicName = topicName
            };

            _ = await subscriberApi.CreateSubscriptionAsync(subscription, cancellationToken: cancellationToken);

            _logger.LogInformation(
                "Pub/Sub subscription создана: {Subscription} -> {Topic}",
                subscriptionName.ToString(),
                topicName.ToString());
        }
    }

    private async Task EnsureTopicIamAsync(PublisherServiceApiClient publisherApi, TopicName topicName, CancellationToken cancellationToken)
    {
        Policy policy;

        try
        {
            var getRequest = new GetIamPolicyRequest
            {
                ResourceAsResourceName = topicName
            };

            policy = await publisherApi.IAMPolicyClient.GetIamPolicyAsync(
                getRequest,
                CallSettings.FromCancellationToken(cancellationToken));
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
        {
            throw new InvalidOperationException("Pub/Sub topic не найден при чтении IAM политики", ex);
        }

        if (HasPublisherBinding(policy))
        {
            return;
        }

        AddPublisherBinding(policy);

        var setRequest = new SetIamPolicyRequest
        {
            ResourceAsResourceName = topicName,
            Policy = policy
        };

        _ = await publisherApi.IAMPolicyClient.SetIamPolicyAsync(
            setRequest,
            CallSettings.FromCancellationToken(cancellationToken));

        _logger.LogInformation("Выданы права publish для Gmail на topic: {Topic}", topicName.ToString());
    }

    private static bool HasPublisherBinding(Policy policy)
    {
        foreach (var binding in policy.Bindings)
        {
            if (string.Equals(binding.Role, PubSubPublisherRole, StringComparison.Ordinal) is false)
            {
                continue;
            }

            foreach (var member in binding.Members)
            {
                if (string.Equals(member, GmailPublisherMember, StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static void AddPublisherBinding(Policy policy)
    {
        Binding? publisherBinding = null;

        foreach (var binding in policy.Bindings)
        {
            if (string.Equals(binding.Role, PubSubPublisherRole, StringComparison.Ordinal))
            {
                publisherBinding = binding;
                break;
            }
        }

        if (publisherBinding is null)
        {
            publisherBinding = new Binding
            {
                Role = PubSubPublisherRole
            };

            policy.Bindings.Add(publisherBinding);
        }

        foreach (var member in publisherBinding.Members)
        {
            if (string.Equals(member, GmailPublisherMember, StringComparison.Ordinal))
            {
                return;
            }
        }

        publisherBinding.Members.Add(GmailPublisherMember);
    }
}

internal sealed class GmailWatchManager
{
    private readonly GmailOptions _gmailOptions;
    private readonly ILogger _logger;
    private readonly GmailServiceFactory _gmailServiceFactory;
    private readonly HistoryStateFacade _historyState;
    private readonly PubSubInfrastructureEnsurer _pubSubEnsurer;
    private readonly TimeSpan _renewInterval;

    public GmailWatchManager(
        GmailOptions gmailOptions,
        ILogger logger,
        GmailServiceFactory gmailServiceFactory,
        HistoryStateFacade historyState,
        PubSubInfrastructureEnsurer pubSubEnsurer,
        TimeSpan renewInterval)
    {
        _gmailOptions = gmailOptions;
        _logger = logger;
        _gmailServiceFactory = gmailServiceFactory;
        _historyState = historyState;
        _pubSubEnsurer = pubSubEnsurer;
        _renewInterval = renewInterval;
    }

    public async Task WatchRenewLoopAsync(CancellationToken cancellationToken)
    {
        while (cancellationToken.IsCancellationRequested is false)
        {
            try
            {
                await Task.Delay(_renewInterval, cancellationToken);
                await EnsureWatchAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка обновления Gmail watch");
            }
        }
    }

    public async Task EnsureWatchAsync(CancellationToken cancellationToken)
    {
        try
        {
            await EnsureWatchCoreAsync(cancellationToken);
        }
        catch (Google.GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogWarning(ex, "Не удалось настроить Gmail watch. Попробую пересоздать ресурсы Pub/Sub и повторить");

            // На этом уровне TopicName уже валидирован и связан с ProjectId.
            // Повторно обеспечиваем инфраструктуру, потом повторяем watch.
            var topicName = TopicName.Parse(_gmailOptions.TopicName);
            await _pubSubEnsurer.EnsureAsync(topicName, cancellationToken);

            await EnsureWatchCoreAsync(cancellationToken);
        }
    }

    private async Task EnsureWatchCoreAsync(CancellationToken cancellationToken)
    {
        var gmail = _gmailServiceFactory.CreateReadonly();

        var request = new WatchRequest
        {
            TopicName = _gmailOptions.TopicName,
            LabelIds = _gmailOptions.LabelIds is { Length: > 0 } ? _gmailOptions.LabelIds : null,
            LabelFilterBehavior = "INCLUDE",
        };

        var watchResponse = await gmail.Users.Watch(request, _gmailOptions.UserId).ExecuteAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(watchResponse.HistoryId.ToString()) is false)
        {
            var current = await _historyState.GetLastHistoryIdAsync(cancellationToken);

            if (string.IsNullOrWhiteSpace(current))
            {
                await _historyState.SaveLastHistoryIdAsync(watchResponse.HistoryId.ToString(), cancellationToken);
            }
        }

        _logger.LogInformation(
            "Gmail watch обновлен. HistoryId: {HistoryId}, Expiration: {Expiration}",
            watchResponse.HistoryId,
            watchResponse.Expiration);
    }
}

internal sealed class PubSubSubscriberFactory
{
    private readonly PubSubOptions _pubSubOptions;

    public PubSubSubscriberFactory(PubSubOptions pubSubOptions)
    {
        _pubSubOptions = pubSubOptions;
    }

    public async Task<SubscriberClient> CreateSubscriberAsync(CancellationToken cancellationToken)
    {
        var subscriptionName = SubscriptionName.FromProjectSubscription(_pubSubOptions.ProjectId, _pubSubOptions.SubscriptionId);
        var googleCredential = GoogleCredential.FromFile(_pubSubOptions.ServiceAccountJsonPath);

        var builder = new SubscriberClientBuilder
        {
            SubscriptionName = subscriptionName,
            Credential = googleCredential
        };

        return await builder.BuildAsync(cancellationToken);
    }
}

internal sealed class GmailInboundMessageHandler : IDisposable
{
    private readonly ILogger _logger;
    private readonly HistoryStateFacade _historyState;
    private readonly GmailWatchManager _watchManager;
    private readonly GmailHistoryProcessor _historyProcessor;
    private readonly PubSubNotificationParser _notificationParser;
    private readonly SemaphoreSlim _handleLock;

    public GmailInboundMessageHandler(
        ILogger logger,
        HistoryStateFacade historyState,
        GmailWatchManager watchManager,
        GmailHistoryProcessor historyProcessor,
        PubSubNotificationParser notificationParser)
    {
        _logger = logger;
        _historyState = historyState;
        _watchManager = watchManager;
        _historyProcessor = historyProcessor;
        _notificationParser = notificationParser;
        _handleLock = new SemaphoreSlim(1, 1);
    }

    public async Task<SubscriberClient.Reply> HandleAsync(PubsubMessage message, CancellationToken cancellationToken)
    {
        await _handleLock.WaitAsync(cancellationToken);

        try
        {
            var notification = _notificationParser.TryParse(message.Data);

            if (notification is null)
            {
                var preview = BuildBase64Preview(message.Data);

                _logger.LogWarning(
                    "Не удалось разобрать уведомление Pub/Sub. MessageId={MessageId}, PublishTime={PublishTime}, DataBase64Preview={Data}",
                    message.MessageId,
                    message.PublishTime,
                    preview);

                return SubscriberClient.Reply.Ack;
            }

            var lastHistoryId = await _historyState.GetLastHistoryIdAsync(cancellationToken);

            if (ulong.TryParse(lastHistoryId, out var last) && ulong.TryParse(notification.HistoryId, out var incoming))
            {
                if (incoming <= last)
                {
                    return SubscriberClient.Reply.Ack;
                }
            }

            if (string.IsNullOrWhiteSpace(lastHistoryId))
            {
                await _historyState.SaveLastHistoryIdAsync(notification.HistoryId, cancellationToken);
                return SubscriberClient.Reply.Ack;
            }

            try
            {
                var processed = await _historyProcessor.ProcessChangesAsync(lastHistoryId, notification.HistoryId, cancellationToken);
                await _historyState.SaveLastHistoryIdAsync(notification.HistoryId, cancellationToken);

                _logger.LogInformation(
                    "История обработана. startHistoryId={Start} newHistoryId={New} processed={Count}",
                    lastHistoryId,
                    notification.HistoryId,
                    processed);

                return SubscriberClient.Reply.Ack;
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Письмо отклонено обработчиком");
                await _historyState.SaveLastHistoryIdAsync(notification.HistoryId, cancellationToken);
                return SubscriberClient.Reply.Ack;
            }
            catch (Google.GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.NotFound)
            {
                _logger.LogWarning("HistoryId устарел или неверный. Выполняю повторный watch");
                await _watchManager.EnsureWatchAsync(cancellationToken);
                return SubscriberClient.Reply.Ack;
            }
            catch (TimeoutException ex)
            {
                _logger.LogWarning(ex, "Таймаут при ожидании RPC-ответа. Повтор не нужен");
                await _historyState.SaveLastHistoryIdAsync(notification.HistoryId, cancellationToken);
                return SubscriberClient.Reply.Ack;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка обработки уведомления. Повторю обработку");
                return SubscriberClient.Reply.Nack;
            }
        }
        finally
        {
            _handleLock.Release();
        }
    }

    public void Dispose()
    {
        _handleLock.Dispose();
    }

    private static string BuildBase64Preview(ByteString data)
    {
        if (data.IsEmpty)
        {
            return "<пусто>";
        }

        var base64 = Convert.ToBase64String(data.ToByteArray());

        const int maxLen = 220;

        if (base64.Length <= maxLen)
        {
            return base64;
        }

        return base64.Substring(0, maxLen) + "...";
    }
}

internal sealed class GmailHistoryProcessor
{
    private readonly GmailOptions _gmailOptions;
    private readonly GmailServiceFactory _gmailServiceFactory;
    private readonly IServiceScopeFactory _scopeFactory;

    public GmailHistoryProcessor(
        GmailOptions gmailOptions,
        GmailServiceFactory gmailServiceFactory,
        IServiceScopeFactory scopeFactory)
    {
        _gmailOptions = gmailOptions;
        _gmailServiceFactory = gmailServiceFactory;
        _scopeFactory = scopeFactory;
    }

    public async Task<int> ProcessChangesAsync(string startHistoryId, string newHistoryId, CancellationToken cancellationToken)
    {
        var gmail = _gmailServiceFactory.CreateReadonly();
        var handled = new HashSet<string>(StringComparer.Ordinal);
        var processedCount = 0;

        var historyRequest = gmail.Users.History.List(_gmailOptions.UserId);
        historyRequest.StartHistoryId = ParseUlong(startHistoryId);
        historyRequest.HistoryTypes = Google.Apis.Gmail.v1.UsersResource.HistoryResource.ListRequest.HistoryTypesEnum.MessageAdded;

        if (_gmailOptions.LabelIds is { Length: > 0 })
        {
            historyRequest.LabelId = _gmailOptions.LabelIds[0];
        }

        string? pageToken = null;

        do
        {
            historyRequest.PageToken = pageToken;
            var response = await historyRequest.ExecuteAsync(cancellationToken);

            if (response.History is not null)
            {
                foreach (var h in response.History)
                {
                    if (h.MessagesAdded is null)
                    {
                        continue;
                    }

                    foreach (var added in h.MessagesAdded)
                    {
                        var id = added?.Message?.Id;

                        if (string.IsNullOrWhiteSpace(id))
                        {
                            continue;
                        }

                        if (handled.Add(id) is false)
                        {
                            continue;
                        }

                        await HandleMessageIdAsync(gmail, id, cancellationToken);
                        processedCount++;
                    }
                }
            }

            pageToken = response.NextPageToken;
        }
        while (string.IsNullOrWhiteSpace(pageToken) is false);

        return processedCount;
    }

    private async Task HandleMessageIdAsync(GmailService gmail, string messageId, CancellationToken cancellationToken)
    {
        var get = gmail.Users.Messages.Get(_gmailOptions.UserId, messageId);
        get.Format = UsersResource.MessagesResource.GetRequest.FormatEnum.Raw;

        var msg = await get.ExecuteAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(msg.Raw))
        {
            return;
        }

        var rawBytes = Base64Url.Decode(msg.Raw);

        using var scope = _scopeFactory.CreateScope();
        var ingestor = scope.ServiceProvider.GetRequiredService<IEmailIngestor>();

        var request = EmailRequestFactory.Build(rawBytes);
        Console.WriteLine("dsa " + request.Content);
        if (string.IsNullOrWhiteSpace(request.Content))
        {
            var extracted = EmailContentExtractor.ExtractPlainText(rawBytes);

            request.Content = string.IsNullOrWhiteSpace(extracted)
                ? null
                : extracted.Trim();
        }

        await ingestor.IngestAsync(request, cancellationToken);
    }

    private static ulong ParseUlong(string value)
    {
        if (ulong.TryParse(value, out var result))
        {
            return result;
        }

        throw new InvalidOperationException("Некорректный historyId");
    }
}

internal sealed class PubSubNotificationParser
{
    public GmailNotification? TryParse(ByteString data)
    {
        var text = data.ToStringUtf8().Trim();

        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        if (TryParseNotificationJson(text, out var notification))
        {
            return notification;
        }

        try
        {
            var decodedBytes = Base64Url.Decode(text);
            var decodedText = System.Text.Encoding.UTF8.GetString(decodedBytes).Trim();

            if (TryParseNotificationJson(decodedText, out notification))
            {
                return notification;
            }
        }
        catch
        {
        }

        return null;
    }

    private static bool TryParseNotificationJson(string text, out GmailNotification? notification)
    {
        notification = null;

        var trimmed = text.Trim();

        if (trimmed.StartsWith("{", StringComparison.Ordinal) is false)
        {
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(trimmed);
            notification = GmailNotification.From(doc);
            return notification is not null;
        }
        catch
        {
            return false;
        }
    }

    internal sealed class GmailNotification
    {
        public string EmailAddress { get; }
        public string HistoryId { get; }

        private GmailNotification(string emailAddress, string historyId)
        {
            EmailAddress = emailAddress;
            HistoryId = historyId;
        }

        public static GmailNotification? From(JsonDocument doc)
        {
            if (doc.RootElement.TryGetProperty("emailAddress", out var emailAddressEl) is false)
            {
                return null;
            }

            if (doc.RootElement.TryGetProperty("historyId", out var historyIdEl) is false)
            {
                return null;
            }

            var emailAddress = emailAddressEl.GetString();
            string? historyId;

            if (historyIdEl.ValueKind == JsonValueKind.String)
            {
                historyId = historyIdEl.GetString();
            }
            else if (historyIdEl.ValueKind == JsonValueKind.Number)
            {
                if (historyIdEl.TryGetUInt64(out var historyUlong))
                {
                    historyId = historyUlong.ToString();
                }
                else if (historyIdEl.TryGetInt64(out var historyLong))
                {
                    historyId = historyLong.ToString();
                }
                else
                {
                    historyId = null;
                }
            }
            else
            {
                historyId = null;
            }

            if (string.IsNullOrWhiteSpace(emailAddress))
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(historyId))
            {
                return null;
            }

            return new GmailNotification(emailAddress, historyId);
        }
    }
}

internal static class Base64Url
{
    public static byte[] Decode(string input)
    {
        var cleaned = input.Trim()
            .Replace("\r", string.Empty, StringComparison.Ordinal)
            .Replace("\n", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal);

        var base64 = cleaned.Replace('-', '+').Replace('_', '/');

        var pad = base64.Length % 4;

        if (pad == 2)
        {
            base64 += "==";
        }
        else if (pad == 3)
        {
            base64 += "=";
        }
        else if (pad != 0)
        {
            throw new InvalidOperationException("Некорректная base64url строка");
        }

        return Convert.FromBase64String(base64);
    }
}