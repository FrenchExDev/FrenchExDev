using System.Text.Json;

using FrenchExDev.Net.QualityGate.Config;
using FrenchExDev.Net.QualityGate.Model;

using Shouldly;

namespace FrenchExDev.Net.QualityGate.Tests;

public class QualityEngineTests
{
    [Fact]
    public void Constructor_NullConfig_Throws()
    {
        Should.Throw<ArgumentNullException>(() => new QualityEngine(null!));
    }

    [Fact]
    public async Task AnalyzeAsync_NullSolution_Throws()
    {
        var config = new QualityGateConfig { Solution = null };
        var engine = new QualityEngine(config);

        await Should.ThrowAsync<ArgumentException>(() => engine.AnalyzeAsync());
    }

    [Fact]
    public async Task AnalyzeAsync_EmptySolution_Throws()
    {
        var config = new QualityGateConfig { Solution = "" };
        var engine = new QualityEngine(config);

        await Should.ThrowAsync<ArgumentException>(() => engine.AnalyzeAsync());
    }

    [Fact]
    public async Task AnalyzeAsync_WhitespaceSolution_Throws()
    {
        var config = new QualityGateConfig { Solution = "   " };
        var engine = new QualityEngine(config);

        await Should.ThrowAsync<ArgumentException>(() => engine.AnalyzeAsync());
    }

