using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;

namespace Infrastructure.Hosting.Kestrel;

/// <summary>
/// Настройки конфигурирования Kestrel
/// </summary>
public static class KestrelOptionsExtension
{
    /// <summary>
    /// Настраивает опции Kestrel server
    /// </summary>
    public static void UseKestrelOptions(this IWebHostBuilder webHostBuilder)
    {
        webHostBuilder.UseKestrel(options =>
        {
            // Не раскрываем информацию о сервере в ответах
            options.AddServerHeader = false;

            // Для внешнего контура отключаем динамическое сжатие заголовков, чтобы снизить риск компрессионных атак
            options.AllowResponseHeaderCompression = false;

            ApplyLimitOptions(options.Limits);
        });
    }

    /// <summary>
    /// Устанавливает ограничение лимитов
    /// todo для ограничений нужно делать замеры, пока оставляем так
    /// </summary>
    private static void ApplyLimitOptions(KestrelServerLimits options)
    {
        // Ограничиваем размер тела запроса. Для файлов использовать Minio, в API передавать только метаданные
        options.MaxRequestBodySize = 1 * 1024 * 1024;

        // Ограничиваем общий размер заголовков, чтобы снизить риск запросов с мусорными/слишком большими заголовками
        options.MaxRequestHeadersTotalSize = 16 * 1024;

        // Ограничиваем количество заголовков, чтобы снизить риск мусорных запросов
        options.MaxRequestHeaderCount = 60;

        // Снижаем время на получение заголовков, чтобы защититься от медленных клиентов
        options.RequestHeadersTimeout = TimeSpan.FromSeconds(10);

        // Умеренный keep-alive, чтобы не держать соединения слишком долго и не раздувать число сокетов
        options.KeepAliveTimeout = TimeSpan.FromSeconds(90);
    }
}