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
using Microsoft.Extensions.Options;
using SmtpConnector.Api.Factory;
using SmtpConnector.Api.Options;
using SmtpConnector.Dal.History.Interfaces;

namespace SmtpConnector.Api;

public sealed class GmailInboundHostedService : IHostedService, IDisposable
{
    private static readonly TimeSpan WatchRenewInterval = TimeSpan.FromDays(1);

    private const string GmailPublisherMember = "serviceAccount:gmail-api-push@system.gserviceaccount.com";
    private const string PubSubPublisherRole = "roles/pubsub.publisher";

    private readonly GmailOptions _gmailOptions;
    private readonly PubSubOptions _pubSubOptions;
    private readonly ILogger<GmailInboundHostedService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    private SubscriberClient? _subscriber;
    private Task? _subscriberTask;
    private CancellationTokenSource? _cts;
    private Task? _watchRenewTask;
    private readonly SemaphoreSlim _handleLock;

    public GmailInboundHostedService(
        IOptions<GmailOptions> gmailOptions,
        IOptions<PubSubOptions> pubSubOptions,
        ILogger<GmailInboundHostedService> logger,
        IServiceScopeFactory scopeFactory)
    {
        _gmailOptions = gmailOptions.Value;
        _pubSubOptions = pubSubOptions.Value;
        _logger = logger;
        _scopeFactory = scopeFactory;
        _handleLock = new SemaphoreSlim(1, 1);
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        ValidateOptions();

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        await EnsurePubSubInfrastructureAsync(_cts.Token);
        await EnsureWatchAsync(_cts.Token);

        _watchRenewTask = Task.Run(() => WatchRenewLoopAsync(_cts.Token), _cts.Token);

        _subscriber = await CreateSubscriberAsync(_cts.Token);
        _subscriberTask = _subscriber.StartAsync(HandleMessageAsync);

        _logger.LogInformation(
            "Gmail connector запущен. Pub/Sub subscription: {ProjectId}/{SubscriptionId}",
            _pubSubOptions.ProjectId,
            _pubSubOptions.SubscriptionId);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_cts is null)
        {
            return;
        }

        _cts.Cancel();

        if (_subscriber is not null)
        {
            await _subscriber.StopAsync(CancellationToken.None);
        }

        if (_subscriberTask is not null)
        {
            try
            {
                await _subscriberTask;
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка работы Pub/Sub подписчика");
            }
        }

