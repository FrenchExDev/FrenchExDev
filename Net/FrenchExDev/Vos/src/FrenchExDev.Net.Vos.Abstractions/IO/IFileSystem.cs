namespace FrenchExDev.Net.Vos.Abstractions.IO;

/// <summary>
/// Abstraction over filesystem operations. Enables in-memory fakes for testing.
/// </summary>
public interface IFileSystem
{
    IFile GetFile(string path);
    IEnumerable<IFile> GetFiles(string directory, string pattern = "*");
    void CreateDirectory(string path);
    bool DirectoryExists(string path);
    void DeleteDirectory(string path, bool recursive = false);
    void DeleteFile(string path);
}
