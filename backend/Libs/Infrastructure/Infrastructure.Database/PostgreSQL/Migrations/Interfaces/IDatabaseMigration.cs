namespace Infrastructure.Database.PostgreSQL.Migrations.Interfaces;

/// <summary>
/// Контракт миграций, сделаем свое, fluent мне не нравится
/// </summary>
public interface IDatabaseMigration
{
    /// <summary>
    /// Применение миграций
    /// </summary>
    Task ApplyAsync(CancellationToken token);
}