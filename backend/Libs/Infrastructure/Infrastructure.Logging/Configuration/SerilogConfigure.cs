using Microsoft.Extensions.Hosting;
using Serilog;

namespace Infrastructure.Logging.Configuration;

/// <summary>
/// Extension для конфигурации логгера
/// </summary>
public static class SerilogConfigure
{
    /// <summary>
    /// Сконфигурировать логгер
    /// </summary>
    public static void Configure(this LoggerConfiguration loggerConfiguration, HostBuilderContext context, IServiceProvider provider)
    {
        loggerConfiguration.ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(provider)
            .Enrich.FromLogContext()
            .WriteTo.Console();
    }
}