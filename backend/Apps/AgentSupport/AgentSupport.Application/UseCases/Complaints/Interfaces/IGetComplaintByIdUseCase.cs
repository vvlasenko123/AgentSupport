using AgentSupport.Application.UseCases.Complaints.Dto;

namespace AgentSupport.Application.UseCases.Complaints.Interfaces;

/// <summary>
/// UseCase получения жалобы по id
/// </summary>
public interface IGetComplaintByIdUseCase
{
    /// <summary>
    /// Получить жалобу
    /// </summary>
    Task<ComplaintDto> GetByIdAsync(Guid id, CancellationToken cancellationToken);
}