using Infrastructure.Database.PostgreSQL.Migrations.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using SmtpConnector.Dal.History.Interfaces;
using SmtpConnector.Dal.Migrations;
using SmtpConnector.Dal.Repository;

namespace SmtpConnector.Dal;

public static class SmtpConnectorDalStartUp
{
    public static void AddSmtpConnectorDal(this IServiceCollection services)
    {
        services.AddTransient<IDatabaseMigration, GmailHistoryStateCreateTableMigration>();
        services.AddScoped<IHistoryStateStore, PostgresHistoryStateStore>();
    }
}