        if (_watchRenewTask is not null)
        {
            try
            {
                await _watchRenewTask;
            }
            catch (OperationCanceledException)
            {
            }
        }
    }

    private void ValidateOptions()
    {
        if (string.IsNullOrWhiteSpace(_gmailOptions.ClientId))
        {
            throw new InvalidOperationException("Не задан GmailOptions.ClientId");
        }

        if (string.IsNullOrWhiteSpace(_gmailOptions.ClientSecret))
        {
            throw new InvalidOperationException("Не задан GmailOptions.ClientSecret");
        }

        if (string.IsNullOrWhiteSpace(_gmailOptions.RefreshToken))
        {
            throw new InvalidOperationException("Не задан GmailOptions.RefreshToken");
        }

        if (string.IsNullOrWhiteSpace(_gmailOptions.TopicName))
        {
            throw new InvalidOperationException("Не задан GmailOptions.TopicName");
        }

        if (string.IsNullOrWhiteSpace(_pubSubOptions.ProjectId))
        {
            throw new InvalidOperationException("Не задан PubSubOptions.ProjectId");
        }

        if (string.IsNullOrWhiteSpace(_pubSubOptions.SubscriptionId))
        {
            throw new InvalidOperationException("Не задан PubSubOptions.SubscriptionId");
        }

        if (string.IsNullOrWhiteSpace(_pubSubOptions.ServiceAccountJsonPath))
        {
            throw new InvalidOperationException("Не задан PubSubOptions.ServiceAccountJsonPath");
        }

        _ = GetValidatedTopicName();
    }

    private async Task WatchRenewLoopAsync(CancellationToken cancellationToken)
    {
        while (cancellationToken.IsCancellationRequested is false)
        {
            try
            {
                await Task.Delay(WatchRenewInterval, cancellationToken);
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

    private async Task EnsureWatchAsync(CancellationToken cancellationToken)
    {
        try
        {
            await EnsureWatchCoreAsync(cancellationToken);
        }
        catch (Google.GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogWarning(ex, "Не удалось настроить Gmail watch. Попробую пересоздать ресурсы Pub/Sub и повторить");

            await EnsurePubSubInfrastructureAsync(cancellationToken);
            await EnsureWatchCoreAsync(cancellationToken);
        }
    }

    private async Task EnsureWatchCoreAsync(CancellationToken cancellationToken)
    {
        var gmail = CreateGmailService();

        var request = new WatchRequest
        {
            TopicName = _gmailOptions.TopicName,
            LabelIds = _gmailOptions.LabelIds is { Length: > 0 } ? _gmailOptions.LabelIds : null,
            LabelFilterBehavior = "INCLUDE",
        };

        var watchResponse = await gmail.Users.Watch(request, _gmailOptions.UserId).ExecuteAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(watchResponse.HistoryId.ToString()) is false)
        {
            var current = await GetLastHistoryIdAsync(cancellationToken);

            if (string.IsNullOrWhiteSpace(current))
            {
                await SaveLastHistoryIdAsync(watchResponse.HistoryId.ToString(), cancellationToken);
            }
        }

        _logger.LogInformation(
            "Gmail watch обновлен. HistoryId: {HistoryId}, Expiration: {Expiration}",
            watchResponse.HistoryId,
            watchResponse.Expiration);
    }

    private GmailService CreateGmailService()
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

    private async Task EnsurePubSubInfrastructureAsync(CancellationToken cancellationToken)
    {
        var topicName = GetValidatedTopicName();
        var subscriptionName = SubscriptionName.FromProjectSubscription(_pubSubOptions.ProjectId, _pubSubOptions.SubscriptionId);

        var credential = GoogleCredential.FromFile(_pubSubOptions.ServiceAccountJsonPath);

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

    private TopicName GetValidatedTopicName()
    {
        TopicName topicName;

        try
        {
            topicName = TopicName.Parse(_gmailOptions.TopicName);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "Некорректный GmailOptions.TopicName. Ожидается формат projects/<projectId>/topics/<topicId>",
                ex);
        }

        if (string.Equals(topicName.ProjectId, _pubSubOptions.ProjectId, StringComparison.Ordinal) is false)
        {
            throw new InvalidOperationException("GmailOptions.TopicName должен использовать тот же ProjectId, что и PubSubOptions.ProjectId");
        }

        return topicName;
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
            throw new InvalidOperationException("Pub/Sub topic не найден при чтении IAM политики");
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

    private async Task<SubscriberClient> CreateSubscriberAsync(CancellationToken cancellationToken)
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

    private async Task<SubscriberClient.Reply> HandleMessageAsync(PubsubMessage message, CancellationToken cancellationToken)
    {
        await _handleLock.WaitAsync(cancellationToken);

        try
        {
            var notification = TryParseNotification(message.Data);

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

            var lastHistoryId = await GetLastHistoryIdAsync(cancellationToken);
            
            if (ulong.TryParse(lastHistoryId, out var last) && ulong.TryParse(notification.HistoryId, out var incoming))
            {
                if (incoming <= last)
                {
                    return SubscriberClient.Reply.Ack;
                }
            }

            if (string.IsNullOrWhiteSpace(lastHistoryId))
            {
                await SaveLastHistoryIdAsync(notification.HistoryId, cancellationToken);
                return SubscriberClient.Reply.Ack;
            }

            try
            {
                var processed = await ProcessChangesAsync(lastHistoryId, notification.HistoryId, cancellationToken);
                await SaveLastHistoryIdAsync(notification.HistoryId, cancellationToken);

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
                await SaveLastHistoryIdAsync(notification.HistoryId, cancellationToken);
                return SubscriberClient.Reply.Ack;
            }
            catch (Google.GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.NotFound)
            {
                _logger.LogWarning("HistoryId устарел или неверный. Выполняю повторный watch");
                await EnsureWatchAsync(cancellationToken);
                return SubscriberClient.Reply.Ack;
            }
            catch (TimeoutException ex)
            {
                _logger.LogWarning(ex, "Таймаут при ожидании RPC-ответа. Повтор не нужен");
                await SaveLastHistoryIdAsync(notification.HistoryId, cancellationToken);
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

    private async Task<int> ProcessChangesAsync(string startHistoryId, string newHistoryId, CancellationToken cancellationToken)
    {
        var gmail = CreateGmailService();
        var handled = new HashSet<string>(StringComparer.Ordinal);
        var processedCount = 0;

        var historyRequest = gmail.Users.History.List(_gmailOptions.UserId);
        historyRequest.StartHistoryId = ParseUlong(startHistoryId);
        historyRequest.HistoryTypes = UsersResource.HistoryResource.ListRequest.HistoryTypesEnum.MessageAdded;

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
        get.Format = Google.Apis.Gmail.v1.UsersResource.MessagesResource.GetRequest.FormatEnum.Raw;

        var msg = await get.ExecuteAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(msg.Raw))
        {
            return;
        }

        var rawBytes = Base64Url.Decode(msg.Raw);

        using var scope = _scopeFactory.CreateScope();
        var ingestor = scope.ServiceProvider.GetRequiredService<IEmailIngestor>();

        var request = EmailRequestFactory.Build(rawBytes);
        await ingestor.IngestAsync(request, cancellationToken);
    }

    private async Task<string?> GetLastHistoryIdAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IHistoryStateStore>();
        return await store.GetLastHistoryIdAsync(cancellationToken);
    }

    private async Task SaveLastHistoryIdAsync(string historyId, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IHistoryStateStore>();
        await store.SaveLastHistoryIdAsync(historyId, cancellationToken);
    }

    private static ulong ParseUlong(string value)
    {
        if (ulong.TryParse(value, out var result))
        {
            return result;
        }

        throw new InvalidOperationException("Некорректный historyId");
    }

    private static GmailNotification? TryParseNotification(ByteString data)
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

    public void Dispose()
    {
        _cts?.Dispose();
        _subscriber?.DisposeAsync();
        _handleLock.Dispose();
    }

    private sealed class GmailNotification
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

    private static class Base64Url
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

        public static string DecodeToString(string input)
        {
            var bytes = Decode(input);
            return System.Text.Encoding.UTF8.GetString(bytes);
        }
    }
}