namespace AgentSupport.Application.UseCases.Complaints.Interfaces;

/// <summary>
/// UseCase удаления жалобы
/// </summary>
public interface IDeleteComplaintUseCase
{
    /// <summary>
    /// Удалить жалобу
    /// </summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}