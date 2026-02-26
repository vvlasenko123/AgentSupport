using Infrastructure.Broker.Kafka.Rpc.Commandor;
using Infrastructure.Broker.Kafka.Rpc.Interfaces;
using Infrastructure.Broker.Kafka.Rpc.Services;
using Infrastructure.Broker.Options;
using Infrastructure.Options.Extensions.Validate;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Broker;

/// <summary>
/// подключение кафки
/// </summary>
public static class KafkaStartUp
{
    /// <summary>
    /// Добавление Kafka RPC инфраструктуры (клиент + сервер).
    /// </summary>
    public static void AddKafka(this IServiceCollection services)
    {
        services.AddKafkaClient();
        services.AddKafkaServer();
    }

    /// <summary>
    /// Добавление Kafka RPC клиента (для SendRpc)
    /// </summary>
    public static void AddKafkaClient(this IServiceCollection services)
    {
        services.AddOptions<KafkaOptions>()
            .BindConfiguration(configSectionPath: nameof(KafkaOptions))
            .UseValidationOptions()
            .ValidateOnStart();

        services.AddSingleton<IKafkaBrokerCommandor, KafkaBrokerCommandor>();
    }

    /// <summary>
    /// Добавление Kafka RPC сервера (для обработки входящих запросов)
    /// </summary>
    public static void AddKafkaServer(this IServiceCollection services)
    {
        services.AddHostedService<KafkaRpcServerHostedService>();
    }
}