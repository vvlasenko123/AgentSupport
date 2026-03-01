using System.Data;
using Dapper;
using Infrastructure.Database.PostgreSQL.Migrations.Interfaces;
using Microsoft.Extensions.Logging;

namespace SmtpConnector.Dal.Migrations;

/// <summary>
/// Миграция создания таблицы gmail_history_state
/// </summary>
public sealed class GmailHistoryStateCreateTableMigration : IDatabaseMigration
{
    private readonly IDbConnection _connection;
    private readonly ILogger<GmailHistoryStateCreateTableMigration> _logger;

    public GmailHistoryStateCreateTableMigration(
        IDbConnection connection,
        ILogger<GmailHistoryStateCreateTableMigration> logger)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task ApplyAsync(CancellationToken token)
    {
        const string sql = @"
CREATE TABLE IF NOT EXISTS public.gmail_history_state
(
    id int NOT NULL PRIMARY KEY,
    last_history_id text NOT NULL,
    updated_at timestamp with time zone NOT NULL
);
";

        if (_connection.State is not ConnectionState.Open)
        {
            _connection.Open();
        }

        _logger.LogInformation("Применение миграции: создание таблицы gmail_history_state");
        await _connection.ExecuteAsync(new CommandDefinition(sql, cancellationToken: token));
    }
}