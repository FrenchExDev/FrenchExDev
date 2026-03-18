using FrenchExDev.Net.QualityGate.Reports;

using Shouldly;

namespace FrenchExDev.Net.QualityGate.Tests;

public class CoberturaParserTests_Extra : IDisposable
{
    private readonly string _tempDir;

    public CoberturaParserTests_Extra()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"cobertura-extra-{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [Fact]
    public void Parse_InvalidNumberFormatInAttribute_ThrowsFormatException()
    {
        var xml = """
            <?xml version="1.0" encoding="utf-8"?>
            <coverage line-rate="not-a-number" branch-rate="0.5">
              <packages />
            </coverage>
            """;

        var path = Path.Combine(_tempDir, "bad-rate.xml");
        File.WriteAllText(path, xml);

        Should.Throw<FormatException>(() => CoberturaParser.Parse(path));
    }

    [Fact]
    public void Parse_ClassMissingNameAttribute_DefaultsToUnknown()
    {
        var xml = """
            <?xml version="1.0" encoding="utf-8"?>
            <coverage line-rate="0.5" branch-rate="0.5">
              <packages>
                <package>
                  <classes>
                    <class filename="test.cs" line-rate="0.8" branch-rate="0.7" />
                  </classes>
                </package>
              </packages>
            </coverage>
            """;

        var path = Path.Combine(_tempDir, "no-name.xml");
        File.WriteAllText(path, xml);

        var report = CoberturaParser.Parse(path);

        report.Classes.Count.ShouldBe(1);
        report.Classes[0].Name.ShouldBe("(unknown)");
    }

    [Fact]
    public void Parse_ClassMissingFilenameAttribute_DefaultsToEmpty()
    {
        var xml = """
            <?xml version="1.0" encoding="utf-8"?>
            <coverage line-rate="0.5" branch-rate="0.5">
              <packages>
                <package>
                  <classes>
                    <class name="MyClass" line-rate="0.8" branch-rate="0.7" />
                  </classes>
                </package>
              </packages>
            </coverage>
            """;

        var path = Path.Combine(_tempDir, "no-filename.xml");
        File.WriteAllText(path, xml);

        var report = CoberturaParser.Parse(path);

        report.Classes[0].FileName.ShouldBe("");
    }

    [Fact]
    public void Parse_ClassMissingRateAttributes_DefaultsToZero()
    {
        var xml = """
            <?xml version="1.0" encoding="utf-8"?>
            <coverage line-rate="0.5" branch-rate="0.5">
              <packages>
                <package>
                  <classes>
                    <class name="MyClass" filename="test.cs" />
                  </classes>
                </package>
              </packages>
            </coverage>
            """;

        var path = Path.Combine(_tempDir, "no-rates.xml");
        File.WriteAllText(path, xml);

        var report = CoberturaParser.Parse(path);

        report.Classes[0].LineRate.ShouldBe(0.0);
        report.Classes[0].BranchRate.ShouldBe(0.0);
    }

    [Fact]
    public void Parse_WhitespacePath_Throws()
    {
        Should.Throw<ArgumentException>(() => CoberturaParser.Parse("   "));
    }

    [Fact]
    public void TryParseGlobs_NonExistentBaseDir_ReturnsNull()
    {
        var result = CoberturaParser.TryParseGlobs(
            Path.Combine(_tempDir, "nonexistent"),
            ["*.xml"]);

        result.ShouldBeNull();
    }

    [Fact]
    public void TryParseGlobs_MultipleGlobs_FirstMatchWins()
    {
        var xml = """
            <?xml version="1.0" encoding="utf-8"?>
            <coverage line-rate="0.99" branch-rate="0.88">
              <packages />
            </coverage>
            """;
        File.WriteAllText(Path.Combine(_tempDir, "coverage.xml"), xml);

        var report = CoberturaParser.TryParseGlobs(_tempDir,
            ["nonexistent.xml", "coverage.xml"]);

        report.ShouldNotBeNull();
        report.LineRate.ShouldBe(0.99);
    }

    [Fact]
    public void Parse_InvalidBranchRateFormat_ThrowsFormatException()
    {
        var xml = """
            <?xml version="1.0" encoding="utf-8"?>
            <coverage line-rate="0.5" branch-rate="abc">
              <packages />
            </coverage>
            """;

        var path = Path.Combine(_tempDir, "bad-branch.xml");
        File.WriteAllText(path, xml);

        Should.Throw<FormatException>(() => CoberturaParser.Parse(path));
    }
}
