using AgentSupport.Application.UseCases.Complaints.Command;
using AgentSupport.Application.UseCases.Complaints.Dto;

namespace AgentSupport.Application.UseCases.Complaints.Interfaces;

/// <summary>
/// UseCase создания жалобы
/// </summary>
public interface ICreateComplaintUseCase
{
    /// <summary>
    /// Создать жалобу
    /// </summary>
    Task<ComplaintDto> CreateAsync(CreateComplaintCommand command, CancellationToken cancellationToken);
}