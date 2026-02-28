namespace Infrastructure.Broker.Kafka.Contract;

/// <summary>
/// Контракт по имени
/// </summary>
public static class KafkaContractName
{
    /// <summary>
    /// Получение имение контракта request/response
    /// </summary>
    public static string Get<T>()
    {
        var name = typeof(T).Name;

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidOperationException("Не удалось определить имя контракта");
        }

        return name;
    }
}