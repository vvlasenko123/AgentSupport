namespace Infrastructure.Broker.Kafka.Rpc.Envelope;

/// <summary>
/// Envelope RPC-ответа
/// </summary>
public sealed class KafkaRpcResponseEnvelope
{
    /// <summary>
    /// айди кореляции
    /// </summary>
    public string CorrelationId { get; set; } = string.Empty;

    /// <summary>
    /// Идентификатор контракта ответа
    /// </summary>
    public string Contract { get; set; } = string.Empty;

    /// <summary>
    /// успех
    /// </summary>
    public bool IsSuccess { get; set; }

    /// <summary>
    /// JSON payload ответа
    /// </summary>
    public string? Payload { get; set; }

    /// <summary>
    /// Текст ошибки
    /// </summary>
    public string? Error { get; set; }
}