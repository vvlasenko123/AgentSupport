using System.Collections.Concurrent;
using System.Text.Json;
using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Infrastructure.Broker.Kafka.Rpc.Envelope;
using Infrastructure.Broker.Kafka.Rpc.Handler.Interfaces;
using Infrastructure.Broker.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Broker.Kafka.Rpc.Services;

public sealed class KafkaRpcServerHostedService : BackgroundService
{
    private const int DefaultTopicPartitions = 1;
    private const short DefaultTopicReplicationFactor = 1;

    private readonly KafkaOptions _options;
    private readonly ILogger<KafkaRpcServerHostedService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly string[] _contracts;

    private readonly ConcurrentDictionary<string, byte> _ensuredTopics;

    public KafkaRpcServerHostedService(
        IOptions<KafkaOptions> options,
        IServiceScopeFactory scopeFactory,
        ILogger<KafkaRpcServerHostedService> logger)
    {
        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        _options = options.Value;
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _ensuredTopics = new ConcurrentDictionary<string, byte>(StringComparer.Ordinal);

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        if (string.IsNullOrWhiteSpace(_options.BootstrapServers))
        {
            throw new InvalidOperationException("Не задан KafkaOptions.BootstrapServers");
        }

        using (var scope = _scopeFactory.CreateScope())
        {
            var handlers = scope.ServiceProvider.GetServices<IKafkaRpcHandler>().ToArray();

            if (handlers.Length == 0)
            {
                throw new InvalidOperationException("Не зарегистрированы IKafkaRpcHandler");
            }

            _contracts = handlers
                .Select(x => x.Contract)
                .Where(x => string.IsNullOrWhiteSpace(x) is false)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }

        if (_contracts.Length == 0)
        {
            throw new InvalidOperationException("Не зарегистрированы IKafkaRpcHandler");
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var adminConfig = new AdminClientConfig
        {
            BootstrapServers = _options.BootstrapServers,
        };

        using var admin = new AdminClientBuilder(adminConfig).Build();

        await EnsureTopicsCreatedAsync(admin, _contracts, stoppingToken);

        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = _options.BootstrapServers,
            GroupId = "rpc-server",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false,
        };

        var producerConfig = new ProducerConfig
        {
            BootstrapServers = _options.BootstrapServers,
        };

        using var consumer = new ConsumerBuilder<string, string>(consumerConfig).Build();
        using var producer = new ProducerBuilder<string, string>(producerConfig).Build();

        consumer.Subscribe(_contracts);

        _logger.LogInformation("Kafka RPC server запущен. Топики: {Topics}", string.Join(", ", _contracts));

        while (stoppingToken.IsCancellationRequested is false)
        {
            ConsumeResult<string, string>? cr = null;

            try
            {
                cr = consumer.Consume(TimeSpan.FromMilliseconds(500));

                if (cr?.Message?.Value is null)
                {
                    continue;
                }

                var requestEnvelope = JsonSerializer.Deserialize<KafkaRpcRequestEnvelope>(cr.Message.Value, _jsonOptions);

                if (requestEnvelope is null)
                {
                    consumer.Commit(cr);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(requestEnvelope.ReplyToTopic) is false)
                {
                    await EnsureTopicCreatedAsync(admin, requestEnvelope.ReplyToTopic, stoppingToken);
                }

                using var scope = _scopeFactory.CreateScope();

                var handler = scope.ServiceProvider
                    .GetServices<IKafkaRpcHandler>()
                    .FirstOrDefault(x => string.Equals(x.Contract, requestEnvelope.Contract, StringComparison.Ordinal));

                if (handler is null)
                {
                    await ProduceErrorAsync(producer, requestEnvelope, "Не найден обработчик для контракта", stoppingToken);
                    consumer.Commit(cr);
                    continue;
                }

                var responsePayloadJson = await handler.HandleAsync(requestEnvelope.Payload, stoppingToken);

                var responseEnvelope = new KafkaRpcResponseEnvelope
                {
                    CorrelationId = requestEnvelope.CorrelationId,
                    Contract = requestEnvelope.ResponseContract,
                    IsSuccess = true,
                    Payload = responsePayloadJson,
                };

                var responseJson = JsonSerializer.Serialize(responseEnvelope, _jsonOptions);

                await producer.ProduceAsync(
                    requestEnvelope.ReplyToTopic,
                    new Message<string, string>
                    {
                        Key = requestEnvelope.CorrelationId,
                        Value = responseJson
                    },
                    stoppingToken);

                consumer.Commit(cr);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка обработки Kafka RPC сообщения");

                if (cr is not null)
                {
                    try
                    {
                        consumer.Commit(cr);
                    }
                    catch
                    {
                    }
                }
            }
        }
    }

    private async Task EnsureTopicsCreatedAsync(
        IAdminClient adminClient,
        string[] topics,
        CancellationToken cancellationToken)
    {
        foreach (var topic in topics)
        {
            await EnsureTopicCreatedAsync(adminClient, topic, cancellationToken);
        }
    }

    private async Task EnsureTopicCreatedAsync(
        IAdminClient adminClient,
        string topic,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(topic))
        {
            return;
        }

        if (_ensuredTopics.TryAdd(topic, 0) is false)
        {
            return;
        }

        try
        {
            await adminClient.CreateTopicsAsync(
                new[]
                {
                    new TopicSpecification
                    {
                        Name = topic,
                        NumPartitions = DefaultTopicPartitions,
                        ReplicationFactor = DefaultTopicReplicationFactor,
                    }
                });

            _logger.LogInformation("Создан топик Kafka: {Topic}", topic);
        }
        catch (CreateTopicsException ex)
        {
            var topicResult = ex.Results.FirstOrDefault(x => string.Equals(x.Topic, topic, StringComparison.Ordinal));

            if (topicResult is not null && topicResult.Error.Code == ErrorCode.TopicAlreadyExists)
            {
                return;
            }

            _ensuredTopics.TryRemove(topic, out _);
            _logger.LogWarning(ex, "Не удалось создать топик Kafka: {Topic}", topic);
        }
        catch (Exception ex)
        {
            _ensuredTopics.TryRemove(topic, out _);
            _logger.LogWarning(ex, "Не удалось создать топик Kafka: {Topic}", topic);
        }
    }

    private async Task ProduceErrorAsync(
        IProducer<string, string> producer,
        KafkaRpcRequestEnvelope request,
        string error,
        CancellationToken cancellationToken)
    {
        var responseEnvelope = new KafkaRpcResponseEnvelope
        {
            CorrelationId = request.CorrelationId,
            Contract = request.ResponseContract,
            IsSuccess = false,
            Error = error,
            Payload = string.Empty,
        };

        var responseJson = JsonSerializer.Serialize(responseEnvelope, _jsonOptions);

        await producer.ProduceAsync(
            request.ReplyToTopic,
            new Message<string, string>
            {
                Key = request.CorrelationId,
                Value = responseJson
            },
            cancellationToken);
    }
}