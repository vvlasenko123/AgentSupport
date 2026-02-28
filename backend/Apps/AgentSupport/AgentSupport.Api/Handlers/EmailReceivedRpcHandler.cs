using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AgentSupport.Domain.Models.Complaints;
using Infrastructure.Broker.Kafka.Contract;
using Infrastructure.Broker.Kafka.Contracts;
using Infrastructure.Broker.Kafka.Rpc.Handler.Interfaces;
using Infrastructure.Database.Common.Interfaces;

namespace AgentSupport.Api.Handlers;

public sealed class EmailReceivedRpcHandler : IKafkaRpcHandler
{
    private readonly ILogger<EmailReceivedRpcHandler> _logger;
    private readonly IRepository<ComplaintModel> _complaints;
    private readonly JsonSerializerOptions _jsonOptions;

    public EmailReceivedRpcHandler(
        ILogger<EmailReceivedRpcHandler> logger,
        IRepository<ComplaintModel> complaints)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _complaints = complaints ?? throw new ArgumentNullException(nameof(complaints));

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };
    }

    public string Contract => KafkaContractName.Get<EmailReceivedRpcRequest>();

    public async Task<string> HandleAsync(string payloadJson, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            return SerializeError("Пустой payload");
        }

        EmailReceivedRpcRequest? request;

        try
        {
            request = JsonSerializer.Deserialize<EmailReceivedRpcRequest>(payloadJson, _jsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Не удалось разобрать payload");
            return SerializeError("Не удалось разобрать payload");
        }

        if (request is null)
        {
            return SerializeError("Payload не распознан");
        }

        if (string.IsNullOrWhiteSpace(request.MessageId))
        {
            return SerializeError("Не задан messageId");
        }

        _logger.LogInformation(
            "Получено письмо. MessageId={MessageId}, FromName={FromName}, FromEmail={FromEmail}, Subject={Subject}, SentAtUtc={SentAtUtc}, ContentLength={ContentLength}, Content={Content}",
            request.MessageId,
            request.FromName,
            request.FromEmail,
            request.Subject,
            request.SentAtUtc,
            request.Content?.Length ?? 0,
            request.Content ?? string.Empty);

        var complaintId = CreateDeterministicGuid(request.MessageId);

        var existing = await _complaints.GetByIdAsync(complaintId, cancellationToken);

        if (existing is not null)
        {
            return JsonSerializer.Serialize(new EmailReceivedRpcResponse { Accepted = true }, _jsonOptions);
        }

        var objectName = ExtractDomain(request.FromEmail);

        var submissionDate = request.SentAtUtc == default
            ? DateTime.UtcNow
            : request.SentAtUtc;

        var complaint = new ComplaintModel
        {
            Id = complaintId,
            SubmissionDate = submissionDate,
            Fio = request.FromName ?? string.Empty,
            ObjectName = objectName,
            PhoneNumber = null,
            Email = string.IsNullOrWhiteSpace(request.FromEmail) ? null : request.FromEmail,
            SerialNumbers = [],
            DeviceType = null,
            EmotionalTone = null,
            IssueSummary = string.IsNullOrWhiteSpace(request.Subject) ? string.Empty : request.Subject,
            Status = "open",
        };

        await _complaints.CreateAsync(complaint, cancellationToken);

        return JsonSerializer.Serialize(new EmailReceivedRpcResponse { Accepted = true }, _jsonOptions);
    }

    private string SerializeError(string message)
    {
        var error = new EmailReceivedRpcResponse
        {
            Accepted = false,
            Error = message,
        };

        return JsonSerializer.Serialize(error, _jsonOptions);
    }

    private static Guid CreateDeterministicGuid(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        var hash = MD5.HashData(bytes);
        return new Guid(hash);
    }

    private static string ExtractDomain(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return string.Empty;
        }

        var at = email.IndexOf('@', StringComparison.Ordinal);

        if (at < 0 || at + 1 >= email.Length)
        {
            return string.Empty;
        }

        return email.Substring(at + 1).Trim();
    }
}