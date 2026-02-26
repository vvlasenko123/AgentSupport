using System.Text.Json;
using Confluent.Kafka;
using Infrastructure.Broker.Kafka.Rpc.Envelope;
using Infrastructure.Broker.Kafka.Rpc.Handler.Interfaces;
using Infrastructure.Broker.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Broker.Kafka.Rpc.Services;

/// <summary>
/// Kafka RPC server
/// </summary>
public sealed class KafkaRpcServerHostedService : BackgroundService
{
    private static readonly TimeSpan KafkaWaitDelay = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan KafkaWaitLogInterval = TimeSpan.FromSeconds(30);

    private readonly KafkaOptions _options;
    private readonly ILogger<KafkaRpcServerHostedService> _logger;
    private readonly IReadOnlyDictionary<string, IKafkaRpcHandler> _handlers;
    private readonly JsonSerializerOptions _jsonOptions;

    public KafkaRpcServerHostedService(
        IOptions<KafkaOptions> options,
        IEnumerable<IKafkaRpcHandler> handlers,
        ILogger<KafkaRpcServerHostedService> logger)
    {
        _options = options.Value;
        _logger = logger;
        _handlers = handlers.ToDictionary(x => x.Contract, StringComparer.Ordinal);
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };
    }

    /// <inheritdoc />
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return RunAsync(stoppingToken);
    }

    private async Task RunAsync(CancellationToken stoppingToken)
    {
        var topics = _handlers.Keys.ToArray();

        if (topics.Length == 0)
        {
            _logger.LogWarning("Обработчики Kafka RPC не зарегистрированы. Kafka RPC server не будет запущен");
            return;
        }

        await WaitKafkaReadyAsync(stoppingToken);

        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = _options.BootstrapServers,
            GroupId = $"rpc-server-{Environment.MachineName}",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false,
        };

        var producerConfig = new ProducerConfig
        {
            BootstrapServers = _options.BootstrapServers,
        };

        using var consumer = new ConsumerBuilder<string, string>(consumerConfig)
            .SetLogHandler((_, _) => { })
            .SetErrorHandler((_, _) => { })
            .Build();

        using var producer = new ProducerBuilder<string, string>(producerConfig)
            .SetLogHandler((_, _) => { })
            .SetErrorHandler((_, _) => { })
            .Build();

        consumer.Subscribe(topics);

        _logger.LogInformation("Kafka RPC server запущен. Topics: {Topics}", string.Join(", ", topics));

        while (stoppingToken.IsCancellationRequested is false)
        {
            try
            {
                var result = consumer.Consume(stoppingToken);

                if (result?.Message?.Value is null)
                {
                    continue;
                }

                var requestEnvelope = JsonSerializer.Deserialize<KafkaRpcRequestEnvelope>(result.Message.Value, _jsonOptions);

                if (requestEnvelope is null)
                {
                    consumer.Commit(result);
                    continue;
                }

                var responseEnvelope = await HandleAsync(requestEnvelope, stoppingToken);

                var responseJson = JsonSerializer.Serialize(responseEnvelope, _jsonOptions);

                if (string.IsNullOrWhiteSpace(requestEnvelope.ReplyToTopic))
                {
                    throw new InvalidOperationException("В RPC-запросе не задан ReplyToTopic");
                }

                await producer.ProduceAsync(
                    requestEnvelope.ReplyToTopic,
                    new Message<string, string>
                    {
                        Key = requestEnvelope.CorrelationId,
                        Value = responseJson,
                    },
                    stoppingToken);

                consumer.Commit(result);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ConsumeException ex)
            {
                _logger.LogError(ex, "Ошибка чтения RPC-запроса из Kafka");
                await WaitKafkaReadyAsync(stoppingToken);
            }
            catch (KafkaException ex)
            {
                _logger.LogError(ex, "Ошибка Kafka RPC server");
                await WaitKafkaReadyAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Неожиданная ошибка в Kafka RPC server");
            }
        }

        consumer.Close();

        _logger.LogInformation("Kafka RPC server остановлен");
    }

    /// <summary>
    /// Ждет доступности Kafka
    /// </summary>
    private async Task WaitKafkaReadyAsync(CancellationToken cancellationToken)
    {
        var lastLogAt = DateTime.MinValue;

        while (cancellationToken.IsCancellationRequested is false)
        {
            try
            {
                var config = new AdminClientConfig
                {
                    BootstrapServers = _options.BootstrapServers,
                };

                using var admin = new AdminClientBuilder(config)
                    .SetLogHandler((_, _) => { })
                    .SetErrorHandler((_, _) => { })
                    .Build();

                _ = admin.GetMetadata(TimeSpan.FromSeconds(3));

                return;
            }
            catch (Exception)
            {
                if (DateTime.UtcNow - lastLogAt >= KafkaWaitLogInterval)
                {
                    _logger.LogWarning("Kafka недоступна. Ожидание подключения к {BootstrapServers}", _options.BootstrapServers);
                    lastLogAt = DateTime.UtcNow;
                }

                await Task.Delay(KafkaWaitDelay, cancellationToken);
            }
        }
    }

    /// <summary>
    /// Обрабатывает один RPC-запрос и формирует envelope ответа
    /// </summary>
    private async Task<KafkaRpcResponseEnvelope> HandleAsync(KafkaRpcRequestEnvelope requestEnvelope, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(requestEnvelope.CorrelationId))
        {
            return new KafkaRpcResponseEnvelope
            {
                CorrelationId = string.Empty,
                Contract = string.Empty,
                IsSuccess = false,
                Error = "Не задан CorrelationId",
            };
        }

        if (string.IsNullOrWhiteSpace(requestEnvelope.Contract))
        {
            return new KafkaRpcResponseEnvelope
            {
                CorrelationId = requestEnvelope.CorrelationId,
                Contract = string.Empty,
                IsSuccess = false,
                Error = "Не задан контракт запроса",
            };
        }

        if (_handlers.TryGetValue(requestEnvelope.Contract, out var handler) is false)
        {
            return new KafkaRpcResponseEnvelope
            {
                CorrelationId = requestEnvelope.CorrelationId,
                Contract = requestEnvelope.ResponseContract,
                IsSuccess = false,
                Error = $"Обработчик для контракта '{requestEnvelope.Contract}' не найден",
            };
        }

        try
        {
            var payload = await handler.HandleAsync(requestEnvelope.Payload, cancellationToken);

            return new KafkaRpcResponseEnvelope
            {
                CorrelationId = requestEnvelope.CorrelationId,
                Contract = requestEnvelope.ResponseContract,
                IsSuccess = true,
                Payload = payload,
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка обработки RPC-запроса. Contract: {Contract}", requestEnvelope.Contract);

            return new KafkaRpcResponseEnvelope
            {
                CorrelationId = requestEnvelope.CorrelationId,
                Contract = requestEnvelope.ResponseContract,
                IsSuccess = false,
                Error = "Ошибка обработки RPC-запроса",
            };
        }
    }
}