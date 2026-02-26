using Microsoft.Extensions.Options;

namespace Infrastructure.Options.Extensions.Validate;

/// <summary>
/// Подключение валидации секции
/// </summary>
/// <remarks>Для валидации должен быть public</remarks>
public sealed class ConnectionOptionsValidator<TOptions> : IValidateOptions<TOptions>
    where TOptions : class
{
    /// <summary>
    /// Валидация секции опций
    /// </summary>
    public ValidateOptionsResult Validate(string? name, TOptions? options)
    {
        if (options is null)
        {
            return ValidateOptionsResult.Fail($"Настройки не найдены для сервиса {typeof(TOptions).Name}");
        }

        return ValidateOptionsResult.Success;
    }
}