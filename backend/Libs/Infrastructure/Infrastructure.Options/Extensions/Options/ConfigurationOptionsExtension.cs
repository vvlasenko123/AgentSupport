using Infrastructure.Options.Extensions.Validate;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Infrastructure.Options.Extensions.Options;

/// <summary>
/// Extension для конфигурирования в других проектах
/// </summary>
public static class ConfigurationOptionsExtension
{
    /// <summary>
    /// Сбиндить валидацию конфигурации
    /// </summary>
    public static void BindConfigurationOptions<TOptions>(this OptionsBuilder<TOptions> optionsBuilder)
        where TOptions : class
    {
        optionsBuilder.BindConfiguration(configSectionPath: nameof(TOptions))
            .UseValidationOptions()
            .ValidateOnStart();
    }
}