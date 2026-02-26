using System.Data;

namespace Infrastructure.Database.PostgreSQL.Connection.Interfaces;

/// <summary>
/// Асинхронное открытие соединения
/// </summary>
public interface IAsyncDbConnection : IDbConnection
{
    Task OpenAsync(CancellationToken cancellationToken);
}