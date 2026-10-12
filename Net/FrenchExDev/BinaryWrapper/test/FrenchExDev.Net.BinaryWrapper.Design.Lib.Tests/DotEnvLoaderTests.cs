using FrenchExDev.Net.BinaryWrapper.Design.Lib;
using Shouldly;

namespace FrenchExDev.Net.BinaryWrapper.Design.Lib.Tests;

public sealed class DotEnvLoaderTests : IDisposable
{
    private readonly string _tempDir;

    public DotEnvLoaderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"dotenv-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void Load_ParsesKeyValuePairs()
    {
        File.WriteAllText(Path.Combine(_tempDir, ".env"), "FOO=bar\nBAZ=qux\n");

        var result = DotEnvLoader.Load(_tempDir);

        result.Count.ShouldBe(2);
        result["FOO"].ShouldBe("bar");
        result["BAZ"].ShouldBe("qux");
    }

    [Fact]
    public void Load_IgnoresComments()
    {
        File.WriteAllText(Path.Combine(_tempDir, ".env"), "# comment\nFOO=bar\n");

        var result = DotEnvLoader.Load(_tempDir);

        result.Count.ShouldBe(1);
        result["FOO"].ShouldBe("bar");
    }

    [Fact]
    public void Load_IgnoresEmptyLines()
    {
        File.WriteAllText(Path.Combine(_tempDir, ".env"), "\nFOO=bar\n\nBAZ=qux\n");

        var result = DotEnvLoader.Load(_tempDir);

        result.Count.ShouldBe(2);
    }

    [Fact]
    public void Load_TrimsWhitespace()
    {
        File.WriteAllText(Path.Combine(_tempDir, ".env"), "  FOO  =  bar  \n");

        var result = DotEnvLoader.Load(_tempDir);

        result["FOO"].ShouldBe("bar");
    }

    [Fact]
    public void Load_NoEnvFile_ReturnsEmptyDict()
    {
        var emptyDir = Path.Combine(_tempDir, "empty");
        Directory.CreateDirectory(emptyDir);

        var result = DotEnvLoader.Load(emptyDir);

        result.ShouldBeEmpty();
    }

    [Fact]
    public void Load_WalksUpDirectoryTree()
    {
        File.WriteAllText(Path.Combine(_tempDir, ".env"), "TOKEN=secret\n");
        var childDir = Path.Combine(_tempDir, "child", "grandchild");
        Directory.CreateDirectory(childDir);

        var result = DotEnvLoader.Load(childDir);

        result.Count.ShouldBe(1);
        result["TOKEN"].ShouldBe("secret");
    }

    [Fact]
    public void Load_ValueWithEqualsSign_PreservesIt()
    {
        File.WriteAllText(Path.Combine(_tempDir, ".env"), "KEY=val=ue\n");

        var result = DotEnvLoader.Load(_tempDir);

        result["KEY"].ShouldBe("val=ue");
    }

    [Fact]
    public void Load_LineWithoutEquals_Ignored()
    {
        File.WriteAllText(Path.Combine(_tempDir, ".env"), "NOEQUALS\nFOO=bar\n");

        var result = DotEnvLoader.Load(_tempDir);

        result.Count.ShouldBe(1);
        result["FOO"].ShouldBe("bar");
    }

    [Fact]
    public void Load_EqualsAtStart_Ignored()
    {
        File.WriteAllText(Path.Combine(_tempDir, ".env"), "=value\nFOO=bar\n");

        var result = DotEnvLoader.Load(_tempDir);

        result.Count.ShouldBe(1);
        result["FOO"].ShouldBe("bar");
    }

    [Fact]
    public void Load_DuplicateKeys_LastWins()
    {
        File.WriteAllText(Path.Combine(_tempDir, ".env"), "FOO=first\nFOO=second\n");

        var result = DotEnvLoader.Load(_tempDir);

        result["FOO"].ShouldBe("second");
    }

    [Fact]
    public void Load_ClosestEnvFileWins()
    {
        File.WriteAllText(Path.Combine(_tempDir, ".env"), "FOO=parent\n");
        var childDir = Path.Combine(_tempDir, "child");
        Directory.CreateDirectory(childDir);
        File.WriteAllText(Path.Combine(childDir, ".env"), "FOO=child\n");

        var result = DotEnvLoader.Load(childDir);

        result["FOO"].ShouldBe("child");
    }
}
