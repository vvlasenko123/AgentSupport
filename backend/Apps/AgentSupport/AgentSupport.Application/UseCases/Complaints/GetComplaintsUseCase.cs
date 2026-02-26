using AgentSupport.Application.UseCases.Complaints.Dto;
using AgentSupport.Application.UseCases.Complaints.Interfaces;
using AgentSupport.Domain.Models.Complaints;
using Infrastructure.Database.Common.Interfaces;

namespace AgentSupport.Application.UseCases.Complaints;

/// <summary>
/// UseCase получения списка жалоб
/// </summary>
public sealed class GetComplaintsUseCase : IGetComplaintsUseCase
{
    private readonly IRepository<ComplaintModel> _repository;

    public GetComplaintsUseCase(IRepository<ComplaintModel> repository)
    {
        _repository = repository;
    }

    /// <summary>
    /// Получение всех жалоб
    /// </summary>
    public async Task<IReadOnlyCollection<ComplaintDto>> GetAllAsync(CancellationToken cancellationToken)
    {
        var items = await _repository.GetAllAsync(cancellationToken);

        return items
            .Select(x => new ComplaintDto
            {
                Id = x.Id,
                SubmissionDate = x.SubmissionDate,
                Fio = x.Fio,
                ObjectName = x.ObjectName,
                PhoneNumber = x.PhoneNumber,
                Email = x.Email,
                SerialNumbers = x.SerialNumbers,
                DeviceType = x.DeviceType,
                EmotionalTone = x.EmotionalTone,
                IssueSummary = x.IssueSummary,
            }).ToList();
    }
}