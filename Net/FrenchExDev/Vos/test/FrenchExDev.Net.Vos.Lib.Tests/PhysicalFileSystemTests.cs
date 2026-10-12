using FrenchExDev.Net.Vos.Infra.FileSystem;

namespace FrenchExDev.Net.Vos.Lib.Tests;

public class PhysicalFileSystemTests : IDisposable
{
    private readonly string _tempDir;
    private readonly PhysicalFileSystem _fs = new();

    public PhysicalFileSystemTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "vos-fs-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    // ── GetFile ──────────────────────────────────────────────────────

    [Fact]
    public void GetFile_returns_file_with_correct_name()
    {
        var path = Path.Combine(_tempDir, "test.txt");
        var file = _fs.GetFile(path);

        file.Name.ShouldBe("test.txt");
    }

    [Fact]
    public void GetFile_returns_file_with_correct_extension()
    {
        var path = Path.Combine(_tempDir, "test.yaml");
        var file = _fs.GetFile(path);

        file.Extension.ShouldBe(".yaml");
    }

    [Fact]
    public void GetFile_returns_file_with_correct_full_path()
    {
        var path = Path.Combine(_tempDir, "test.txt");
        var file = _fs.GetFile(path);

        file.FullPath.ShouldBe(path);
    }

    [Fact]
    public void GetFile_exists_returns_false_when_file_missing()
    {
        var path = Path.Combine(_tempDir, "nonexistent.txt");
        var file = _fs.GetFile(path);

        file.Exists.ShouldBeFalse();
    }

    [Fact]
    public void GetFile_exists_returns_true_when_file_exists()
    {
        var path = Path.Combine(_tempDir, "exists.txt");
        File.WriteAllText(path, "hello");
        var file = _fs.GetFile(path);

        file.Exists.ShouldBeTrue();
    }

    // ── ReadContentAsync / WriteContentAsync ─────────────────────────

    [Fact]
    public async Task ReadContentAsync_and_WriteContentAsync_round_trip()
    {
        var path = Path.Combine(_tempDir, "roundtrip.txt");
        var file = _fs.GetFile(path);
        var content = "hello world\nline 2";

        await file.WriteContentAsync(content);
        var read = await file.ReadContentAsync();

        read.ShouldBe(content);
    }

    // ── GetFiles ─────────────────────────────────────────────────────

    [Fact]
    public void GetFiles_returns_matching_files()
    {
        File.WriteAllText(Path.Combine(_tempDir, "a.txt"), "a");
        File.WriteAllText(Path.Combine(_tempDir, "b.txt"), "b");
        File.WriteAllText(Path.Combine(_tempDir, "c.yaml"), "c");

        var txtFiles = _fs.GetFiles(_tempDir, "*.txt").ToList();
        txtFiles.Count.ShouldBe(2);
    }

    [Fact]
    public void GetFiles_returns_empty_for_nonexistent_directory()
    {
        var files = _fs.GetFiles(Path.Combine(_tempDir, "nonexistent"), "*.txt");
        files.ShouldBeEmpty();
    }

    // ── CreateDirectory / DirectoryExists ────────────────────────────

    [Fact]
    public void CreateDirectory_creates_directory()
    {
        var subDir = Path.Combine(_tempDir, "sub");
        _fs.DirectoryExists(subDir).ShouldBeFalse();

        _fs.CreateDirectory(subDir);

        _fs.DirectoryExists(subDir).ShouldBeTrue();
    }

    // ── DeleteDirectory ──────────────────────────────────────────────

    [Fact]
    public void DeleteDirectory_removes_empty_directory()
    {
        var subDir = Path.Combine(_tempDir, "to-delete");
        Directory.CreateDirectory(subDir);

        _fs.DeleteDirectory(subDir);

        _fs.DirectoryExists(subDir).ShouldBeFalse();
    }

    [Fact]
    public void DeleteDirectory_recursive_removes_directory_with_contents()
    {
        var subDir = Path.Combine(_tempDir, "to-delete-recursive");
        Directory.CreateDirectory(subDir);
        File.WriteAllText(Path.Combine(subDir, "file.txt"), "content");

        _fs.DeleteDirectory(subDir, recursive: true);

        _fs.DirectoryExists(subDir).ShouldBeFalse();
    }

    // ── DeleteFile ───────────────────────────────────────────────────

    [Fact]
    public void DeleteFile_removes_file()
    {
        var path = Path.Combine(_tempDir, "to-delete.txt");
        File.WriteAllText(path, "x");

        _fs.DeleteFile(path);

        File.Exists(path).ShouldBeFalse();
    }
}
