namespace SmtpConnector.Api.Options;

public sealed class PubSubOptions
{
    public string ProjectId { get; set; } = string.Empty;

    public string SubscriptionId { get; set; } = string.Empty;

    public string ServiceAccountJsonPath { get; set; } = string.Empty;
}