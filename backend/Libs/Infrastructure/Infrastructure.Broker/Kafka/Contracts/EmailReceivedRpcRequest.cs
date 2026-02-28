namespace Infrastructure.Broker.Kafka.Contracts;

public sealed class EmailReceivedRpcRequest
{
    public string MessageId { get; set; } = string.Empty;

    public string? From { get; set; }

    public List<string> To { get; set; } = new List<string>();

    public string? Subject { get; set; }

    public string RawMimeBase64 { get; set; } = string.Empty;
}