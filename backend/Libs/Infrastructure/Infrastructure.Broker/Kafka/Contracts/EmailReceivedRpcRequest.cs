namespace Infrastructure.Broker.Kafka.Contracts;

public sealed class EmailReceivedRpcRequest
{
    public string MessageId { get; set; } = string.Empty;
    public string FromName { get; set; } = string.Empty;
    public string FromEmail { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public DateTime SentAtUtc { get; set; }

    public string Content { get; set; } = string.Empty;
}