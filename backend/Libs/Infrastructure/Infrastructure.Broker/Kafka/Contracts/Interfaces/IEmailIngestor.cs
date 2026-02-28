namespace Infrastructure.Broker.Kafka.Contracts.Interfaces;

public interface IEmailIngestor
{
    Task IngestAsync(EmailReceivedRpcRequest request, CancellationToken cancellationToken);
}