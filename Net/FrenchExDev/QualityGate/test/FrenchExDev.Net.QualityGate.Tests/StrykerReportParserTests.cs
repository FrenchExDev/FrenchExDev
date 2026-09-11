using FrenchExDev.Net.QualityGate.Reports;

using Shouldly;

namespace FrenchExDev.Net.QualityGate.Tests;

public class StrykerReportParserTests
{
    [Fact]
    public void Parse_ValidJson_ExtractsMutationStats()
    {
        var json = """
            {
              "files": {
                "Foo.cs": {
                  "mutants": [
                    { "status": "Killed" },
                    { "status": "Killed" },
                    { "status": "Survived" },
                    { "status": "NoCoverage" },
                    { "status": "Timeout" }
                  ]
                }
              }
            }
            """;

        var path = Path.Combine(Path.GetTempPath(), $"stryker-{Guid.NewGuid()}.json");
        try
        {
            File.WriteAllText(path, json);
            var report = StrykerReportParser.Parse(path);

            report.TotalMutants.ShouldBe(5);
            report.Killed.ShouldBe(2);
            report.Survived.ShouldBe(1);
            report.NoCoverage.ShouldBe(1);
            report.Timeout.ShouldBe(1);
            report.MutationScore.ShouldBe(0.4); // 2/5
            report.Files.Count.ShouldBe(1);
            report.Files[0].Path.ShouldBe("Foo.cs");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Parse_MultipleFiles_AggregatesCorrectly()
    {
        var json = """
            {
              "files": {
                "A.cs": {
                  "mutants": [
                    { "status": "Killed" },
                    { "status": "Killed" }
                  ]
                },
                "B.cs": {
                  "mutants": [
                    { "status": "Survived" },
                    { "status": "Killed" }
                  ]
                }
              }
            }
            """;

        var path = Path.Combine(Path.GetTempPath(), $"stryker-{Guid.NewGuid()}.json");
        try
        {
            File.WriteAllText(path, json);
            var report = StrykerReportParser.Parse(path);

            report.TotalMutants.ShouldBe(4);
            report.Killed.ShouldBe(3);
            report.Survived.ShouldBe(1);
            report.MutationScore.ShouldBe(0.75); // 3/4
            report.Files.Count.ShouldBe(2);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Parse_NoFilesProperty_ReturnsEmpty()
    {
        var json = "{}";

        var path = Path.Combine(Path.GetTempPath(), $"stryker-{Guid.NewGuid()}.json");
        try
        {
            File.WriteAllText(path, json);
            var report = StrykerReportParser.Parse(path);

            report.TotalMutants.ShouldBe(0);
            report.MutationScore.ShouldBe(0);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Parse_EmptyMutantsArray_ReturnsZero()
    {
        var json = """
            {
              "files": {
                "C.cs": { "mutants": [] }
              }
            }
            """;

        var path = Path.Combine(Path.GetTempPath(), $"stryker-{Guid.NewGuid()}.json");
        try
        {
            File.WriteAllText(path, json);
            var report = StrykerReportParser.Parse(path);

            report.TotalMutants.ShouldBe(0);
            report.Files.Count.ShouldBe(1);
            report.Files[0].MutationScore.ShouldBe(0);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Parse_CompileErrorStatus_CountedInTotal()
    {
        var json = """
            {
              "files": {
                "D.cs": {
                  "mutants": [
                    { "status": "CompileError" },
                    { "status": "Killed" }
                  ]
                }
              }
            }
            """;

        var path = Path.Combine(Path.GetTempPath(), $"stryker-{Guid.NewGuid()}.json");
        try
        {
            File.WriteAllText(path, json);
            var report = StrykerReportParser.Parse(path);

            report.TotalMutants.ShouldBe(2);
            report.Killed.ShouldBe(1);
            report.Survived.ShouldBe(0);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Parse_NullPath_Throws()
    {
        Should.Throw<ArgumentException>(() => StrykerReportParser.Parse(null!));
    }

    [Fact]
    public void TryParseGlobs_NullGlobs_ReturnsNull()
    {
        StrykerReportParser.TryParseGlobs(".", null).ShouldBeNull();
    }

    [Fact]
    public void TryParseGlobs_EmptyGlobs_ReturnsNull()
    {
        StrykerReportParser.TryParseGlobs(".", []).ShouldBeNull();
    }

    [Fact]
    public void TryParseGlobs_NoMatches_ReturnsNull()
    {
        StrykerReportParser.TryParseGlobs(Path.GetTempPath(), ["nonexistent-*.json"]).ShouldBeNull();
    }
}
