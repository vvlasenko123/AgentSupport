using System.Reflection;
using AgentSupport.Api.Handlers;
using AgentSupport.Application;
using AgentSupport.Domain;
using AgentSupport.Infrastructure;
using Infrastructure.Broker;
using Infrastructure.Broker.Kafka.Rpc.Handler.Interfaces;
using Infrastructure.Broker.Kafka.Rpc.Services;
using Infrastructure.Database;
using Infrastructure.Mapper;
using Infrastructure.MinIO;
using Infrastructure.Swagger;

namespace AgentSupport.Api;

/// <summary>
/// Класс настройки сервиса
/// </summary>
public class StartUp
{
    private IWebHostEnvironment Environment { get; }

    public StartUp(IWebHostEnvironment env)
    {
        Environment = env;
    }

    /// <summary>
    /// Конфигурация сервисов
    /// </summary>
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddControllers().AddDataAnnotationsLocalization();
        services.AddCors(options =>
        {
            options.AddDefaultPolicy(builder =>
            {
                builder
                    .SetIsOriginAllowed(_ => true)
                    .AllowAnyMethod()
                    .AllowAnyHeader()
                    .AllowCredentials();
            });
        });

        services.AddMapperExtension(assemblies: Assembly.GetExecutingAssembly());
        services.AddPostgres();
        services.AddMinioStorage();

        if (Environment.IsDevelopment())
        {
            services.AddHealthChecks();
            services.AddSwaggerDocumentation(apiName: "AgentSupport Api", version: "v1");
        }

        services.AddDomain();
        services.AddApplication();
        services.AddAgentInfrastructure();

        services.AddPostgres();
        services.AddMinioStorage();
        services.AddKafka();
        services.AddScoped<IKafkaRpcHandler, EmailReceivedRpcHandler>();
        services.AddHostedService<KafkaRpcServerHostedService>();
    }

    /// <summary>
    /// Конфигурация приложения
    /// </summary>
    public void Configure(IApplicationBuilder app)
    {
        if (Environment.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
            app.UseSwaggerDocumentation();
        }

        app.UseCors();
        app.UseRouting();

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers();
            endpoints.MapHealthChecks("/health");
        });
    }
}