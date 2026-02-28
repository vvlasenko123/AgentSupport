using System.Text.Json;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Gmail.v1;
using Google.Apis.Gmail.v1.Data;
using Google.Apis.Services;
using Google.Cloud.PubSub.V1;
using Google.Protobuf;
using Infrastructure.Broker.Kafka.Contracts.Interfaces;
using Microsoft.Extensions.Options;
using SmtpConnector.Api.Factory;
using SmtpConnector.Api.Options;
using SmtpConnector.Dal.History.Interfaces;

namespace SmtpConnector.Api;

public sealed class GmailInboundHostedService : IHostedService, IDisposable
{
    private static readonly TimeSpan WatchRenewInterval = TimeSpan.FromDays(1);

    private readonly GmailOptions _gmailOptions;
    private readonly PubSubOptions _pubSubOptions;
    private readonly ILogger<GmailInboundHostedService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly IHistoryStateStore _historyStateStore;

    private SubscriberClient? _subscriber;
    private CancellationTokenSource? _cts;
    private Task? _watchRenewTask;
    private readonly SemaphoreSlim _handleLock;

    public GmailInboundHostedService(
        IOptions<GmailOptions> gmailOptions,
        IOptions<PubSubOptions> pubSubOptions,
        IHistoryStateStore historyStateStore,
        ILogger<GmailInboundHostedService> logger,
        IServiceProvider serviceProvider)
    {
        _gmailOptions = gmailOptions.Value;
        _pubSubOptions = pubSubOptions.Value;
        _historyStateStore = historyStateStore;
        _logger = logger;
        _serviceProvider = serviceProvider;
        _handleLock = new SemaphoreSlim(1, 1);
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        ValidateOptions();

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        await EnsureWatchAsync(_cts.Token);

        _watchRenewTask = Task.Run(() => WatchRenewLoopAsync(_cts.Token), _cts.Token);

        _subscriber = await CreateSubscriberAsync(_cts.Token);
        _ = _subscriber.StartAsync(HandleMessageAsync);

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
            var current = await _historyStateStore.GetLastHistoryIdAsync(cancellationToken);

            if (string.IsNullOrWhiteSpace(current))
            {
                await _historyStateStore.SaveLastHistoryIdAsync(watchResponse.HistoryId.ToString(), cancellationToken);
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
                _logger.LogWarning("Не удалось разобрать уведомление Pub/Sub");
                return SubscriberClient.Reply.Ack;
            }

            var lastHistoryId = await _historyStateStore.GetLastHistoryIdAsync(cancellationToken);

            if (string.IsNullOrWhiteSpace(lastHistoryId))
            {
                await _historyStateStore.SaveLastHistoryIdAsync(notification.HistoryId, cancellationToken);
                return SubscriberClient.Reply.Ack;
            }

            try
            {
                await ProcessChangesAsync(lastHistoryId, notification.HistoryId, cancellationToken);
                await _historyStateStore.SaveLastHistoryIdAsync(notification.HistoryId, cancellationToken);
                return SubscriberClient.Reply.Ack;
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Письмо отклонено обработчиком");
                await _historyStateStore.SaveLastHistoryIdAsync(notification.HistoryId, cancellationToken);
                return SubscriberClient.Reply.Ack;
            }
            catch (Google.GoogleApiException ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning("HistoryId устарел или неверный. Выполняю повторный watch");
                await EnsureWatchAsync(cancellationToken);
                return SubscriberClient.Reply.Ack;
            }
            catch (TimeoutException ex)
            {
                _logger.LogWarning(ex, "Таймаут при вызове AgentSupportApi. Повторю обработку");
                return SubscriberClient.Reply.Nack;
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

    private async Task ProcessChangesAsync(string startHistoryId, string newHistoryId, CancellationToken cancellationToken)
    {
        var gmail = CreateGmailService();

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

                        await HandleMessageIdAsync(gmail, id, cancellationToken);
                    }
                }
            }

            pageToken = response.NextPageToken;
        }
        while (string.IsNullOrWhiteSpace(pageToken) is false);

        _logger.LogInformation("История обработана. startHistoryId={Start} newHistoryId={New}", startHistoryId, newHistoryId);
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

        using var scope = _serviceProvider.CreateScope();
        var ingestor = scope.ServiceProvider.GetRequiredService<IEmailIngestor>();

        var request = EmailRequestFactory.Build(rawBytes);
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

    private static GmailNotification? TryParseNotification(ByteString data)
    {
        var text = data.ToStringUtf8();

        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(text);
            return GmailNotification.From(doc);
        }
        catch
        {
            try
            {
                var decoded = Base64Url.DecodeToString(text);
                using var doc = JsonDocument.Parse(decoded);
                return GmailNotification.From(doc);
            }
            catch
            {
                return null;
            }
        }
    }

    public void Dispose()
    {
        _cts?.Dispose();
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
            var historyId = historyIdEl.GetString();

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
            var base64 = input.Replace('-', '+').Replace('_', '/');

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