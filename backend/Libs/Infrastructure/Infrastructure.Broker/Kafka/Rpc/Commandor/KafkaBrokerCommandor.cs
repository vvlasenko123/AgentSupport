using System.Text.Json;
using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Infrastructure.Broker.Kafka.Contract;
using Infrastructure.Broker.Kafka.Rpc.Envelope;
using Infrastructure.Broker.Kafka.Rpc.Interfaces;
using Infrastructure.Broker.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Broker.Kafka.Rpc.Commandor;

/// <summary>
/// Командор Kafka RPC
/// </summary>
public sealed class KafkaBrokerCommandor : IKafkaBrokerCommandor, IDisposable
{
    private readonly KafkaOptions _options;
    private readonly ILogger<KafkaBrokerCommandor> _logger;
    private readonly IProducer<string, string> _producer;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly IAdminClient _admin;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, byte> _ensuredTopics = new();

    // KafkaBrokerCommandor.cs
    public KafkaBrokerCommandor(IOptions<KafkaOptions> options, ILogger<KafkaBrokerCommandor> logger)
    {
        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        if (logger is null)
        {
            throw new ArgumentNullException(nameof(logger));
        }

        _options = options.Value;
        _logger = logger;

        if (string.IsNullOrWhiteSpace(_options.BootstrapServers))
        {
            throw new InvalidOperationException("Не задан KafkaOptions.BootstrapServers");
        }

        var producerConfig = new ProducerConfig
        {
            BootstrapServers = _options.BootstrapServers,
        };

        _producer = new ProducerBuilder<string, string>(producerConfig).Build();

        var adminConfig = new AdminClientConfig
        {
            BootstrapServers = _options.BootstrapServers,
        };

        _admin = new AdminClientBuilder(adminConfig).Build();

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };
    }

    /// <inheritdoc />
    public async Task<TResponse> SendRpc<TRequest, TResponse>(TRequest request, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request), "Запрос не может быть null");
        }

        var requestTopic = KafkaContractName.Get<TRequest>();
        var responseTopic = KafkaContractName.Get<TResponse>();

        await EnsureTopicsAsync(requestTopic, responseTopic, cancellationToken);

        var correlationId = Guid.NewGuid().ToString("N");

        var envelope = new KafkaRpcRequestEnvelope
        {
            CorrelationId = correlationId,
            Contract = requestTopic,
            ReplyToTopic = responseTopic,
            ResponseContract = responseTopic,
            Payload = JsonSerializer.Serialize(request, _jsonOptions),
        };

        var envelopeJson = JsonSerializer.Serialize(envelope, _jsonOptions);

        _logger.LogInformation("Отправка RPC в Kafka. Topic: {Topic}, CorrelationId: {CorrelationId}", requestTopic, correlationId);

        using var consumer = CreateResponseConsumer(responseTopic);

        await _producer.ProduceAsync(
            requestTopic,
            new Message<string, string>
            {
                Key = correlationId,
                Value = envelopeJson,
            },
            cancellationToken);

        return WaitResponse<TResponse>(consumer, responseTopic, correlationId, cancellationToken);
    }

    private async Task EnsureTopicsAsync(string requestTopic, string responseTopic, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(requestTopic))
        {
            throw new InvalidOperationException("Не задан request topic");
        }

        if (string.IsNullOrWhiteSpace(responseTopic))
        {
            throw new InvalidOperationException("Не задан response topic");
        }

        cancellationToken.ThrowIfCancellationRequested();

        var needCreateRequest = _ensuredTopics.TryAdd(requestTopic, 0);
        var needCreateResponse = _ensuredTopics.TryAdd(responseTopic, 0);

        if (needCreateRequest is false && needCreateResponse is false)
        {
            return;
        }

        var topics = new List<TopicSpecification>();

        if (needCreateRequest)
        {
            topics.Add(new TopicSpecification
            {
                Name = requestTopic,
                NumPartitions = 1,
                ReplicationFactor = 1
            });
        }

        if (needCreateResponse)
        {
            topics.Add(new TopicSpecification
            {
                Name = responseTopic,
                NumPartitions = 1,
                ReplicationFactor = 1
            });
        }

        try
        {
            await _admin.CreateTopicsAsync(topics);

            await Task.Delay(200, cancellationToken);
        }
        catch (CreateTopicsException ex)
        {
            foreach (var result in ex.Results)
            {
                if (result.Error.Code == ErrorCode.NoError)
                {
                    continue;
                }

                if (result.Error.Code == ErrorCode.TopicAlreadyExists)
                {
                    continue;
                }

                if (needCreateRequest)
                {
                    _ensuredTopics.TryRemove(requestTopic, out _);
                }

                if (needCreateResponse)
                {
                    _ensuredTopics.TryRemove(responseTopic, out _);
                }

                throw new InvalidOperationException(
                    $"Не удалось создать Kafka topic {result.Topic}. Причина: {result.Error.Reason}");
            }
        }
    }

    /// <summary>
    /// Создает consumer, который подписывается на топик ответов
    /// </summary>
    private IConsumer<string, string> CreateResponseConsumer(string responseTopic)
    {
        if (string.IsNullOrWhiteSpace(responseTopic))
        {
            throw new InvalidOperationException("Не задан response topic");
        }

        var config = new ConsumerConfig
        {
            BootstrapServers = _options.BootstrapServers,
            GroupId = $"rpc-client-{Guid.NewGuid():N}",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false,
        };

        var consumer = new ConsumerBuilder<string, string>(config).Build();

        consumer.Subscribe(responseTopic);

        var deadline = DateTime.UtcNow.AddSeconds(5);

        while (consumer.Assignment.Count == 0 && DateTime.UtcNow < deadline)
        {
            consumer.Consume(TimeSpan.FromMilliseconds(200));
        }

        if (consumer.Assignment.Count == 0)
        {
            consumer.Close();
            throw new InvalidOperationException("Не удалось получить назначение партиций для топика ответа");
        }

        foreach (var tp in consumer.Assignment)
        {
            var watermark = consumer.QueryWatermarkOffsets(tp, TimeSpan.FromSeconds(5));
            consumer.Seek(new TopicPartitionOffset(tp, watermark.High));
        }

        return consumer;
    }

    /// <summary>
    /// Ожидает RPC-ответ в топике ответа по correlationId до истечения таймаута
    /// </summary>
    private TResponse WaitResponse<TResponse>(
        IConsumer<string, string> consumer,
        string responseTopic,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var timeoutAt = DateTime.UtcNow.AddSeconds(_options.RpcTimeoutSeconds);

        while (DateTime.UtcNow < timeoutAt)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = consumer.Consume(TimeSpan.FromMilliseconds(250));

            if (result?.Message?.Value is null)
            {
                continue;
            }

            var envelope = JsonSerializer.Deserialize<KafkaRpcResponseEnvelope>(result.Message.Value, _jsonOptions);

            if (envelope is null)
            {
                continue;
            }

            if (string.Equals(envelope.CorrelationId, correlationId, StringComparison.Ordinal) is false)
            {
                continue;
            }

            if (envelope.IsSuccess is false)
            {
                throw new InvalidOperationException(string.IsNullOrWhiteSpace(envelope.Error)
                    ? "RPC вернул ошибку"
                    : envelope.Error);
            }

            if (string.IsNullOrWhiteSpace(envelope.Payload))
            {
                throw new InvalidOperationException("RPC вернул пустой ответ");
            }

            if (string.Equals(envelope.Contract, responseTopic, StringComparison.Ordinal) is false)
            {
                throw new InvalidOperationException("RPC вернул ответ неподходящего контракта");
            }

            var response = JsonSerializer.Deserialize<TResponse>(envelope.Payload, _jsonOptions);

            if (response is null)
            {
                throw new InvalidOperationException("Не удалось разобрать RPC-ответ");
            }

            consumer.Close();

            return response;
        }

        consumer.Close();

        throw new TimeoutException("Истек таймаут ожидания RPC-ответа");
    }

    /// <inheritdoc />
    public void Dispose()
    {
        try
        {
            _producer.Flush(TimeSpan.FromSeconds(5));
        }
        finally
        {
            _producer.Dispose();
        }
    }
}