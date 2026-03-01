using System.Text.Json.Serialization;

namespace Infrastructure.Broker.Kafka.Contracts;

public sealed class MlProcessRequest
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("complaintId")]
    public string ComplaintId { get; set; } = string.Empty;

    [JsonPropertyName("direction")]
    public string Direction { get; set; } = string.Empty;

    [JsonPropertyName("externalMessageId")]
    public string ExternalMessageId { get; set; } = string.Empty;

    [JsonPropertyName("fromEmail")]
    public string? FromEmail { get; set; }

    [JsonPropertyName("fromName")]
    public string? FromName { get; set; }

    [JsonPropertyName("toEmail")]
    public string? ToEmail { get; set; }

    [JsonPropertyName("subject")]
    public string? Subject { get; set; }

    [JsonPropertyName("content")]
    public string? Content { get; set; }

    [JsonPropertyName("threadId")]
    public string? ThreadId { get; set; }

    [JsonPropertyName("sentAtUtc")]
    public DateTime SentAtUtc { get; set; }

    [JsonPropertyName("createdAtUtc")]
    public DateTime CreatedAtUtc { get; set; }
}

public sealed class MlComplaintResponse
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("submissionDate")]
    public DateTime? SubmissionDate { get; set; }

    [JsonPropertyName("fio")]
    public string? Fio { get; set; }

    [JsonPropertyName("objectName")]
    public string? ObjectName { get; set; }

    [JsonPropertyName("phoneNumber")]
    public string? PhoneNumber { get; set; }

    [JsonPropertyName("email")]
    public string? Email { get; set; }

    [JsonPropertyName("serialNumbers")]
    public string[]? SerialNumbers { get; set; }

    [JsonPropertyName("deviceType")]
    public string? DeviceType { get; set; }

    [JsonPropertyName("emotionalTone")]
    public string? EmotionalTone { get; set; }

    [JsonPropertyName("issueSummary")]
    public string? IssueSummary { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("category")]
    public string? Category { get; set; }

    [JsonPropertyName("suggestedAnswer")]
    public string? SuggestedAnswer { get; set; }
}