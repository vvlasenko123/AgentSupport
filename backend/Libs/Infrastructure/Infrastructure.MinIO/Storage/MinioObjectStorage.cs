using Infrastructure.MinIO.Options;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;

namespace Infrastructure.MinIO.Storage;

/// <summary>
/// Универсальное хранилище файлов в MinIO
/// </summary>
public sealed class MinioObjectStorage
{
    /// <summary>
    /// Клиент MinIO
    /// </summary>
    private readonly IMinioClient _client;

    /// <summary>
    /// Опции MinIO
    /// </summary>
    private readonly MinioOptions _options;

    public MinioObjectStorage(IMinioClient client, IOptions<MinioOptions> options)
    {
        _options = options.Value;
        _client = client;

        if (string.IsNullOrWhiteSpace(_options.BucketName))
        {
            throw new ArgumentException("Имя бакета MinIO не задано", nameof(options));
        }
    }

    /// <summary>
    /// Загружает файл в MinIO
    /// </summary>
    public async Task<string> UploadAsync(
        string objectType,
        Guid objectId,
        string fileName,
        Stream content,
        long contentLength,
        string contentType,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(objectType))
        {
            throw new ArgumentException("Не задан тип объекта", nameof(objectType));
        }

        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentException("Не задано имя файла", nameof(fileName));
        }

        if (content is null)
        {
            throw new ArgumentNullException(nameof(content), "Поток содержимого не может быть null");
        }

        if (contentLength <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(contentLength), "Размер файла должен быть больше нуля");
        }

        if (string.IsNullOrWhiteSpace(contentType))
        {
            throw new ArgumentException("Не задан тип содержимого", nameof(contentType));
        }

        await EnsureBucketExistsAsync(cancellationToken);

        var normalizedObjectType = NormalizePathSegment(objectType);
        var normalizedFileName = NormalizeFileName(fileName);
        var objectName = $"{normalizedObjectType}/{objectId:D}/{UUIDNext.Uuid.NewSequential():N}_{normalizedFileName}";

        var args = new PutObjectArgs()
            .WithBucket(_options.BucketName)
            .WithObject(objectName)
            .WithStreamData(content)
            .WithObjectSize(contentLength)
            .WithContentType(contentType);

        await _client.PutObjectAsync(args, cancellationToken);

        return objectName;
    }

    /// <summary>
    /// Получает ссылку на скачивание файла.
    /// </summary>
    public async Task<string> GetDownloadUrlAsync(string objectName, int expirySeconds, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(objectName))
        {
            throw new ArgumentException("Не задан путь объекта", nameof(objectName));
        }

        if (expirySeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(expirySeconds), "Время жизни ссылки должно быть больше нуля");
        }

        await EnsureBucketExistsAsync(cancellationToken);

        var args = new PresignedGetObjectArgs().WithBucket(_options.BucketName).WithObject(objectName).WithExpiry(expirySeconds);
        return await _client.PresignedGetObjectAsync(args);
    }

    /// <summary>
    /// Удаляет файл из MinIO по имени объекта
    /// </summary>
    public async Task RemoveAsync(string objectName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(objectName))
        {
            throw new ArgumentException("Не задан путь объекта", nameof(objectName));
        }

        await EnsureBucketExistsAsync(cancellationToken);

        var args = new RemoveObjectArgs().WithBucket(_options.BucketName).WithObject(objectName);
        await _client.RemoveObjectAsync(args, cancellationToken);
    }

    /// <summary>
    /// Создает бакет, если его нет
    /// </summary>
    private async Task EnsureBucketExistsAsync(CancellationToken cancellationToken)
    {
        var bucketExistsArgs = new BucketExistsArgs().WithBucket(_options.BucketName);
        var exists = await _client.BucketExistsAsync(bucketExistsArgs, cancellationToken);

        if (exists)
        {
            return;
        }

        var makeBucketArgs = new MakeBucketArgs().WithBucket(_options.BucketName);
        await _client.MakeBucketAsync(makeBucketArgs, cancellationToken);
    }

    /// <summary>
    /// Нормализует сегмент пути MinIO
    /// </summary>
    private static string NormalizePathSegment(string value)
    {
        var result = value.Trim().Replace("\\", "/");

        while (result.Contains("//", StringComparison.Ordinal))
        {
            result = result.Replace("//", "/", StringComparison.Ordinal);
        }

        result = result.Trim('/');

        if (string.IsNullOrWhiteSpace(result))
        {
            throw new ArgumentException("Тип объекта содержит некорректное значение", nameof(value));
        }

        return result;
    }

    /// <summary>
    /// Нормализует имя файла для сохранения в MinIO.
    /// </summary>
    private static string NormalizeFileName(string fileName)
    {
        var result = fileName.Trim();

        result = result.Replace("\\", "_", StringComparison.Ordinal);
        result = result.Replace("/", "_", StringComparison.Ordinal);

        if (string.IsNullOrWhiteSpace(result))
        {
            throw new ArgumentException("Имя файла содержит некорректное значение", nameof(fileName));
        }

        return result;
    }
}