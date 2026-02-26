using System.Data;

namespace Infrastructure.Database.PostgreSQL.Extensions;

/// <summary>
/// Методы рашсирения
/// </summary>
public static class ConnectionExtension
{
    /// <summary>
    /// Проверка открыто ли соединение
    /// </summary>
    public static Task EnsureOpenAsync(this IDbConnection connection)
    {
        if (connection.State is ConnectionState.Open)
        {
            return Task.CompletedTask;
        }

        connection.Open();

        return Task.CompletedTask;
    }
}