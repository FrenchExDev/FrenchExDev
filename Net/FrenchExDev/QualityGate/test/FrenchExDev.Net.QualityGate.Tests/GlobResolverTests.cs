using FrenchExDev.Net.QualityGate.Reports;

using Shouldly;

namespace FrenchExDev.Net.QualityGate.Tests;

public class GlobResolverTests : IDisposable
{
    private readonly string _tempDir;

    public GlobResolverTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"glob-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [Fact]
    public void Resolve_NonExistentBaseDir_ReturnsEmpty()
    {
        var result = GlobResolver.Resolve(Path.Combine(_tempDir, "does-not-exist"), "*.xml");
        result.ShouldBeEmpty();
    }

    [Fact]
    public void Resolve_SimpleFilenamePattern_FindsFiles()
    {
        File.WriteAllText(Path.Combine(_tempDir, "report.xml"), "<root/>");
        File.WriteAllText(Path.Combine(_tempDir, "other.txt"), "text");

        var result = GlobResolver.Resolve(_tempDir, "*.xml");

        result.Length.ShouldBe(1);
        Path.GetFileName(result[0]).ShouldBe("report.xml");
    }

    [Fact]
    public void Resolve_RecursiveDoubleStarPattern_FindsInSubdirs()
    {
        var sub = Path.Combine(_tempDir, "a", "b");
        Directory.CreateDirectory(sub);
        File.WriteAllText(Path.Combine(sub, "deep.xml"), "<root/>");

        var result = GlobResolver.Resolve(_tempDir, "**/*.xml");

        result.Length.ShouldBe(1);
        Path.GetFileName(result[0]).ShouldBe("deep.xml");
    }

    [Fact]
    public void Resolve_SubdirectorySegment_WalksIntoDir()
    {
        var sub = Path.Combine(_tempDir, "reports");
        Directory.CreateDirectory(sub);
        File.WriteAllText(Path.Combine(sub, "coverage.xml"), "<root/>");

        var result = GlobResolver.Resolve(_tempDir, "reports/coverage.xml");

        result.Length.ShouldBe(1);
        Path.GetFileName(result[0]).ShouldBe("coverage.xml");
    }

    [Fact]
    public void Resolve_BackslashNormalization_TreatedAsForwardSlash()
    {
        var sub = Path.Combine(_tempDir, "data");
        Directory.CreateDirectory(sub);
        File.WriteAllText(Path.Combine(sub, "file.json"), "{}");

        var result = GlobResolver.Resolve(_tempDir, "data\\file.json");

        result.Length.ShouldBe(1);
        Path.GetFileName(result[0]).ShouldBe("file.json");
    }

    [Fact]
    public void Resolve_NonExistentSubdirectory_ReturnsEmpty()
    {
        // Glob references a subdir that doesn't exist on disk
        var result = GlobResolver.Resolve(_tempDir, "nonexistent/sub/*.xml");

        result.ShouldBeEmpty();
    }

    [Fact]
    public void Resolve_DoubleStarInMiddle_SetsRecursiveAndWalksExistingDirs()
    {
        var sub = Path.Combine(_tempDir, "src", "nested");
        Directory.CreateDirectory(sub);
        File.WriteAllText(Path.Combine(sub, "found.xml"), "<x/>");

        // pattern: src/**/found.xml  -> walks into src, then recursively searches
        var result = GlobResolver.Resolve(_tempDir, "src/**/found.xml");

        result.Length.ShouldBe(1);
        Path.GetFileName(result[0]).ShouldBe("found.xml");
    }

    [Fact]
    public void Resolve_TopDirectoryOnly_DoesNotFindInSubdirs()
    {
        var sub = Path.Combine(_tempDir, "child");
        Directory.CreateDirectory(sub);
        File.WriteAllText(Path.Combine(sub, "hidden.xml"), "<root/>");

        // No ** in pattern, so only top-level search
        var result = GlobResolver.Resolve(_tempDir, "*.xml");

        result.ShouldBeEmpty();
    }

    [Fact]
    public void Resolve_MultipleSubdirSegments_WalksEach()
    {
        var sub = Path.Combine(_tempDir, "a", "b", "c");
        Directory.CreateDirectory(sub);
        File.WriteAllText(Path.Combine(sub, "deep.xml"), "<root/>");

        var result = GlobResolver.Resolve(_tempDir, "a/b/c/deep.xml");

        result.Length.ShouldBe(1);
    }
}
