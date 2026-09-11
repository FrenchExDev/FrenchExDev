using System.Text.Json;

using FrenchExDev.Net.QualityGate.Model;

namespace FrenchExDev.Net.QualityGate.Reports;

/// <summary>
/// Parses Stryker mutation testing JSON reports into <see cref="MutationReport"/> instances.
/// </summary>
public static class StrykerReportParser
{
    /// <summary>
    /// Parses a Stryker JSON report at <paramref name="jsonPath"/> and returns a <see cref="MutationReport"/>.
    /// </summary>
    public static MutationReport Parse(string jsonPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jsonPath);

        var json = File.ReadAllText(jsonPath);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (!root.TryGetProperty("files", out var filesElement))
        {
            return new MutationReport
            {
                MutationScore = 0,
                TotalMutants = 0,
                Killed = 0,
                Survived = 0,
                NoCoverage = 0,
                Timeout = 0
            };
        }

        int totalKilled = 0, totalSurvived = 0, totalNoCoverage = 0, totalTimeout = 0, totalMutants = 0;
        var fileReports = new List<MutationFileReport>();

        foreach (var fileProperty in filesElement.EnumerateObject())
        {
            var (report, timeout, fileTotal) = ParseFileEntry(fileProperty.Name, fileProperty.Value);
            fileReports.Add(report);

            totalKilled += report.Killed;
            totalSurvived += report.Survived;
            totalNoCoverage += report.NoCoverage;
            totalTimeout += timeout;
            totalMutants += fileTotal;
        }

        return new MutationReport
        {
            MutationScore = totalMutants > 0 ? (double)totalKilled / totalMutants : 0.0,
            TotalMutants = totalMutants,
            Killed = totalKilled,
            Survived = totalSurvived,
            NoCoverage = totalNoCoverage,
            Timeout = totalTimeout,
            Files = fileReports
        };
    }

    /// <summary>
    /// Searches <paramref name="baseDir"/> for files matching the given glob patterns,
    /// parses the newest match found (by last write time), and returns the result.
    /// Returns <c>null</c> if no matching files are found or <paramref name="globs"/> is null/empty.
    /// </summary>
    private static (MutationFileReport Report, int Timeout, int Total) ParseFileEntry(string filePath, JsonElement fileElement)
    {
        int killed = 0, survived = 0, noCoverage = 0, timeout = 0, total = 0;

        if (fileElement.TryGetProperty("mutants", out var mutantsElement))
        {
            foreach (var mutant in mutantsElement.EnumerateArray())
            {
                total++;
                var status = mutant.TryGetProperty("status", out var s) ? s.GetString() ?? "" : "";
                switch (status)
                {
                    case "Killed": killed++; break;
                    case "Survived": survived++; break;
                    case "NoCoverage": noCoverage++; break;
                    case "Timeout": timeout++; break;
                }
            }
        }

        var score = total > 0 ? (double)killed / total : 0.0;
        return (new MutationFileReport(filePath, score, killed, survived, noCoverage), timeout, total);
    }

    public static MutationReport? TryParseGlobs(string baseDir, List<string>? globs)
    {
        if (globs is null || globs.Count == 0)
            return null;

        var allMatches = new List<string>();

        foreach (var glob in globs)
            allMatches.AddRange(GlobResolver.Resolve(baseDir, glob));

        if (allMatches.Count == 0)
            return null;

        var newest = allMatches
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .First();

        return Parse(newest);
    }
}
