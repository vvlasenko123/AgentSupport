using AgentSupport.Application.UseCases.Complaints.Interfaces;
using AgentSupport.Domain.Models.Complaints;
using Infrastructure.Database.Common.Interfaces;

namespace AgentSupport.Application.UseCases.Complaints;

/// <summary>
/// UseCase удаления жалобы
/// </summary>
public sealed class DeleteComplaintUseCase : IDeleteComplaintUseCase
{
    private readonly IRepository<ComplaintModel> _repository;

    public DeleteComplaintUseCase(IRepository<ComplaintModel> repository)
    {
        _repository = repository;
    }

    /// <summary>
    /// Удаление жалобы
    /// </summary>
    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await _repository.DeleteAsync(id, cancellationToken);

        if (deleted is false)
        {
            throw new InvalidOperationException("Жалоба не найдена");
        }
    }
}