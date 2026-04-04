using FrenchExDev.Net.Vos.Abstractions.IO;

namespace FrenchExDev.Net.Vos.Infra.FileSystem;

/// <summary>
/// Real filesystem implementation of IFileSystem.
/// Registered as singleton in DI by the CLI host.
/// </summary>
public sealed class PhysicalFileSystem : IFileSystem
{
    public IFile GetFile(string path) => new PhysicalFile(path);

    public IEnumerable<IFile> GetFiles(string directory, string pattern = "*")
    {
        if (!Directory.Exists(directory))
            return Enumerable.Empty<IFile>();
        return Directory.GetFiles(directory, pattern, SearchOption.AllDirectories)
            .Select(p => new PhysicalFile(p));
    }

    public void CreateDirectory(string path) => Directory.CreateDirectory(path);

    public bool DirectoryExists(string path) => Directory.Exists(path);

    public void DeleteDirectory(string path, bool recursive = false) => Directory.Delete(path, recursive);

    public void DeleteFile(string path) => File.Delete(path);
}
