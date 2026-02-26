using AgentSupport.Application.UseCases.Complaints.Command;
using AgentSupport.Application.UseCases.Complaints.Dto;

namespace AgentSupport.Application.UseCases.Complaints.Interfaces;

/// <summary>
/// UseCase обновления жалобы
/// </summary>
public interface IUpdateComplaintUseCase
{
    /// <summary>
    /// Обновить жалобу
    /// </summary>
    Task<ComplaintDto> UpdateAsync(UpdateComplaintCommand command, CancellationToken cancellationToken);
}