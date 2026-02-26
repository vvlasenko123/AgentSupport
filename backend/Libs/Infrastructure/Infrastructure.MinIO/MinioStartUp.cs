using Infrastructure.MinIO.Options;
using Infrastructure.MinIO.Storage;
using Infrastructure.Options.Extensions.Validate;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Minio;

namespace Infrastructure.MinIO;

/// <summary>
/// minio startup
/// </summary>
public static class MinioStartUp
{
    /// <summary>
    /// Миньо extension
    /// </summary>
    public static void AddMinioStorage(this IServiceCollection services)
    {
        services.AddOptions<MinioOptions>()
            .BindConfiguration(configSectionPath: nameof(MinioOptions))
            .UseValidationOptions()
            .ValidateOnStart();

        //todo плохо выглядит
        services.AddSingleton<IMinioClient>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<MinioOptions>>().Value;

            var client = new MinioClient()
                .WithEndpoint(options.Endpoint)
                .WithCredentials(options.AccessKey, options.SecretKey);

            if (options.UseSsl)
            {
                client = client.WithSSL();
            }

            return client.Build();
        });

        services.AddScoped<MinioObjectStorage>();
    }
}