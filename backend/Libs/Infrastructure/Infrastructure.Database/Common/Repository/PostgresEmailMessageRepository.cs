using System.Data;
using Dapper;
using Infrastructure.Database.Common.Interfaces;
using Infrastructure.Database.Common.Models;
using Infrastructure.Database.PostgreSQL.Extensions;

namespace Infrastructure.Database.Common.Repository;

public sealed class PostgresEmailMessageRepository : IEmailMessageRepository
{
    private readonly IDbConnection _connection;

    public PostgresEmailMessageRepository(IDbConnection connection)
    {
        _connection = connection;
    }

    public async Task<EmailMessageModel?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        const string sql = @"
SELECT
    id,
    complaint_id AS ComplaintId,
    direction AS Direction,
    external_message_id AS ExternalMessageId,
    from_email AS FromEmail,
    to_email AS ToEmail,
    subject AS Subject,
    content AS Content,
    thread_id AS ThreadId,
    sent_at_utc AS SentAtUtc,
    created_at_utc AS CreatedAtUtc
FROM gmail_messages
WHERE id = @Id;
";

        await _connection.EnsureOpenAsync();

        return await _connection.QueryFirstOrDefaultAsync<EmailMessageModel>(new CommandDefinition(
            sql,
            new
            {
                Id = id,
            },
            cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyCollection<EmailMessageModel>> GetAllAsync(CancellationToken cancellationToken)
    {
        const string sql = @"
SELECT
    id,
    complaint_id AS ComplaintId,
    direction AS Direction,
    external_message_id AS ExternalMessageId,
    from_email AS FromEmail,
    to_email AS ToEmail,
    subject AS Subject,
    content AS Content,
    thread_id AS ThreadId,
    sent_at_utc AS SentAtUtc,
    created_at_utc AS CreatedAtUtc
FROM gmail_messages
ORDER BY created_at_utc DESC;
";

        await _connection.EnsureOpenAsync();

        var rows = await _connection.QueryAsync<EmailMessageModel>(new CommandDefinition(
            sql,
            cancellationToken: cancellationToken));

        return rows.AsList();
    }

    public async Task<IReadOnlyCollection<EmailMessageModel>> GetByComplaintIdAsync(Guid complaintId, CancellationToken cancellationToken)
    {
        const string sql = @"
SELECT
    id,
    complaint_id AS ComplaintId,
    direction AS Direction,
    external_message_id AS ExternalMessageId,
    from_email AS FromEmail,
    to_email AS ToEmail,
    subject AS Subject,
    content AS Content,
    thread_id AS ThreadId,
    sent_at_utc AS SentAtUtc,
    created_at_utc AS CreatedAtUtc
FROM gmail_messages
WHERE complaint_id = @ComplaintId
ORDER BY created_at_utc ASC;
";

        await _connection.EnsureOpenAsync();

        var rows = await _connection.QueryAsync<EmailMessageModel>(new CommandDefinition(
            sql,
            new
            {
                ComplaintId = complaintId,
            },
            cancellationToken: cancellationToken));

        return rows.AsList();
    }

    public async Task<EmailMessageModel?> GetLastIncomingByComplaintIdAsync(Guid complaintId, CancellationToken cancellationToken)
    {
        const string sql = @"
SELECT
    id,
    complaint_id AS ComplaintId,
    direction AS Direction,
    external_message_id AS ExternalMessageId,
    from_email AS FromEmail,
    to_email AS ToEmail,
    subject AS Subject,
    content AS Content,
    thread_id AS ThreadId,
    sent_at_utc AS SentAtUtc,
    created_at_utc AS CreatedAtUtc
FROM gmail_messages
WHERE complaint_id = @ComplaintId
  AND direction = @Direction
ORDER BY created_at_utc DESC
LIMIT 1;
";

        await _connection.EnsureOpenAsync();

        return await _connection.QueryFirstOrDefaultAsync<EmailMessageModel>(new CommandDefinition(
            sql,
            new
            {
                ComplaintId = complaintId,
                Direction = (int)EmailMessageDirection.Incoming,
            },
            cancellationToken: cancellationToken));
    }

    public async Task<Guid> CreateAsync(EmailMessageModel entity, CancellationToken cancellationToken)
    {
        if (entity.Id == Guid.Empty)
        {
            throw new InvalidOperationException("Некорректный Id сообщения");
        }

        if (entity.ComplaintId == Guid.Empty)
        {
            throw new InvalidOperationException("Некорректный ComplaintId сообщения");
        }

        if (string.IsNullOrWhiteSpace(entity.ExternalMessageId))
        {
            throw new InvalidOperationException("Не задан ExternalMessageId");
        }

        const string sql = @"
INSERT INTO gmail_messages
(
    id,
    complaint_id,
    direction,
    external_message_id,
    from_email,
    to_email,
    subject,
    content,
    thread_id,
    sent_at_utc,
    created_at_utc
)
VALUES
(
    @Id,
    @ComplaintId,
    @Direction,
    @ExternalMessageId,
    @FromEmail,
    @ToEmail,
    @Subject,
    @Content,
    @ThreadId,
    @SentAtUtc,
    @CreatedAtUtc
)
ON CONFLICT (id) DO NOTHING;
";

        await _connection.EnsureOpenAsync();

        await _connection.ExecuteAsync(new CommandDefinition(
            sql,
            new
            {
                entity.Id,
                entity.ComplaintId,
                Direction = (int)entity.Direction,
                entity.ExternalMessageId,
                entity.FromEmail,
                entity.ToEmail,
                entity.Subject,
                entity.Content,
                entity.ThreadId,
                entity.SentAtUtc,
                entity.CreatedAtUtc,
            },
            cancellationToken: cancellationToken));

        return entity.Id;
    }

    public async Task<bool> UpdateAsync(EmailMessageModel entity, CancellationToken cancellationToken)
    {
        const string sql = @"
UPDATE gmail_messages
SET
    from_email = @FromEmail,
    to_email = @ToEmail,
    subject = @Subject,
    content = @Content,
    thread_id = @ThreadId,
    sent_at_utc = @SentAtUtc
WHERE id = @Id;
";

        await _connection.EnsureOpenAsync();

        var affected = await _connection.ExecuteAsync(new CommandDefinition(
            sql,
            new
            {
                entity.Id,
                entity.FromEmail,
                entity.ToEmail,
                entity.Subject,
                entity.Content,
                entity.ThreadId,
                entity.SentAtUtc,
            },
            cancellationToken: cancellationToken));

        return affected > 0;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        const string sql = @"
DELETE FROM gmail_messages
WHERE id = @Id;
";

        await _connection.EnsureOpenAsync();

        var affected = await _connection.ExecuteAsync(new CommandDefinition(
            sql,
            new
            {
                Id = id,
            },
            cancellationToken: cancellationToken));

        return affected > 0;
    }
}