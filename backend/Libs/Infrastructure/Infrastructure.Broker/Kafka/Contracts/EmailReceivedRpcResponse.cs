namespace Infrastructure.Broker.Kafka.Contracts;

public sealed class EmailReceivedRpcResponse
{
    public bool Accepted { get; set; }

    public string? Error { get; set; }
}