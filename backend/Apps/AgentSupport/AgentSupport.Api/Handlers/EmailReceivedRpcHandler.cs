using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AgentSupport.Domain.Models.Complaints;
using Infrastructure.Broker.Kafka.Contract;
using Infrastructure.Broker.Kafka.Contracts;
using Infrastructure.Broker.Kafka.Rpc.Handler;
using Infrastructure.Broker.Kafka.Rpc.Handler.Interfaces;
using Infrastructure.Database.Common.Interfaces;

namespace AgentSupport.Api.Handlers;

public sealed class EmailReceivedRpcHandler : IKafkaRpcHandler
{
    private readonly ILogger<EmailReceivedRpcHandler> _logger;
    private readonly IRepository<ComplaintModel> _complaints;
    private readonly IMlComplaintClient _mlClient;
    private readonly JsonSerializerOptions _jsonOptions;

    public EmailReceivedRpcHandler(
        ILogger<EmailReceivedRpcHandler> logger,
        IRepository<ComplaintModel> complaints,
        IMlComplaintClient mlClient)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _complaints = complaints ?? throw new ArgumentNullException(nameof(complaints));
        _mlClient = mlClient ?? throw new ArgumentNullException(nameof(mlClient));

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
            "Получено письмо. MessageId={MessageId}, FromName={FromName}, FromEmail={FromEmail}, Subject={Subject}, SentAtUtc={SentAtUtc}, ContentLength={ContentLength}",
            request.MessageId,
            request.FromName ?? string.Empty,
            request.FromEmail ?? string.Empty,
            request.Subject ?? string.Empty,
            request.SentAtUtc,
            request.Content?.Length ?? 0);

        var complaintId = CreateDeterministicGuid(request.MessageId);

        var existing = await _complaints.GetByIdAsync(complaintId, cancellationToken);

        if (existing is not null)
        {
            return JsonSerializer.Serialize(new EmailReceivedRpcResponse { Accepted = true }, _jsonOptions);
        }

        var submissionDate = request.SentAtUtc == default
            ? DateTime.UtcNow
            : request.SentAtUtc;

        var objectName = ExtractDomain(request.FromEmail);

        var mlRequest = new MlProcessRequest
        {
            Id = request.MessageId,
            ComplaintId = complaintId.ToString(),
            Direction = "Incoming",
            ExternalMessageId = request.MessageId,
            FromEmail = request.FromEmail,
            FromName = request.FromName,
            ToEmail = null,
            Subject = request.Subject,
            Content = request.Content,
            ThreadId = null,
            SentAtUtc = submissionDate,
            CreatedAtUtc = DateTime.UtcNow,
        };

        MlComplaintResponse? mlResponse = null;

        try
        {
            mlResponse = await _mlClient.ProcessAsync(mlRequest, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ML сервис недоступен или вернул ошибку. Сохраню жалобу без обогащения");
        }

        var complaint = new ComplaintModel
        {
            Id = complaintId,
            SubmissionDate = mlResponse?.SubmissionDate ?? submissionDate,
            Fio = mlResponse?.Fio ?? (request.FromName ?? string.Empty),
            ObjectName = mlResponse?.ObjectName ?? objectName,
            PhoneNumber = mlResponse?.PhoneNumber,
            Email = string.IsNullOrWhiteSpace(mlResponse?.Email) ? request.FromEmail : mlResponse?.Email,
            SerialNumbers = mlResponse?.SerialNumbers ?? Array.Empty<string>(),
            DeviceType = mlResponse?.DeviceType,
            EmotionalTone = mlResponse?.EmotionalTone,
            IssueSummary = mlResponse?.IssueSummary ?? (string.IsNullOrWhiteSpace(request.Subject) ? string.Empty : request.Subject),
            Status = mlResponse?.Status ?? "open",
            Category = mlResponse?.Category,
            SuggestedAnswer = mlResponse?.SuggestedAnswer,
            Content = request.Content,
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