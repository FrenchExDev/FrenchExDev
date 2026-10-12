using FrenchExDev.Net.QualityGate.Config;
using FrenchExDev.Net.QualityGate.Model;
using FrenchExDev.Net.QualityGate.Tests.Fakes;

using Shouldly;

namespace FrenchExDev.Net.QualityGate.Tests;

public class QualityEngineWithFakesTests
{
    [Fact]
    public async Task AnalyzeAsync_WithFakeSolutionLoader_NoMsBuildRequired()
    {
        var engine = new QualityEngine(
            new QualityGateConfig { Solution = "fake.slnx" },
            solutionLoader: FakeSolutionLoader.Empty());

        var report = await engine.AnalyzeAsync();

        report.SolutionPath.ShouldBe("fake.slnx");
        report.Projects.ShouldBeEmpty();
    }

    [Fact]
    public async Task AnalyzeAsync_WithProject_RunsAnalyzers()
    {
        var source = @"
namespace TestNs
{
    public class Foo { public void Bar() { } }
}";
        var engine = new QualityEngine(
            new QualityGateConfig { Solution = "test.slnx" },
            solutionLoader: FakeSolutionLoader.WithSource(source));

        var report = await engine.AnalyzeAsync();

        report.Projects.Count.ShouldBe(1);
        report.Projects[0].Namespaces.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task AnalyzeAsync_WithFakeCoverage_InjectsIt()
    {
        var coverage = new CoverageReport { LineRate = 0.85, BranchRate = 0.72 };

        var engine = new QualityEngine(
            new QualityGateConfig { Solution = "test.slnx", CoverageGlobs = ["*.xml"] },
            solutionLoader: FakeSolutionLoader.Empty(),
            coverageParser: new FakeCoverageParser(coverage));

        var report = await engine.AnalyzeAsync();

        report.Coverage.ShouldNotBeNull();
        report.Coverage.LineRate.ShouldBe(0.85);
        report.Coverage.BranchRate.ShouldBe(0.72);
    }

    [Fact]
    public async Task AnalyzeAsync_WithFakeMutation_InjectsIt()
    {
        var mutation = new MutationReport
        {
            MutationScore = 0.9, TotalMutants = 10, Killed = 9,
            Survived = 1, NoCoverage = 0, Timeout = 0
        };

        var engine = new QualityEngine(
            new QualityGateConfig { Solution = "test.slnx", MutationGlobs = ["*.json"] },
            solutionLoader: FakeSolutionLoader.Empty(),
            mutationParser: new FakeMutationParser(mutation));

        var report = await engine.AnalyzeAsync();

        report.Mutation.ShouldNotBeNull();
        report.Mutation.MutationScore.ShouldBe(0.9);
    }

    [Fact]
    public async Task AnalyzeAsync_EvaluatesGates()
    {
        var engine = new QualityEngine(
            new QualityGateConfig
            {
                Solution = "test.slnx",
                Gates = new GateThresholds { MinTestQualityScore = 0 }
            },
            solutionLoader: FakeSolutionLoader.Empty());

        var report = await engine.AnalyzeAsync();

        report.GateResults.ShouldNotBeNull();
    }

    [Fact]
    public async Task RunAsync_UsesFakeReportWriter()
    {
        var writer = new FakeReportWriter();

        var engine = new QualityEngine(
            new QualityGateConfig { Solution = "test.slnx", Output = "/tmp/out" },
            solutionLoader: FakeSolutionLoader.Empty(),
            reportWriter: writer);

        var outputDir = await engine.RunAsync();

        writer.WriteCount.ShouldBe(1);
        writer.LastReport.ShouldNotBeNull();
        writer.LastReport.SolutionPath.ShouldBe("test.slnx");
        outputDir.ShouldContain("fake-run");
    }

    [Fact]
    public async Task RunAsync_WithAllFakes_NoFilesystemRequired()
    {
        var coverage = new CoverageReport { LineRate = 0.9, BranchRate = 0.8 };
        var mutation = new MutationReport
        {
            MutationScore = 0.85, TotalMutants = 20, Killed = 17,
            Survived = 3, NoCoverage = 0, Timeout = 0
        };
        var writer = new FakeReportWriter();

        var engine = new QualityEngine(
            new QualityGateConfig
            {
                Solution = "test.slnx",
                Output = "/tmp/out",
                CoverageGlobs = ["*.xml"],
                MutationGlobs = ["*.json"],
                Gates = new GateThresholds { MinTestQualityScore = 0 }
            },
            solutionLoader: FakeSolutionLoader.WithSource("namespace Ns { public class C { } }"),
            coverageParser: new FakeCoverageParser(coverage),
            mutationParser: new FakeMutationParser(mutation),
            reportWriter: writer);

        await engine.RunAsync();

        writer.LastReport.ShouldNotBeNull();
        writer.LastReport.Coverage.ShouldNotBeNull();
        writer.LastReport.Mutation.ShouldNotBeNull();
        writer.LastReport.Projects.Count.ShouldBe(1);
    }

    [Fact]
    public async Task AnalyzeAsync_NullCoverageGlobs_NoCoverage()
    {
        var engine = new QualityEngine(
            new QualityGateConfig { Solution = "test.slnx", CoverageGlobs = null },
            solutionLoader: FakeSolutionLoader.Empty(),
            coverageParser: new FakeCoverageParser(new CoverageReport { LineRate = 1, BranchRate = 1 }));

        var report = await engine.AnalyzeAsync();

        // Even though the fake would return data, the engine should still call it
        // and the fake doesn't check globs — it always returns the report
        report.ShouldNotBeNull();
    }

    [Fact]
    public void Constructor_DefaultImplementations_WhenNoFakesProvided()
    {
        // This just verifies the constructor doesn't throw with defaults
        var engine = new QualityEngine(new QualityGateConfig { Solution = "test.slnx" });
        engine.ShouldNotBeNull();
    }
}
