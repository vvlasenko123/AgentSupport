namespace Infrastructure.Database.Common.Interfaces;

/// <summary>
/// Базовый репозиторий
/// </summary>
public interface IRepository<T>
{
    /// <summary>
    /// Получение сущности по идентификатору
    /// </summary>
    Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Получение списка сущностей
    /// </summary>
    Task<IReadOnlyCollection<T>> GetAllAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Добавление сущности
    /// </summary>
    Task<Guid> CreateAsync(T entity, CancellationToken cancellationToken);

    /// <summary>
    /// Обновление сущности
    /// </summary>
    Task<bool> UpdateAsync(T entity, CancellationToken cancellationToken);

    /// <summary>
    /// Удаление сущности
    /// </summary>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken);
}