namespace Infrastructure.Broker.Kafka.Rpc.Interfaces;

/// <summary>
/// Командор Kafka RPC
/// </summary>
public interface IKafkaBrokerCommandor
{
    /// <summary>
    /// Отправляет RPC-запрос в Kafka и ожидает ответ в топике ответа
    /// </summary>
    Task<TResponse> SendRpc<TRequest, TResponse>(TRequest request, CancellationToken cancellationToken);
}