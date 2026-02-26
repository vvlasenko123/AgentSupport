using AgentSupport.Application.UseCases.Complaints.Command;
using AgentSupport.Application.UseCases.Complaints.Dto;
using AgentSupport.Application.UseCases.Complaints.Interfaces;
using AgentSupport.Domain.Models.Complaints;
using Infrastructure.Database.Common.Interfaces;
using UUIDNext;

namespace AgentSupport.Application.UseCases.Complaints;

/// <summary>
/// UseCase создания жалобы
/// </summary>
public sealed class CreateComplaintUseCase : ICreateComplaintUseCase
{
    private readonly IRepository<ComplaintModel> _repository;

    public CreateComplaintUseCase(IRepository<ComplaintModel> repository)
    {
        _repository = repository;
    }

    /// <summary>
    /// Создание жалобы
    /// </summary>
    public async Task<ComplaintDto> CreateAsync(CreateComplaintCommand command, CancellationToken cancellationToken)
    {
        var entity = new ComplaintModel
        {
            Id = Uuid.NewSequential(),
            SubmissionDate = command.SubmissionDate ?? DateTime.UtcNow,
            Fio = command.Fio,
            ObjectName = command.ObjectName,
            PhoneNumber = command.PhoneNumber,
            Email = command.Email,
            SerialNumbers = command.SerialNumbers,
            DeviceType = command.DeviceType,
            EmotionalTone = command.EmotionalTone,
            IssueSummary = command.IssueSummary,
        };

        await _repository.CreateAsync(entity, cancellationToken);

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