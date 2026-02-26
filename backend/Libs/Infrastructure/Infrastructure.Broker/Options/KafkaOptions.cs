using Microsoft.Extensions.Options;

namespace Infrastructure.Broker.Options;

/// <summary>
/// Опции Kafka
/// </summary>
/// <remarks>Для валидации должен быть public</remarks>
public sealed class KafkaOptions : IValidateOptions<KafkaOptions>
{
    /// <summary>
    /// Адрес Kafka (bootstrap servers)
    /// </summary>
    public string? BootstrapServers { get; init; }

    /// <summary>
    /// Таймаут ожидания RPC-ответа в секундах
    /// </summary>
    public int RpcTimeoutSeconds { get; init; } = 30;

    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, KafkaOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.BootstrapServers))
        {
            return ValidateOptionsResult.Fail("BootstrapServers для Kafka не должен быть пустой");
        }

        if (options.RpcTimeoutSeconds <= 0)
        {
            return ValidateOptionsResult.Fail("RpcTimeoutSeconds должен быть больше нуля");
        }

        return ValidateOptionsResult.Success;
    }
}