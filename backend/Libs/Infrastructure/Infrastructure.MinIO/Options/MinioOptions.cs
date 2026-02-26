using Microsoft.Extensions.Options;

namespace Infrastructure.MinIO.Options;

/// <summary>
/// Настройки Minio
/// </summary>
public sealed class MinioOptions : IValidateOptions<MinioOptions>
{
    /// <summary>
    /// Адрес Minio-сервера
    /// </summary>
    public string Endpoint { get; init; } = string.Empty;

    /// <summary>
    /// Ключ доступа к Minio
    /// </summary>
    public string AccessKey { get; init; } = string.Empty;

    /// <summary>
    /// Секретный ключ доступа к Minio
    /// </summary>
    public string SecretKey { get; init; } = string.Empty;

    /// <summary>
    /// Имя бакета по умолчанию в Minio
    /// </summary>
    public string BucketName { get; init; } = string.Empty;

    /// <summary>
    /// Флаг использования SSL при подключении к Minio
    /// </summary>
    public bool UseSsl { get; init; }

    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, MinioOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Endpoint))
        {
            return ValidateOptionsResult.Fail("Endpoint Minio не должен быть пустым");
        }

        if (string.IsNullOrWhiteSpace(options.AccessKey))
        {
            return ValidateOptionsResult.Fail("AccessKey Minio не должен быть пустым");
        }

        if (string.IsNullOrWhiteSpace(options.SecretKey))
        {
            return ValidateOptionsResult.Fail("SecretKey Minio не должен быть пустым");
        }

        if (string.IsNullOrWhiteSpace(options.BucketName))
        {
            return ValidateOptionsResult.Fail("BucketName Minio не должен быть пустым");
        }

        return ValidateOptionsResult.Success;
    }
}