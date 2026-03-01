using Infrastructure.Database.Common.Models;

namespace Infrastructure.Database.Common.Interfaces;

public interface IEmailMessageRepository : IRepository<EmailMessageModel>
{
    Task<IReadOnlyCollection<EmailMessageModel>> GetByComplaintIdAsync(Guid complaintId, CancellationToken cancellationToken);
    Task<EmailMessageModel?> GetLastIncomingByComplaintIdAsync(Guid complaintId, CancellationToken cancellationToken);
}