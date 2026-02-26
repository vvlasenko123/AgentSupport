using System.Data;
using Infrastructure.Database.Options;
using Infrastructure.Database.PostgreSQL.Connection;
using Infrastructure.Database.PostgreSQL.Services;
using Infrastructure.Options.Extensions.Validate;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Database;

/// <summary>
/// Добавелние настроек базы данных
/// </summary>
public static class DatabaseStartUp
{
    /// <summary>
    /// Добавление Postgres
    /// </summary>
    public static void AddPostgres(this IServiceCollection services)
    {
        services.AddOptions<PostgresOptions>()
            .BindConfiguration(configSectionPath: nameof(PostgresOptions))
            .UseValidationOptions()
            .ValidateOnStart();

        services.AddTransient<IDbConnection, PostgresConnection>();
        services.AddHostedService<PostgresMigrationHostedService>();
    }
}