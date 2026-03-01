using Infrastructure.Broker.Kafka.Contracts;

namespace Infrastructure.Broker.Kafka.Rpc.Handler;

public interface IMlComplaintClient
{
    Task<MlComplaintResponse?> ProcessAsync(MlProcessRequest request, CancellationToken cancellationToken);
}