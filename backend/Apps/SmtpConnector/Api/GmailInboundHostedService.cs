using Google.Cloud.PubSub.V1;
using Microsoft.Extensions.Options;
using SmtpConnector.Api.Options;

namespace SmtpConnector.Api;

public sealed class GmailInboundHostedService : IHostedService, IDisposable
{
    private static readonly TimeSpan WatchRenewInterval = TimeSpan.FromDays(1);

    private readonly GmailOptions _gmailOptions;
    private readonly PubSubOptions _pubSubOptions;
    private readonly ILogger<GmailInboundHostedService> _logger;

    private readonly GmailOptionsValidator _validator;
    private readonly GmailServiceFactory _gmailServiceFactory;
    private readonly HistoryStateFacade _historyState;
    private readonly PubSubInfrastructureEnsurer _pubSubEnsurer;
    private readonly GmailWatchManager _watchManager;
    private readonly PubSubSubscriberFactory _subscriberFactory;
    private readonly GmailHistoryProcessor _historyProcessor;
    private readonly PubSubNotificationParser _notificationParser;

    private SubscriberClient? _subscriber;
    private Task? _subscriberTask;
    private CancellationTokenSource? _cts;
    private Task? _watchRenewTask;
    private GmailInboundMessageHandler? _messageHandler;

    public GmailInboundHostedService(
        IOptions<GmailOptions> gmailOptions,
        IOptions<PubSubOptions> pubSubOptions,
        ILogger<GmailInboundHostedService> logger,
        IServiceScopeFactory scopeFactory)
    {
        _gmailOptions = gmailOptions.Value;
        _pubSubOptions = pubSubOptions.Value;
        _logger = logger;

        _validator = new GmailOptionsValidator();
        _gmailServiceFactory = new GmailServiceFactory(_gmailOptions);
        _historyState = new HistoryStateFacade(scopeFactory);
        _pubSubEnsurer = new PubSubInfrastructureEnsurer(_pubSubOptions, _logger);

        _watchManager = new GmailWatchManager(
            _gmailOptions,
            _logger,
            _gmailServiceFactory,
            _historyState,
            _pubSubEnsurer,
            WatchRenewInterval);

        _subscriberFactory = new PubSubSubscriberFactory(_pubSubOptions);
        _historyProcessor = new GmailHistoryProcessor(_gmailOptions, _gmailServiceFactory, scopeFactory);
        _notificationParser = new PubSubNotificationParser();

        _messageHandler = new GmailInboundMessageHandler(
            _logger,
            _historyState,
            _watchManager,
            _historyProcessor,
            _notificationParser);
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var topicName = _validator.ValidateAndGetTopicName(_gmailOptions, _pubSubOptions);

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        await _pubSubEnsurer.EnsureAsync(topicName, _cts.Token);
        await _watchManager.EnsureWatchAsync(_cts.Token);

        _watchRenewTask = Task.Run(() => _watchManager.WatchRenewLoopAsync(_cts.Token), _cts.Token);

        _subscriber = await _subscriberFactory.CreateSubscriberAsync(_cts.Token);

        if (_messageHandler is null)
        {
            throw new InvalidOperationException("Не удалось создать обработчик сообщений");
        }

        _subscriberTask = _subscriber.StartAsync(_messageHandler.HandleAsync);

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

    public void Dispose()
    {
        _cts?.Dispose();
        _subscriber?.DisposeAsync();
        _messageHandler?.Dispose();
    }
}