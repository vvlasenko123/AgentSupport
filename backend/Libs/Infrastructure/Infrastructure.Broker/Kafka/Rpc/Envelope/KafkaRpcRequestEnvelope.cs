namespace Infrastructure.Broker.Kafka.Rpc.Envelope;

/// <summary>
/// Envelope RPC-запроса
/// </summary>
public sealed class KafkaRpcRequestEnvelope
{
    /// <summary>
    /// айди кореляции
    /// </summary>
    public string CorrelationId { get; set; } = string.Empty;

    /// <summary>
    /// Имя контракта запроса (topic = Contract)
    /// </summary>
    public string Contract { get; set; } = string.Empty;

    /// <summary>
    /// Топик для ответа (имя модели ответа)
    /// </summary>
    public string ReplyToTopic { get; set; } = string.Empty;

    /// <summary>
    /// Имя контракта ответа
    /// </summary>
    public string ResponseContract { get; set; } = string.Empty;

    /// <summary>
    /// JSON payload запроса
    /// </summary>
    public string Payload { get; set; } = string.Empty;
}