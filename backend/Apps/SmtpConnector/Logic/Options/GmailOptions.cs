namespace SmtpConnector.Api.Options;

public sealed class GmailOptions
{
    public string ApplicationName { get; set; } = "SmtpConnector";

    public string UserId { get; set; } = "me";

    public string ClientId { get; set; } = string.Empty;

    public string ClientSecret { get; set; } = string.Empty;

    public string RefreshToken { get; set; } = string.Empty;

    public string TopicName { get; set; } = string.Empty;

    public string[] LabelIds { get; set; } = Array.Empty<string>();
}