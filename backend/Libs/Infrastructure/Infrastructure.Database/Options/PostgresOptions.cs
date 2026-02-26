using Microsoft.Extensions.Options;

namespace Infrastructure.Database.Options;

/// <summary>
/// Опции постгреса
/// </summary>
/// <remarks>Для валидации должен быть public</remarks>
public sealed class PostgresOptions : IValidateOptions<PostgresOptions>
{
    /// <summary>
    /// Флаг автомиграций
    /// </summary>
    /// <remarks>Валидировать в опциях не надо!</remarks>
    public bool AutoMigration { get; init; }

    /// <summary>
    /// Строка подключения из конфигурации
    /// </summary>
    public string? ConnectionString { get; init; }

    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, PostgresOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            return ValidateOptionsResult.Fail("Строка подключения к Postgres не должна быть пустой");
        }

        return ValidateOptionsResult.Success;
    }
}