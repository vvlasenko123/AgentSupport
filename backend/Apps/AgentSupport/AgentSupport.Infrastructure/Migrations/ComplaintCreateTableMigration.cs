using System.Data;
using Dapper;
using Infrastructure.Database.PostgreSQL.Migrations.Interfaces;
using Microsoft.Extensions.Logging;

namespace AgentSupport.Infrastructure.Migrations;

/// <summary>
/// Миграция создания таблицы complaints
/// </summary>
public sealed class ComplaintCreateTableMigration : IDatabaseMigration
{
    private readonly IDbConnection _connection;
    private readonly ILogger<ComplaintCreateTableMigration> _logger;

    public ComplaintCreateTableMigration(
        IDbConnection connection,
        ILogger<ComplaintCreateTableMigration> logger)
    {
        _connection = connection;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task ApplyAsync(CancellationToken token)
    {
        const string sql = @"
CREATE TABLE IF NOT EXISTS complaints
(
    id uuid NOT NULL PRIMARY KEY,
    submission_date timestamp with time zone NOT NULL,
    fio text NOT NULL,
    object_name text NOT NULL,
    phone_number text NULL,
    email text NULL,
    serial_numbers text[] NOT NULL DEFAULT ARRAY[]::text[],
    device_type text NULL,
    emotional_tone text NULL,
    issue_summary text NOT NULL
);
";

        if (_connection.State is not ConnectionState.Open)
        {
            _connection.Open();
        }

        _logger.LogInformation("Применение миграции: создание таблицы complaints");
        await _connection.ExecuteAsync(new CommandDefinition(sql, cancellationToken: token));
    }
}