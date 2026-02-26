using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Infrastructure.Options.Extensions.Validate;

/// <summary>
/// Конфигурация валидации опций
/// </summary>
/// <remarks>Используется в других проектах - public</remarks>
public static class ConfigureValidationOptionsExtension
{
    /// <summary>
    /// Подключение валидации опций
    /// </summary>
    public static OptionsBuilder<TOptions> UseValidationOptions<TOptions>(this OptionsBuilder<TOptions> builder)
        where TOptions : class
    {
        builder.Services.AddSingleton<IValidateOptions<TOptions>, ConnectionOptionsValidator<TOptions>>();

        if (typeof(IValidateOptions<TOptions>).IsAssignableFrom(typeof(TOptions)))
        {
            builder.Services.AddSingleton(typeof(IValidateOptions<TOptions>), typeof(TOptions));
        }

        return builder;
    }
}