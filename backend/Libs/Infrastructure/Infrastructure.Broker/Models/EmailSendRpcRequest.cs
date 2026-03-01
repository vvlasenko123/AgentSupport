namespace Infrastructure.Broker.Models;

public sealed class EmailSendRpcRequest
{
    public string? ToEmail { get; set; }
    public string? Subject { get; set; }
    public string? Body { get; set; }
    public string? InReplyTo { get; set; }
    public string[]? References { get; set; }
    public string? ThreadId { get; set; }
}
