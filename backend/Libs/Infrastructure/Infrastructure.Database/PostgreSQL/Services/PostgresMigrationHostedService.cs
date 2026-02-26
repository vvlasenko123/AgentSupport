using Infrastructure.Database.Options;
using Infrastructure.Database.PostgreSQL.Migrations.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Database.PostgreSQL.Services;

/// <summary>
/// Фоновый сервис применения миграций Postgres, вместо fluent будем определять по контракту
/// </summary>
public sealed class PostgresMigrationHostedService : IHostedService
{
    /// <summary>
    /// Провайдер
    /// </summary>
    private readonly IServiceProvider _serviceProvider;
    
    /// <summary>
    /// опции
    /// </summary>
    private readonly IOptions<PostgresOptions> _options;
    
    /// <summary>
    /// логгер
    /// </summary>
    private readonly ILogger<PostgresMigrationHostedService> _logger;

    public PostgresMigrationHostedService(
        IServiceProvider serviceProvider,
        IOptions<PostgresOptions> options,
        ILogger<PostgresMigrationHostedService> logger)
    {
        _serviceProvider = serviceProvider;
        _options = options;
        _logger = logger;
    }

    /// <summary>
    /// старт миграций
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (_options.Value.AutoMigration is false)
        {
            _logger.LogInformation("Автоматическое применение миграций отключено");
            return;
        }

        _logger.LogInformation("Начато применение миграций Postgres");

        using (var scope = _serviceProvider.CreateScope())
        {
            var migrations = 
                scope.ServiceProvider.GetServices<IDatabaseMigration>().ToList();

            if (!migrations.Any())
            {
                _logger.LogInformation("Миграции Postgres не найдены");
                return;
            }

            foreach (var migration in migrations)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                await migration.ApplyAsync(cancellationToken);
            }
        }

        _logger.LogInformation("Применение миграций завершено");
    }

    /// <summary>
    /// стоп миграциям
    /// </summary>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
