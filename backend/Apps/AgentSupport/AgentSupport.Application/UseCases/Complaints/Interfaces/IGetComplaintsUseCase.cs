using AgentSupport.Application.UseCases.Complaints.Dto;

namespace AgentSupport.Application.UseCases.Complaints.Interfaces;

/// <summary>
/// UseCase получения списка жалоб
/// </summary>
public interface IGetComplaintsUseCase
{
    /// <summary>
    /// Получить жалобы
    /// </summary>
    Task<IReadOnlyCollection<ComplaintDto>> GetAllAsync(CancellationToken cancellationToken);
}