using Infrastructure.Broker.Kafka.Rpc.Commandor;
using Infrastructure.Broker.Kafka.Rpc.Handler;
using Infrastructure.Broker.Kafka.Rpc.Interfaces;
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
        services.AddHttpClient<IMlComplaintClient, MlComplaintClient>(c =>
        {
            var baseAddress = Environment.GetEnvironmentVariable("ML_BASE_ADDRESS");

            if (string.IsNullOrWhiteSpace(baseAddress))
            {
                baseAddress = "http://localhost:6767/";
            }

            c.BaseAddress = new Uri(baseAddress);
            c.Timeout = TimeSpan.FromSeconds(15);
        });
    }
}