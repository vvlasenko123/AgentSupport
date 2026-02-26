using System.Reflection;
using AutoMapper;
using Infrastructure.Mapper.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Mapper;

/// <summary>
/// Настройки AutoMapper
/// </summary>
public static class MapperStartUp
{
    /// <summary>
    /// Добавляет настройки AutoMapper .AddMapperExtension(assemblies: Assembly.GetExecutingAssembly())
    /// </summary>
    public static void AddMapperExtension(this IServiceCollection services, params Assembly[] assemblies)
    {
        // добавляется MapperExtension всегда - не надо волноваться
        var config = new AutoMapperConfiguratonFactory().Create(assemblies);
        config.AssertConfigurationIsValid(); // сразу делаем проверку при старте

        IMapper mapper = new AutoMapper.Mapper(config);
        services.AddSingleton(mapper);
    }
}