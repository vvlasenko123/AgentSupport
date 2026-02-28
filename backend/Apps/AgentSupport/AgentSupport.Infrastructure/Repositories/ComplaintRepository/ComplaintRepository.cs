using System.Data;
using AgentSupport.Domain.Models.Complaints;
using Dapper;
using Infrastructure.Database.Common.Interfaces;
using Infrastructure.Database.PostgreSQL.Extensions;

namespace AgentSupport.Infrastructure.Repositories.ComplaintRepository;

/// <summary>
/// Репозиторий жалоб
/// </summary>
public sealed class ComplaintRepository : IRepository<ComplaintModel>
{
    private readonly IDbConnection _connection;

    public ComplaintRepository(IDbConnection connection)
    {
        _connection = connection;
    }

    /// <inheritdoc />
    public async Task<ComplaintModel?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        const string sql = @"
SELECT
    id AS Id,
    submission_date AS SubmissionDate,
    fio AS Fio,
    object_name AS ObjectName,
    phone_number AS PhoneNumber,
    email AS Email,
    serial_numbers AS SerialNumbers,
    device_type AS DeviceType,
    emotional_tone AS EmotionalTone,
    issue_summary AS IssueSummary,
    status AS Status
FROM complaints
WHERE id = @Id;
";

        await _connection.EnsureOpenAsync();

        return await _connection.QueryFirstOrDefaultAsync<ComplaintModel>(new CommandDefinition(
            sql,
            new
            {
                Id = id,
            },
            cancellationToken: cancellationToken));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<ComplaintModel>> GetAllAsync(CancellationToken cancellationToken)
    {
        const string sql = @"
SELECT
    id AS Id,
    submission_date AS SubmissionDate,
    fio AS Fio,
    object_name AS ObjectName,
    phone_number AS PhoneNumber,
    email AS Email,
    serial_numbers AS SerialNumbers,
    device_type AS DeviceType,
    emotional_tone AS EmotionalTone,
    issue_summary AS IssueSummary,
    status AS Status
FROM complaints
ORDER BY submission_date DESC;
";

        await _connection.EnsureOpenAsync();

        var items = await _connection.QueryAsync<ComplaintModel>(new CommandDefinition(
            sql,
            cancellationToken: cancellationToken));

        return items.ToList();
    }

    /// <inheritdoc />
    public async Task<Guid> CreateAsync(ComplaintModel entity, CancellationToken cancellationToken)
    {
        if (entity is null)
        {
            throw new ArgumentNullException(nameof(entity), "Сущность жалобы не может быть null.");
        }

        const string sql = @"
INSERT INTO complaints
(
    id,
    submission_date,
    fio,
    object_name,
    phone_number,
    email,
    serial_numbers,
    device_type,
    emotional_tone,
    issue_summary,
    status
)
VALUES
(
    @Id,
    @SubmissionDate,
    @Fio,
    @ObjectName,
    @PhoneNumber,
    @Email,
    @SerialNumbers,
    @DeviceType,
    @EmotionalTone,
    @IssueSummary,
    @Status
);
";

        await _connection.EnsureOpenAsync();

        await _connection.ExecuteAsync(new CommandDefinition(
            sql,
            new
            {
                entity.Id,
                entity.SubmissionDate,
                entity.Fio,
                entity.ObjectName,
                entity.PhoneNumber,
                entity.Email,
                entity.SerialNumbers,
                entity.DeviceType,
                entity.EmotionalTone,
                entity.IssueSummary,
                entity.Status,
            },
            cancellationToken: cancellationToken));

        return entity.Id;
    }

    /// <inheritdoc />
    public async Task<bool> UpdateAsync(ComplaintModel entity, CancellationToken cancellationToken)
    {
        if (entity is null)
        {
            throw new ArgumentNullException(nameof(entity), "Сущность жалобы не может быть null.");
        }

        const string sql = @"
UPDATE complaints
SET
    submission_date = @SubmissionDate,
    fio = @Fio,
    object_name = @ObjectName,
    phone_number = @PhoneNumber,
    email = @Email,
    serial_numbers = @SerialNumbers,
    device_type = @DeviceType,
    emotional_tone = @EmotionalTone,
    issue_summary = @IssueSummary,
    status = @Status
WHERE id = @Id;
";

        await _connection.EnsureOpenAsync();

        var affected = await _connection.ExecuteAsync(new CommandDefinition(
            sql,
            new
            {
                entity.Id,
                entity.SubmissionDate,
                entity.Fio,
                entity.ObjectName,
                entity.PhoneNumber,
                entity.Email,
                entity.SerialNumbers,
                entity.DeviceType,
                entity.EmotionalTone,
                entity.IssueSummary,
                entity.Status,
            },
            cancellationToken: cancellationToken));

        return affected > 0;
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        const string sql = @"
DELETE FROM complaints
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