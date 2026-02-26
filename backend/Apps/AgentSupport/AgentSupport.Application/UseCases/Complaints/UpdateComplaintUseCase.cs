using AgentSupport.Application.UseCases.Complaints.Command;
using AgentSupport.Application.UseCases.Complaints.Dto;
using AgentSupport.Application.UseCases.Complaints.Interfaces;
using AgentSupport.Domain.Models.Complaints;
using Infrastructure.Database.Common.Interfaces;

namespace AgentSupport.Application.UseCases.Complaints;

/// <summary>
/// UseCase обновления жалобы
/// </summary>
public sealed class UpdateComplaintUseCase : IUpdateComplaintUseCase
{
    private readonly IRepository<ComplaintModel> _repository;

    public UpdateComplaintUseCase(IRepository<ComplaintModel> repository)
    {
        _repository = repository;
    }

    /// <summary>
    /// Обновление жалобы
    /// </summary>
    public async Task<ComplaintDto> UpdateAsync(UpdateComplaintCommand command, CancellationToken cancellationToken)
    {
        var existing = await _repository.GetByIdAsync(command.Id, cancellationToken);

        if (existing is null)
        {
            throw new InvalidOperationException("Жалоба не найдена");
        }

        existing.SubmissionDate = command.SubmissionDate;
        existing.Fio = command.Fio;
        existing.ObjectName = command.ObjectName;
        existing.PhoneNumber = command.PhoneNumber;
        existing.Email = command.Email;
        existing.SerialNumbers = command.SerialNumbers;
        existing.DeviceType = command.DeviceType;
        existing.EmotionalTone = command.EmotionalTone;
        existing.IssueSummary = command.IssueSummary;

        var updated = await _repository.UpdateAsync(existing, cancellationToken);

        if (updated is false)
        {
            throw new InvalidOperationException("Не удалось обновить жалобу");
        }

        return new ComplaintDto
        {
            Id = existing.Id,
            SubmissionDate = existing.SubmissionDate,
            Fio = existing.Fio,
            ObjectName = existing.ObjectName,
            PhoneNumber = existing.PhoneNumber,
            Email = existing.Email,
            SerialNumbers = existing.SerialNumbers,
            DeviceType = existing.DeviceType,
            EmotionalTone = existing.EmotionalTone,
            IssueSummary = existing.IssueSummary,
        };
    }
}