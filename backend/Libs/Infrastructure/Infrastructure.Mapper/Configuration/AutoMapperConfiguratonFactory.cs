using System.Reflection;
using AutoMapper;

namespace Infrastructure.Mapper.Configuration;

/// <summary>
/// Фабрика получения конфигурации AutoMapper
/// </summary>
public sealed class AutoMapperConfiguratonFactory
{
    /// <summary>
    /// Созданить конфигурации для автомаппера
    /// </summary>
    public MapperConfiguration Create(params Assembly[] assemblies)
    {
        var profileInheritors = new List<Type>();

        // узнаем детей один раз! при старте - не страшно
        foreach (var assembly in assemblies)
        {
            foreach (var type in assembly.GetTypes())
            {
                if (IsProfileType(type))
                {
                    profileInheritors.Add(type);
                }
            }
        }

        if (profileInheritors.Count is 0)
        {
            throw new NotImplementedException("Нету ниодного наследника AutoMapper.Profile");
        }

        return new MapperConfiguration(configure =>
        {
            foreach (var inheritor in profileInheritors)
            {
                if (inheritor.GetConstructor(Type.EmptyTypes) == null)
                {
                    throw new InvalidOperationException(
                        $"Профиль '{inheritor.FullName}' должен иметь публичный конструктор без параметров!!");
                }

                var profile = (Profile) Activator.CreateInstance(inheritor)!;
                configure.AddProfile(profile);
            }
        });
    }

    /// <summary>
    /// Проверяет, что класс наследник Profile
    /// </summary>
    private bool IsProfileType(Type type)
    {
        if (!type.IsClass)
        {
            return false;
        }

        if (type.IsAbstract)
        {
            return false;
        }

        if (!typeof(Profile).IsAssignableFrom(type))
        {
            return false;
        }

        return true;
    }
}