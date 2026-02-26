using Infrastructure.Hosting.Kestrel;
using Infrastructure.Logging;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

namespace Infrastructure.Hosting.HostConfiguration;

/// <summary>
/// Фабрика хоста
/// </summary>
public static class HostFactory
{
    /// <summary>
    /// Создание хоста
    /// </summary>
    public static IHostBuilder CreateHostBuilder<TStartup>(string[] args) where TStartup : class
    {
        return Host.CreateDefaultBuilder(args)
            .UseInfraSerilog()
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseKestrelOptions();
                webBuilder.UseStartup<TStartup>();
            });
    }
}