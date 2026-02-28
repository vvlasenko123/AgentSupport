using System.Text.Json;
using Confluent.Kafka;
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

    public KafkaBrokerCommandor(IOptions<KafkaOptions> options, ILogger<KafkaBrokerCommandor> logger)
    {
        _options = options.Value;
        _logger = logger;

        var producerConfig = new ProducerConfig
        {
            BootstrapServers = _options.BootstrapServers,
        };

        _producer = new ProducerBuilder<string, string>(producerConfig).Build();

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

    /// <summary>
    /// Создает consumer, который подписывается на топик ответов
    /// </summary>
    private IConsumer<string, string> CreateResponseConsumer(string responseTopic)
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = _options.BootstrapServers,
            GroupId = $"rpc-client-{Guid.NewGuid():N}",
            AutoOffsetReset = AutoOffsetReset.Latest,
            EnableAutoCommit = false,
        };

        var consumer = new ConsumerBuilder<string, string>(config).Build();

        consumer.Subscribe(responseTopic);

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