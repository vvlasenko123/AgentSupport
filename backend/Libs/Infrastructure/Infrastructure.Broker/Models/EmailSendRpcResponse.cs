namespace Infrastructure.Broker.Models;

public sealed class EmailSendRpcResponse
{
    public bool Accepted { get; set; }
    public string? Error { get; set; }
}