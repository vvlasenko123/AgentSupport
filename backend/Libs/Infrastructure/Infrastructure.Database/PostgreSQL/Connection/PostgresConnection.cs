using System.Data;
using System.Diagnostics.CodeAnalysis;
using Infrastructure.Database.Options;
using Infrastructure.Database.PostgreSQL.Connection.Interfaces;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Infrastructure.Database.PostgreSQL.Connection;

/// <summary>
/// Постгрес на всякий случай если решим что-то менять внутренне
/// </summary>
public sealed class PostgresConnection : IAsyncDbConnection
{
    /// <summary>
    /// Соединение npgsql
    /// </summary>
    private NpgsqlConnection? _inner;

    /// <summary>
    /// Строка подключения
    /// </summary>
    private readonly string _connectionString;

    /// <inheritdoc />
    [AllowNull]
    public string ConnectionString
    {
        get => _connectionString;
        set => throw new NotSupportedException("Изменение строки подключения запрещено");
    }

    /// <inheritdoc />
    public int ConnectionTimeout => _inner?.ConnectionTimeout ?? 0;

    /// <inheritdoc />
    public string Database => _inner?.Database ?? string.Empty;

    /// <inheritdoc />
    public ConnectionState State => _inner?.State ?? ConnectionState.Closed;

    // <inheritdoc />
    public async Task OpenAsync(CancellationToken cancellationToken)
    {
        EnsureCreated();

        if (_inner!.State is not ConnectionState.Open)
        {
            await _inner.OpenAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Конструктор
    /// </summary>
    public PostgresConnection(IOptions<PostgresOptions> options)
    {
        var connectionString = options.Value.ConnectionString;

        try
        {
            var builder = new NpgsqlConnectionStringBuilder(connectionString);
            _connectionString = builder.ConnectionString;
        }
        catch (Exception ex) when (ex is NpgsqlException or FormatException or ArgumentException)
        {
            throw new ArgumentException("Некорректная строка подключения к Postgres", nameof(options), ex);
        }
    }

    /// <inheritdoc />
    public void Open()
    {
        if (_inner is null)
        {
            _inner = new NpgsqlConnection(_connectionString);
        }

        if (_inner.State is not ConnectionState.Open)
        {
            _inner.Open();
        }
    }

    /// <inheritdoc />
    public void Close()
    {
        if (_inner is not null)
        {
            _inner.Close();
        }
    }

    /// <inheritdoc />
    public IDbCommand CreateCommand()
    {
        if (_inner is null)
        {
            _inner = new NpgsqlConnection(_connectionString);
        }

        return _inner.CreateCommand();
    }

    /// <inheritdoc />
    public IDbTransaction BeginTransaction()
    {
        IsOpen();
        return _inner?.BeginTransaction()!;
    }

    /// <inheritdoc />
    public IDbTransaction BeginTransaction(IsolationLevel il)
    {
        IsOpen();
        return _inner?.BeginTransaction(il)!;
    }

    /// <inheritdoc />
    public void ChangeDatabase(string databaseName)
    {
        if (string.IsNullOrWhiteSpace(databaseName))
        {
            throw new ArgumentException("Имя базы данных не должно быть пустым", nameof(databaseName));
        }

        IsOpen();
        _inner!.ChangeDatabase(databaseName);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_inner is not null)
        {
            _inner.Dispose();
            _inner = null;
        }
    }

    /// <summary>
    /// Проверка на открытость соединения
    /// </summary>
    private void IsOpen()
    {
        if (_inner is null || _inner.State != ConnectionState.Open)
        {
            throw new InvalidOperationException("Соединение закрыто");
        }
    }

    /// <summary>
    /// Проверка на созданное соединение
    /// </summary>
    private void EnsureCreated()
    {
        if (_inner is null)
        {
            _inner = new NpgsqlConnection(_connectionString);
        }
    }
}
