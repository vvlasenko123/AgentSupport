using System.Data;
using Dapper;
using Infrastructure.Database.PostgreSQL.Migrations.Interfaces;
using Microsoft.Extensions.Logging;

namespace AgentSupport.Infrastructure.Migrations;

/// <summary>
/// Миграция создания таблицы complaints
/// </summary>
public sealed class SubscribersCreateTableMigration : IDatabaseMigration
{
    private readonly IDbConnection _connection;
    private readonly ILogger<SubscribersCreateTableMigration> _logger;

    public SubscribersCreateTableMigration(
        IDbConnection connection,
        ILogger<SubscribersCreateTableMigration> logger)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task ApplyAsync(CancellationToken token)
    {
        const string sql = @"
CREATE TABLE IF NOT EXISTS subscribers
(
    chat_id BIGINT PRIMARY KEY,
    username TEXT NULL,
    first_name TEXT NULL,
    last_seen TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    is_active BOOLEAN DEFAULT TRUE
);
";

        if (_connection.State is not ConnectionState.Open)
        {
            _connection.Open();
        }

        _logger.LogInformation("Применение миграции: создание таблицы subscribers");
        await _connection.ExecuteAsync(new CommandDefinition(sql, cancellationToken: token));
    }
}