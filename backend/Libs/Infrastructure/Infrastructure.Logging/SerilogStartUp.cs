using Infrastructure.Logging.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace Infrastructure.Logging;

/// <summary>
/// Добавелние настроек Serilog
/// </summary>
public static class SerilogStartUp
{
    /// <summary>
    /// Поключение Serilog
    /// </summary>
    public static IHostBuilder UseInfraSerilog(this IHostBuilder builder)
    {
        // bootstrap для раннего лога ошибок, потом используем финальный логгер
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .CreateBootstrapLogger();

        builder.UseSerilog(configureLogger: (context, provider, config) =>
        {
            // финальный логгер
            // todo добавить трейсы запросов, чтобы могли различать
            // var traceId = Activity.Current?.TraceId.ToString();
            // var spanId = Activity.Current?.SpanId.ToString();
            config.Configure(context, provider);
        });
        
        return builder;
    }
}
