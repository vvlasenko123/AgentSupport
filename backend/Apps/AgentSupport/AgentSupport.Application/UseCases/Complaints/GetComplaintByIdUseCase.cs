using AgentSupport.Application.UseCases.Complaints.Dto;
using AgentSupport.Application.UseCases.Complaints.Interfaces;
using AgentSupport.Domain.Models.Complaints;
using Infrastructure.Database.Common.Interfaces;

namespace AgentSupport.Application.UseCases.Complaints;

/// <summary>
/// UseCase получения жалобы по id
/// </summary>
public sealed class GetComplaintByIdUseCase : IGetComplaintByIdUseCase
{
    private readonly IRepository<ComplaintModel> _repository;

    public GetComplaintByIdUseCase(IRepository<ComplaintModel> repository)
    {
        _repository = repository;
    }

    /// <summary>
    /// Получение жалобы одной
    /// </summary>
    public async Task<ComplaintDto> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(id, cancellationToken);

        if (entity is null)
        {
            throw new InvalidOperationException("Жалоба не найдена");
        }

        return new ComplaintDto
        {
            Id = entity.Id,
            SubmissionDate = entity.SubmissionDate,
            Fio = entity.Fio,
            ObjectName = entity.ObjectName,
            PhoneNumber = entity.PhoneNumber,
            Email = entity.Email,
            SerialNumbers = entity.SerialNumbers,
            DeviceType = entity.DeviceType,
            EmotionalTone = entity.EmotionalTone,
            IssueSummary = entity.IssueSummary,
        };
    }
}