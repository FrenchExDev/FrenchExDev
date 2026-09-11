namespace FrenchExDev.Net.Diem.Media.FileSystem;

public sealed class FileSystemMediaStorage : IMediaStorage
{
    private readonly string _rootPath;
    private readonly string _publicUrlBase;

    public FileSystemMediaStorage(string rootPath, string publicUrlBase = "/media")
    {
        _rootPath = rootPath;
        _publicUrlBase = publicUrlBase;
    }

    public async Task<string> UploadAsync(string fileName, Stream content, string contentType, CancellationToken ct = default)
    {
        var path = Path.Combine(DateTime.UtcNow.ToString("yyyy/MM"), fileName);
        var fullPath = Path.Combine(_rootPath, path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await using var fs = File.Create(fullPath);
        await content.CopyToAsync(fs, ct);
        return path;
    }

    public Task<Stream?> DownloadAsync(string path, CancellationToken ct = default)
    {
        var fullPath = Path.Combine(_rootPath, path);
        if (!File.Exists(fullPath)) return Task.FromResult<Stream?>(null);
        return Task.FromResult<Stream?>(File.OpenRead(fullPath));
    }

    public Task DeleteAsync(string path, CancellationToken ct = default)
    {
        var fullPath = Path.Combine(_rootPath, path);
        if (File.Exists(fullPath)) File.Delete(fullPath);
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string path, CancellationToken ct = default)
        => Task.FromResult(File.Exists(Path.Combine(_rootPath, path)));

    public string GetPublicUrl(string path) => $"{_publicUrlBase}/{path}";
}
