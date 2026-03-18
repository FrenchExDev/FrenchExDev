using FrenchExDev.Net.QualityGate.Reports;

using Shouldly;

namespace FrenchExDev.Net.QualityGate.Tests;

public class CoberturaParserTests
{
    [Fact]
    public void Parse_ValidXml_ExtractsRates()
    {
        var xml = """
            <?xml version="1.0" encoding="utf-8"?>
            <coverage line-rate="0.85" branch-rate="0.72">
              <packages>
                <package>
                  <classes>
                    <class name="MyClass" filename="MyClass.cs" line-rate="0.9" branch-rate="0.8" />
                    <class name="OtherClass" filename="Other.cs" line-rate="0.7" branch-rate="0.6" />
                  </classes>
                </package>
              </packages>
            </coverage>
            """;

        var path = Path.Combine(Path.GetTempPath(), $"cobertura-{Guid.NewGuid()}.xml");
        try
        {
            File.WriteAllText(path, xml);
            var report = CoberturaParser.Parse(path);

            report.LineRate.ShouldBe(0.85);
            report.BranchRate.ShouldBe(0.72);
            report.Classes.Count.ShouldBe(2);
            report.Classes[0].Name.ShouldBe("MyClass");
            report.Classes[0].LineRate.ShouldBe(0.9);
            report.Classes[1].BranchRate.ShouldBe(0.6);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Parse_MissingRates_DefaultsToZero()
    {
        var xml = """
            <?xml version="1.0" encoding="utf-8"?>
            <coverage>
              <packages />
            </coverage>
            """;

        var path = Path.Combine(Path.GetTempPath(), $"cobertura-{Guid.NewGuid()}.xml");
        try
        {
            File.WriteAllText(path, xml);
            var report = CoberturaParser.Parse(path);

            report.LineRate.ShouldBe(0);
            report.BranchRate.ShouldBe(0);
            report.Classes.ShouldBeEmpty();
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Parse_NullPath_Throws()
    {
        Should.Throw<ArgumentException>(() => CoberturaParser.Parse(null!));
    }

    [Fact]
    public void TryParseGlobs_NullGlobs_ReturnsNull()
    {
        CoberturaParser.TryParseGlobs(".", null).ShouldBeNull();
    }

    [Fact]
    public void TryParseGlobs_EmptyGlobs_ReturnsNull()
    {
        CoberturaParser.TryParseGlobs(".", []).ShouldBeNull();
    }

    [Fact]
    public void TryParseGlobs_NoMatches_ReturnsNull()
    {
        CoberturaParser.TryParseGlobs(Path.GetTempPath(), ["nonexistent-*.xml"]).ShouldBeNull();
    }

    [Fact]
    public void TryParseGlobs_MatchesFile_ParsesIt()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"cobertura-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(dir);
        var xml = """
            <?xml version="1.0" encoding="utf-8"?>
            <coverage line-rate="0.75" branch-rate="0.60">
              <packages />
            </coverage>
            """;
        File.WriteAllText(Path.Combine(dir, "coverage.cobertura.xml"), xml);

        try
        {
            var report = CoberturaParser.TryParseGlobs(dir, ["coverage.cobertura.xml"]);
            report.ShouldNotBeNull();
            report.LineRate.ShouldBe(0.75);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}
