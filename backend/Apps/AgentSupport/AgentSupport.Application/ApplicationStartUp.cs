using AgentSupport.Application.UseCases.Complaints;
using AgentSupport.Application.UseCases.Complaints.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSupport.Application;

/// <summary>
/// Слой Application
/// </summary>
public static class ApplicationStartUp
{
    /// <summary>
    /// Добавление Application слоя
    /// </summary>
    public static void AddApplication(this IServiceCollection services)
    {
        services.AddComplaints();
    }
    
    /// <summary>
    /// Добавление модуля жалоб
    /// </summary>
    private static void AddComplaints(this IServiceCollection services)
    {
        services.AddScoped<ICreateComplaintUseCase, CreateComplaintUseCase>();
        services.AddScoped<IGetComplaintByIdUseCase, GetComplaintByIdUseCase>();
        services.AddScoped<IGetComplaintsUseCase, GetComplaintsUseCase>();
        services.AddScoped<IUpdateComplaintUseCase, UpdateComplaintUseCase>();
        services.AddScoped<IDeleteComplaintUseCase, DeleteComplaintUseCase>();
    }
}
