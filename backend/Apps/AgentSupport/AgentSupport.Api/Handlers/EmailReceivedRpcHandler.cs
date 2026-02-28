using System.Text.Json;
using Infrastructure.Broker.Kafka.Contract;
using Infrastructure.Broker.Kafka.Contracts;
using Infrastructure.Broker.Kafka.Rpc.Handler.Interfaces;

namespace AgentSupport.Api.Handlers;

public sealed class EmailReceivedRpcHandler : IKafkaRpcHandler
{
    private readonly JsonSerializerOptions _jsonOptions;

    public EmailReceivedRpcHandler()
    {
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };
    }

    public string Contract => KafkaContractName.Get<EmailReceivedRpcRequest>();

    public Task<string> HandleAsync(string payloadJson, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            var error = new EmailReceivedRpcResponse
            {
                Accepted = false,
                Error = "Пустой payload",
            };

            return Task.FromResult(JsonSerializer.Serialize(error, _jsonOptions));
        }

        EmailReceivedRpcRequest? request;

        try
        {
            request = JsonSerializer.Deserialize<EmailReceivedRpcRequest>(payloadJson, _jsonOptions);
        }
        catch
        {
            var error = new EmailReceivedRpcResponse
            {
                Accepted = false,
                Error = "Не удалось разобрать payload",
            };

            return Task.FromResult(JsonSerializer.Serialize(error, _jsonOptions));
        }

        if (request is null)
        {
            var error = new EmailReceivedRpcResponse
            {
                Accepted = false,
                Error = "Payload не распознан",
            };

            return Task.FromResult(JsonSerializer.Serialize(error, _jsonOptions));
        }

        // Тут твоя логика: создать обращение, сохранить письмо и т.д.

        var ok = new EmailReceivedRpcResponse
        {
            Accepted = true,
        };

        return Task.FromResult(JsonSerializer.Serialize(ok, _jsonOptions));
    }
}