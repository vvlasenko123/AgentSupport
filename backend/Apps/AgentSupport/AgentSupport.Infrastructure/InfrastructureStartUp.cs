using AgentSupport.Domain.Models.Complaints;
using AgentSupport.Infrastructure.Migrations;
using AgentSupport.Infrastructure.Repositories.ComplaintRepository;
using Infrastructure.Database.Common.Interfaces;
using Infrastructure.Database.PostgreSQL.Migrations.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSupport.Infrastructure;

/// <summary>
/// Инфраструктурный слой
/// </summary>
public static class InfrastructureStartUp
{
    /// <summary>
    /// Подключение инфраструктуры модуля
    /// </summary>
    public static void AddAgentInfrastructure(this IServiceCollection services)
    {
        #region Миграции отката
        /*
         */
        #endregion

        #region Миграции применения
        services.AddTransient<IDatabaseMigration, ComplaintCreateTableMigration>();
        services.AddTransient<IDatabaseMigration, ComplaintAddStatusMigration>();
        services.AddTransient<IDatabaseMigration, SubscribersCreateTableMigration>();
        #endregion

        #region Репозитории
        services.AddScoped<IRepository<ComplaintModel>, ComplaintRepository>();
        #endregion
    }
}
