namespace FrenchExDev.Net.Vos.Lib.Tests.Fakes;

public sealed class FakeFileSystem : IFileSystem
{
    private readonly HashSet<string> _existingFiles = new(StringComparer.OrdinalIgnoreCase);

    public void AddFile(string path) => _existingFiles.Add(path);

    public IFile GetFile(string path) => new FakeFile(path, _existingFiles.Contains(path));

    public IEnumerable<IFile> GetFiles(string directory, string pattern = "*") =>
        _existingFiles
            .Where(f => f.StartsWith(directory, StringComparison.OrdinalIgnoreCase))
            .Select(f => new FakeFile(f, true));

    public void CreateDirectory(string path) { }
    public bool DirectoryExists(string path) => false;
    public void DeleteDirectory(string path, bool recursive = false) { }
    public void DeleteFile(string path) => _existingFiles.Remove(path);

    private sealed class FakeFile(string path, bool exists) : IFile
    {
        public string Name => Path.GetFileName(path);
        public string Extension => Path.GetExtension(path);
        public string FullPath => path;
        public bool Exists => exists;
        public Task<string> ReadContentAsync(CancellationToken ct = default) => Task.FromResult("");
        public Task WriteContentAsync(string content, CancellationToken ct = default) => Task.CompletedTask;
    }
}
