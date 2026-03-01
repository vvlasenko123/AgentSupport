namespace Infrastructure.Database.Common.Models;

public sealed class EmailMessageModel
{
    public Guid Id { get; set; }
    public Guid ComplaintId { get; set; }
    public EmailMessageDirection Direction { get; set; }
    public string ExternalMessageId { get; set; } = string.Empty;
    public string? FromEmail { get; set; }
    public string? ToEmail { get; set; }
    public string? Subject { get; set; }
    public string? Content { get; set; }
    public string? ThreadId { get; set; }
    public DateTime? SentAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}