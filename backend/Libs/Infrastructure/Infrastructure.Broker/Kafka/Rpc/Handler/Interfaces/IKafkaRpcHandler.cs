namespace Infrastructure.Broker.Kafka.Rpc.Handler.Interfaces;

//todo будем через этот контракт обрабратывать письма
/// <summary>
/// Обработчик RPC-запросов для конкретного контракта (request topic) 
/// </summary>
public interface IKafkaRpcHandler
{
    /// <summary>
    /// Контракт запроса (имя request-модели). Топик запроса равен этому значению
    /// </summary>
    string Contract { get; }

    /// <summary>
    /// Обрабатывает payload запроса и возвращает JSON payload ответа
    /// </summary>
    Task<string> HandleAsync(string payloadJson, CancellationToken cancellationToken);
}