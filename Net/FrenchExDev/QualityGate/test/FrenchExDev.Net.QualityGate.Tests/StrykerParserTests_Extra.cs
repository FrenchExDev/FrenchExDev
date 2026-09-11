using FrenchExDev.Net.QualityGate.Reports;

using Shouldly;

namespace FrenchExDev.Net.QualityGate.Tests;

public class StrykerParserTests_Extra : IDisposable
{
    private readonly string _tempDir;

    public StrykerParserTests_Extra()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"stryker-extra-{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [Fact]
    public void Parse_FileWithNoMutantsProperty_TreatsAsZeroMutants()
    {
        var json = """
            {
              "files": {
                "Foo.cs": {
                  "language": "cs"
                }
              }
            }
            """;

        var path = Path.Combine(_tempDir, "no-mutants.json");
        File.WriteAllText(path, json);

        var report = StrykerReportParser.Parse(path);

        report.TotalMutants.ShouldBe(0);
        report.Killed.ShouldBe(0);
        report.Survived.ShouldBe(0);
        report.Files.Count.ShouldBe(1);
        report.Files[0].MutationScore.ShouldBe(0.0);
    }

    [Fact]
    public void Parse_MutantWithMissingStatusProperty_DefaultsToEmptyString()
    {
        var json = """
            {
              "files": {
                "Bar.cs": {
                  "mutants": [
                    { "id": "1" },
                    { "status": "Killed" }
                  ]
                }
              }
            }
            """;

        var path = Path.Combine(_tempDir, "missing-status.json");
        File.WriteAllText(path, json);

        var report = StrykerReportParser.Parse(path);

        report.TotalMutants.ShouldBe(2);
        report.Killed.ShouldBe(1);
        // The mutant without status is counted in total but not in any category
        report.Survived.ShouldBe(0);
        report.NoCoverage.ShouldBe(0);
        report.Timeout.ShouldBe(0);
    }

    [Fact]
    public void Parse_WhitespacePath_Throws()
    {
        Should.Throw<ArgumentException>(() => StrykerReportParser.Parse("   "));
    }

    [Fact]
    public void TryParseGlobs_NonExistentBaseDir_ReturnsNull()
    {
        var result = StrykerReportParser.TryParseGlobs(
            Path.Combine(_tempDir, "nonexistent"),
            ["*.json"]);

        result.ShouldBeNull();
    }

    [Fact]
    public void TryParseGlobs_MatchesNewestFile()
    {
        var json1 = """
            {
              "files": {
                "Old.cs": {
                  "mutants": [{ "status": "Killed" }]
                }
              }
            }
            """;
        var json2 = """
            {
              "files": {
                "New.cs": {
                  "mutants": [{ "status": "Survived" }, { "status": "Survived" }]
                }
              }
            }
            """;

        var path1 = Path.Combine(_tempDir, "report-old.json");
        File.WriteAllText(path1, json1);
        // Ensure different write times
        File.SetLastWriteTimeUtc(path1, DateTime.UtcNow.AddMinutes(-5));

        var path2 = Path.Combine(_tempDir, "report-new.json");
        File.WriteAllText(path2, json2);

        var report = StrykerReportParser.TryParseGlobs(_tempDir, ["report-*.json"]);

        report.ShouldNotBeNull();
        // Should pick the newest file (report-new.json), which has 2 Survived
        report.Survived.ShouldBe(2);
        report.TotalMutants.ShouldBe(2);
    }

    [Fact]
    public void Parse_MutantWithNullStatusValue_DefaultsToEmpty()
    {
        var json = """
            {
              "files": {
                "Baz.cs": {
                  "mutants": [
                    { "status": null }
                  ]
                }
              }
            }
            """;

        var path = Path.Combine(_tempDir, "null-status.json");
        File.WriteAllText(path, json);

        var report = StrykerReportParser.Parse(path);

        report.TotalMutants.ShouldBe(1);
        report.Killed.ShouldBe(0);
        report.Survived.ShouldBe(0);
    }

    [Fact]
    public void TryParseGlobs_MultipleGlobPatterns_CollectsAll()
    {
        var json = """
            {
              "files": {
                "X.cs": {
                  "mutants": [{ "status": "Killed" }]
                }
              }
            }
            """;

        File.WriteAllText(Path.Combine(_tempDir, "a.json"), json);
        File.WriteAllText(Path.Combine(_tempDir, "b.report"), json);

        // Only first pattern matches
        var report = StrykerReportParser.TryParseGlobs(_tempDir, ["*.json", "*.report"]);

        report.ShouldNotBeNull();
    }
}
