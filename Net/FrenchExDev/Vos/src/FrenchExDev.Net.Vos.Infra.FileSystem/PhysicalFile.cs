using FrenchExDev.Net.Vos.Abstractions.IO;

namespace FrenchExDev.Net.Vos.Infra.FileSystem;

public sealed class PhysicalFile : IFile
{
    private readonly FileInfo _info;

    public PhysicalFile(string path)
    {
        _info = new FileInfo(path);
    }

    public string Name => _info.Name;
    public string Extension => _info.Extension;
    public string FullPath => _info.FullName;
    public bool Exists => _info.Exists;

    public Task<string> ReadContentAsync(CancellationToken ct = default)
        => File.ReadAllTextAsync(_info.FullName, ct);

    public Task WriteContentAsync(string content, CancellationToken ct = default)
        => File.WriteAllTextAsync(_info.FullName, content, ct);
}
