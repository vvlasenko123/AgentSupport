using System.Data;
using Dapper;
using Infrastructure.Database.PostgreSQL.Migrations.Interfaces;
using Microsoft.Extensions.Logging;

namespace AgentSupport.Infrastructure.Migrations;

/// <summary>
/// Миграция добавления поля status в complaints
/// </summary>
public sealed class ComplaintAddStatusMigration : IDatabaseMigration
{
    private readonly IDbConnection _connection;
    private readonly ILogger<ComplaintAddStatusMigration> _logger;

    public ComplaintAddStatusMigration(
        IDbConnection connection,
        ILogger<ComplaintAddStatusMigration> logger)
    {
        _connection = connection;
        _logger = logger;
    }

    public async Task ApplyAsync(CancellationToken token)
    {
        const string sql = @"
ALTER TABLE complaints
ADD COLUMN IF NOT EXISTS status text NOT NULL DEFAULT 'new';
";

        if (_connection.State is not ConnectionState.Open)
        {
            _connection.Open();
        }

        _logger.LogInformation("Применение миграции: добавление поля status в таблицу complaints");
        await _connection.ExecuteAsync(new CommandDefinition(sql, cancellationToken: token));
    }
}