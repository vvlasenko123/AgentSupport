using Infrastructure.Broker.Kafka.Contracts;
using Infrastructure.Broker.Kafka.Contracts.Interfaces;
using Infrastructure.Broker.Kafka.Rpc.Interfaces;

namespace SmtpConnector.Logic.Services;

public sealed class EmailIngestor : IEmailIngestor
{
    private readonly IKafkaBrokerCommandor _broker;

    public EmailIngestor(IKafkaBrokerCommandor broker)
    {
        _broker = broker;
    }

    public async Task IngestAsync(EmailReceivedRpcRequest request, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request), "Запрос не может быть null");
        }

        var response = await _broker.SendRpc<EmailReceivedRpcRequest, EmailReceivedRpcResponse>(request, cancellationToken);

        if (response.Accepted is false)
        {
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(response.Error)
                ? "AgentSupport отклонил письмо"
                : response.Error);
        }
    }
}