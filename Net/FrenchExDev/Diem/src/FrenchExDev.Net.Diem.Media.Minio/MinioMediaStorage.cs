namespace FrenchExDev.Net.Diem.Media.Minio;

/// <summary>
/// S3/Minio implementation of IMediaStorage.
/// </summary>
public sealed class MinioMediaStorage : IMediaStorage
{
    private readonly string _endpoint;
    private readonly string _bucket;

    public MinioMediaStorage(string endpoint, string bucket)
    {
        _endpoint = endpoint;
        _bucket = bucket;
    }

    public Task<string> UploadAsync(string fileName, Stream content, string contentType, CancellationToken ct = default)
        => throw new NotImplementedException("Minio upload — will use Minio SDK");

    public Task<Stream?> DownloadAsync(string path, CancellationToken ct = default)
        => throw new NotImplementedException("Minio download");

    public Task DeleteAsync(string path, CancellationToken ct = default)
        => throw new NotImplementedException("Minio delete");

    public Task<bool> ExistsAsync(string path, CancellationToken ct = default)
        => throw new NotImplementedException("Minio exists");

    public string GetPublicUrl(string path) => $"{_endpoint}/{_bucket}/{path}";
}