    private static string? FindSolutionPath()
    {
        var path = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "FrenchExDev.Net.QualityGate.slnx"));
        return File.Exists(path) ? path : null;
    }

    private static async Task<string> RunEngineAsync(string solutionPath, string outputDir, GateThresholds? gates = null)
    {
        var config = new QualityGateConfig
        {
            Solution = solutionPath,
            Output = outputDir,
            Gates = gates ?? new GateThresholds()
        };
        return await new QualityEngine(config).RunAsync();
    }

    [Fact]
    public async Task RunAsync_WritesReportJson()
    {
        var solutionPath = FindSolutionPath();
        if (solutionPath is null) return;

        var outputDir = Path.Combine(Path.GetTempPath(), $"qg-{Guid.NewGuid()}");
        try
        {
            var resultDir = await RunEngineAsync(solutionPath, outputDir);
            File.Exists(Path.Combine(resultDir, "report.json")).ShouldBeTrue();

            var json = await File.ReadAllTextAsync(Path.Combine(resultDir, "report.json"));
            var doc = JsonDocument.Parse(json);
            doc.RootElement.TryGetProperty("solutionPath", out _).ShouldBeTrue();
        }
        finally { if (Directory.Exists(outputDir)) Directory.Delete(outputDir, true); }
    }

    [Fact]
    public async Task RunAsync_WritesSummaryTxt()
    {
        var solutionPath = FindSolutionPath();
        if (solutionPath is null) return;

        var outputDir = Path.Combine(Path.GetTempPath(), $"qg-{Guid.NewGuid()}");
        try
        {
            var resultDir = await RunEngineAsync(solutionPath, outputDir);
            var summary = await File.ReadAllTextAsync(Path.Combine(resultDir, "summary.txt"));
            summary.ShouldContain("Quality Gate Report");
        }
        finally { if (Directory.Exists(outputDir)) Directory.Delete(outputDir, true); }
    }

    [Fact]
    public async Task RunAsync_WritesRunsManifest()
    {
        var solutionPath = FindSolutionPath();
        if (solutionPath is null) return;

        var outputDir = Path.Combine(Path.GetTempPath(), $"qg-{Guid.NewGuid()}");
        try
        {
            await RunEngineAsync(solutionPath, outputDir);
            var runsJson = await File.ReadAllTextAsync(Path.Combine(outputDir, "runs.json"));
            JsonDocument.Parse(runsJson).RootElement.GetArrayLength().ShouldBe(1);
        }
        finally { if (Directory.Exists(outputDir)) Directory.Delete(outputDir, true); }
    }

    [Fact]
    public async Task RunAsync_AppendsToExistingRunsManifest()
    {
        var solutionPath = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "FrenchExDev.Net.QualityGate.slnx"));

        if (!File.Exists(solutionPath))
            return;

        var outputDir = Path.Combine(Path.GetTempPath(), $"qg-test-{Guid.NewGuid()}");

        try
        {
            // Pre-seed runs.json
            Directory.CreateDirectory(outputDir);
            await File.WriteAllTextAsync(
                Path.Combine(outputDir, "runs.json"),
                "[{\"timestamp\":\"old\",\"gatesPassed\":true,\"summary\":\"old run\"}]");

            var config = new QualityGateConfig
            {
                Solution = solutionPath,
                Output = outputDir,
                Gates = new GateThresholds()
            };

            var engine = new QualityEngine(config);
            await engine.RunAsync();

            var runsJson = await File.ReadAllTextAsync(Path.Combine(outputDir, "runs.json"));
            var runs = JsonDocument.Parse(runsJson);
            runs.RootElement.GetArrayLength().ShouldBe(2);
        }
        finally
        {
            if (Directory.Exists(outputDir))
                Directory.Delete(outputDir, true);
        }
    }

    [Fact]
    public async Task RunAsync_CorruptedRunsManifest_StartsOver()
    {
        var solutionPath = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "FrenchExDev.Net.QualityGate.slnx"));

        if (!File.Exists(solutionPath))
            return;

        var outputDir = Path.Combine(Path.GetTempPath(), $"qg-test-{Guid.NewGuid()}");

        try
        {
            Directory.CreateDirectory(outputDir);
            await File.WriteAllTextAsync(Path.Combine(outputDir, "runs.json"), "NOT VALID JSON!!!");

            var config = new QualityGateConfig
            {
                Solution = solutionPath,
                Output = outputDir,
                Gates = new GateThresholds()
            };

            var engine = new QualityEngine(config);
            await engine.RunAsync();

            var runsJson = await File.ReadAllTextAsync(Path.Combine(outputDir, "runs.json"));
            var runs = JsonDocument.Parse(runsJson);
            runs.RootElement.GetArrayLength().ShouldBe(1); // starts fresh
        }
        finally
        {
            if (Directory.Exists(outputDir))
                Directory.Delete(outputDir, true);
        }
    }

    [Fact]
    public async Task AnalyzeAsync_WithCoverageGlobs_ParsesCoverage()
    {
        var solutionPath = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "FrenchExDev.Net.QualityGate.slnx"));

        if (!File.Exists(solutionPath))
            return;

        var solutionDir = Path.GetDirectoryName(solutionPath)!;

        // Create a fake coverage file
        var coverageDir = Path.Combine(solutionDir, "test-coverage-temp");
        Directory.CreateDirectory(coverageDir);
        var coverageFile = Path.Combine(coverageDir, "coverage.cobertura.xml");
        await File.WriteAllTextAsync(coverageFile, """
            <?xml version="1.0" encoding="utf-8"?>
            <coverage line-rate="0.88" branch-rate="0.77"><packages/></coverage>
            """);

        try
        {
            var config = new QualityGateConfig
            {
                Solution = solutionPath,
                CoverageGlobs = ["test-coverage-temp/coverage.cobertura.xml"],
                Gates = new GateThresholds()
            };

            var engine = new QualityEngine(config);
            var report = await engine.AnalyzeAsync();

            report.Coverage.ShouldNotBeNull();
            report.Coverage.LineRate.ShouldBe(0.88);
            report.Coverage.BranchRate.ShouldBe(0.77);
            report.GateResults.ShouldNotBeEmpty();
        }
        finally
        {
            if (Directory.Exists(coverageDir))
                Directory.Delete(coverageDir, true);
        }
    }

    [Fact]
    public async Task RunAsync_WithCoverageAndMutation_SummaryIncludesThem()
    {
        var solutionPath = FindSolutionPath();
        if (solutionPath is null) return;

        var solutionDir = Path.GetDirectoryName(solutionPath)!;
        var outputDir = Path.Combine(Path.GetTempPath(), $"qg-{Guid.NewGuid()}");

        // Create temp coverage file
        var covDir = Path.Combine(solutionDir, $"tmp-cov-{Guid.NewGuid()}");
        Directory.CreateDirectory(covDir);
        await File.WriteAllTextAsync(Path.Combine(covDir, "coverage.cobertura.xml"),
            """<?xml version="1.0"?><coverage line-rate="0.8" branch-rate="0.7"><packages/></coverage>""");

        // Create temp mutation file
        await File.WriteAllTextAsync(Path.Combine(covDir, "mutation-report.json"),
            """{"files":{"A.cs":{"mutants":[{"status":"Killed"},{"status":"Survived"}]}}}""");

        try
        {
            var relCovDir = Path.GetRelativePath(solutionDir, covDir).Replace('\\', '/');
            var config = new QualityGateConfig
            {
                Solution = solutionPath,
                Output = outputDir,
                CoverageGlobs = [$"{relCovDir}/coverage.cobertura.xml"],
                MutationGlobs = [$"{relCovDir}/mutation-report.json"],
                Gates = new GateThresholds { MinTestQualityScore = 0 }
            };

            var resultDir = await new QualityEngine(config).RunAsync();
            var summary = await File.ReadAllTextAsync(Path.Combine(resultDir, "summary.txt"));
            summary.ShouldContain("Coverage:");
            summary.ShouldContain("Mutation:");
        }
        finally
        {
            if (Directory.Exists(outputDir)) Directory.Delete(outputDir, true);
            if (Directory.Exists(covDir)) Directory.Delete(covDir, true);
        }
    }

    [Fact]
    public async Task RunAsync_SummaryContainsPassedWhenAllGatesPass()
    {
        var solutionPath = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "FrenchExDev.Net.QualityGate.slnx"));

        if (!File.Exists(solutionPath))
            return;

        var outputDir = Path.Combine(Path.GetTempPath(), $"qg-test-{Guid.NewGuid()}");

        try
        {
            var config = new QualityGateConfig
            {
                Solution = solutionPath,
                Output = outputDir,
                Gates = new GateThresholds
                {
                    MaxCyclomaticComplexity = 999,
                    MaxCognitiveComplexity = 999,
                    MaxClassCoupling = 999,
                    MaxInheritanceDepth = 999,
                    MinMaintainabilityIndex = 0,
                    MaxLcom = 999,
                    MaxDistanceFromMainSequence = 999,
                    MaxDuplicationPercent = 999,
                    MinTestQualityScore = 0
                }
            };

            var engine = new QualityEngine(config);
            var resultDir = await engine.RunAsync();

            var summary = await File.ReadAllTextAsync(Path.Combine(resultDir, "summary.txt"));
            summary.ShouldContain("PASSED");
            summary.ShouldContain("All quality gates passed.");
        }
        finally
        {
            if (Directory.Exists(outputDir))
                Directory.Delete(outputDir, true);
        }
    }
}
