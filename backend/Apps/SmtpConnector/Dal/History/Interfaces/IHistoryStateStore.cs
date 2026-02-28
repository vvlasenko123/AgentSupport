namespace SmtpConnector.Dal.History.Interfaces;

public interface IHistoryStateStore
{
    Task<string?> GetLastHistoryIdAsync(CancellationToken cancellationToken);
    Task SaveLastHistoryIdAsync(string? historyId, CancellationToken cancellationToken);
}