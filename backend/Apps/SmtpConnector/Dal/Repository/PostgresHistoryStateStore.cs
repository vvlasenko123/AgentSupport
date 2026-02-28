using System.Data;
using Dapper;
using Infrastructure.Database.PostgreSQL.Extensions;
using SmtpConnector.Dal.History.Interfaces;

namespace SmtpConnector.Dal.Repository;

public sealed class PostgresHistoryStateStore : IHistoryStateStore
{
    private const int HistoryStateId = 1;
    private readonly IDbConnection _connection;

    public PostgresHistoryStateStore(IDbConnection connection)
    {
        _connection = connection;
    }

    public async Task<string?> GetLastHistoryIdAsync(CancellationToken cancellationToken)
    {
        const string sql = @"
SELECT last_history_id
FROM gmail_history_state
WHERE id = @Id;
";

        await _connection.EnsureOpenAsync();

        return await _connection.QueryFirstOrDefaultAsync<string?>(new CommandDefinition(
            sql,
            new
            {
                Id = HistoryStateId,
            },
            cancellationToken: cancellationToken));
    }

    public async Task SaveLastHistoryIdAsync(string? historyId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(historyId))
        {
            throw new InvalidOperationException("Некорректный historyId");
        }

        const string sql = @"
INSERT INTO gmail_history_state (id, last_history_id, updated_at)
VALUES (@Id, @HistoryId, NOW())
ON CONFLICT (id) DO UPDATE
SET
    last_history_id = EXCLUDED.last_history_id,
    updated_at = NOW();
";

        await _connection.EnsureOpenAsync();

        await _connection.ExecuteAsync(new CommandDefinition(
            sql,
            new
            {
                Id = HistoryStateId,
                HistoryId = historyId,
            },
            cancellationToken: cancellationToken));
    }
}