using System.Text.Json;

using FrenchExDev.Net.QualityGate.Abstractions;
using FrenchExDev.Net.QualityGate.Model;

namespace FrenchExDev.Net.QualityGate;

internal sealed class DefaultReportWriter : IReportWriter
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task<string> WriteAsync(QualityReport report, string outputRoot, CancellationToken ct = default)
    {
        var timestamp = report.Timestamp.ToString("yyyy-MM-ddTHH-mm-ss");
        var outputDir = Path.Combine(outputRoot, timestamp);
        Directory.CreateDirectory(outputDir);

        await WriteReportJsonAsync(outputDir, report, ct).ConfigureAwait(false);
        await WriteSummaryAsync(outputDir, report, ct).ConfigureAwait(false);
        await UpdateRunsManifestAsync(outputRoot, timestamp, report, ct).ConfigureAwait(false);

        return outputDir;
    }

    private static async Task WriteReportJsonAsync(string outputDir, QualityReport report, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(report, s_jsonOptions);
        await File.WriteAllTextAsync(Path.Combine(outputDir, "report.json"), json, ct).ConfigureAwait(false);
    }

    private static async Task WriteSummaryAsync(string outputDir, QualityReport report, CancellationToken ct)
    {
        var failedGates = report.GateResults.Where(g => !g.Passed).ToList();
        var allPassed = failedGates.Count == 0;

        var lines = BuildSummaryLines(report, failedGates, allPassed);
        await File.WriteAllTextAsync(
            Path.Combine(outputDir, "summary.txt"),
            string.Join(Environment.NewLine, lines),
            ct).ConfigureAwait(false);
    }

    internal static List<string> BuildSummaryLines(QualityReport report, List<QualityGateResult> failedGates, bool allPassed)
    {
        var lines = new List<string>
        {
            $"Quality Gate Report - {report.Timestamp:yyyy-MM-dd HH:mm:ss UTC}",
            $"Solution: {report.SolutionPath}",
            $"Projects analyzed: {report.Projects.Count}",
            $"Gate status: {(allPassed ? "PASSED" : "FAILED")}",
            ""
        };

        if (!allPassed)
        {
            lines.Add($"Failed gates ({failedGates.Count}):");
            foreach (var gate in failedGates)
            {
                lines.Add($"  - [{gate.GateName}] {gate.Description}");
                if (gate.ViolatingElement is not null)
                    lines.Add($"    Element: {gate.ViolatingElement}");
                lines.Add($"    Threshold: {gate.Threshold}, Actual: {gate.ActualValue}");
            }
        }
        else
        {
            lines.Add("All quality gates passed.");
        }

        if (report.Coverage is not null)
        {
            lines.Add("");
            lines.Add($"Coverage: line={report.Coverage.LineRate:P1}, branch={report.Coverage.BranchRate:P1}");
        }

        if (report.Mutation is not null)
        {
            lines.Add("");
            lines.Add($"Mutation: score={report.Mutation.MutationScore:P1}, killed={report.Mutation.Killed}/{report.Mutation.TotalMutants}");
        }

        return lines;
    }

    private static async Task UpdateRunsManifestAsync(
        string outputRoot, string timestamp, QualityReport report, CancellationToken ct)
    {
        var allPassed = report.GateResults.All(g => g.Passed);
        var summaryLines = BuildSummaryLines(report, report.GateResults.Where(g => !g.Passed).ToList(), allPassed);

        var runsPath = Path.Combine(outputRoot, "runs.json");
        var runs = await LoadRunsAsync(runsPath, ct).ConfigureAwait(false);

        var totalTypes = report.Projects.Sum(p => p.Namespaces.Sum(ns => ns.Types.Count));
        var totalMethods = report.Projects.Sum(p => p.Namespaces.Sum(ns => ns.Types.Sum(t => t.Methods.Count)));

        runs.Add(new Dictionary<string, object>
        {
            ["id"] = timestamp,
            ["timestamp"] = timestamp,
            ["gatesPassed"] = report.GateResults.Count(g => g.Passed),
            ["gatesFailed"] = report.GateResults.Count(g => !g.Passed),
            ["totalTypes"] = totalTypes,
            ["totalMethods"] = totalMethods,
            ["coveragePct"] = report.Coverage is not null ? Math.Round(report.Coverage.LineRate * 100, 1) : 0,
            ["mutationPct"] = report.Mutation is not null ? Math.Round(report.Mutation.MutationScore * 100, 1) : 0,
            ["summary"] = string.Join(" | ", summaryLines.Where(l => !string.IsNullOrWhiteSpace(l)).Take(3))
        });

        var json = JsonSerializer.Serialize(runs, s_jsonOptions);
        await File.WriteAllTextAsync(runsPath, json, ct).ConfigureAwait(false);
    }

    private static async Task<List<Dictionary<string, object>>> LoadRunsAsync(string runsPath, CancellationToken ct)
    {
        if (!File.Exists(runsPath))
            return [];

        try
        {
            var existingJson = await File.ReadAllTextAsync(runsPath, ct).ConfigureAwait(false);
            return JsonSerializer.Deserialize<List<Dictionary<string, object>>>(existingJson) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
