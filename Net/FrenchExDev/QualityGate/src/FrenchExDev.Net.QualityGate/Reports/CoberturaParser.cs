using System.Globalization;
using System.Xml.Linq;

using FrenchExDev.Net.QualityGate.Model;

namespace FrenchExDev.Net.QualityGate.Reports;

/// <summary>
/// Parses Cobertura XML coverage reports into <see cref="CoverageReport"/> instances.
/// </summary>
public static class CoberturaParser
{
    /// <summary>
    /// Parses a Cobertura XML file at <paramref name="xmlPath"/> and returns a <see cref="CoverageReport"/>.
    /// </summary>
    public static CoverageReport Parse(string xmlPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xmlPath);

        var doc = XDocument.Load(xmlPath);
        var root = GetRoot(doc, xmlPath);

        var lineRate = ParseDouble(root.Attribute("line-rate")?.Value, "line-rate");
        var branchRate = ParseDouble(root.Attribute("branch-rate")?.Value, "branch-rate");

        var classes = new List<CoverageClass>();

        // <coverage><packages><package><classes><class ...>
        foreach (var classElement in root.Descendants("class"))
        {
            var name = classElement.Attribute("name")?.Value ?? "(unknown)";
            var fileName = classElement.Attribute("filename")?.Value ?? "";
            var classLineRate = ParseDouble(classElement.Attribute("line-rate")?.Value, "class line-rate");
            var classBranchRate = ParseDouble(classElement.Attribute("branch-rate")?.Value, "class branch-rate");

            classes.Add(new CoverageClass(name, fileName, classLineRate, classBranchRate));
        }

        return new CoverageReport
        {
            LineRate = lineRate,
            BranchRate = branchRate,
            Classes = classes
        };
    }

    /// <summary>
    /// Searches <paramref name="baseDir"/> for files matching the given glob patterns,
    /// parses ALL matches, and merges them into a single report.
    /// Per-class coverage is merged by taking the best (max) line/branch rate across all reports.
    /// Returns <c>null</c> if no matching files are found or <paramref name="globs"/> is null/empty.
    /// </summary>
    public static CoverageReport? TryParseGlobs(string baseDir, List<string>? globs)
    {
        if (globs is null || globs.Count == 0)
            return null;

        var allFiles = new List<string>();
        foreach (var glob in globs)
        {
            var files = GlobResolver.Resolve(baseDir, glob);
            allFiles.AddRange(files);
        }

        if (allFiles.Count == 0)
            return null;

        if (allFiles.Count == 1)
            return Parse(allFiles[0]);

        return MergeReports(allFiles.Select(Parse).ToList());
    }

    /// <summary>
    /// Merges multiple coverage reports by taking the best (max) coverage per class.
    /// </summary>
    private static CoverageReport MergeReports(List<CoverageReport> reports)
    {
        var classByKey = new Dictionary<string, CoverageClass>();

        foreach (var report in reports)
        {
            foreach (var cls in report.Classes)
            {
                var key = $"{cls.FileName}:{cls.Name}";
                if (classByKey.TryGetValue(key, out var existing))
                {
                    // Take the best coverage for each class across all reports
                    classByKey[key] = new CoverageClass(
                        cls.Name,
                        cls.FileName,
                        Math.Max(existing.LineRate, cls.LineRate),
                        Math.Max(existing.BranchRate, cls.BranchRate));
                }
                else
                {
                    classByKey[key] = cls;
                }
            }
        }

        var mergedClasses = classByKey.Values.ToList();
        var totalLineRate = mergedClasses.Count > 0
            ? mergedClasses.Average(c => c.LineRate)
            : 0.0;
        var totalBranchRate = mergedClasses.Count > 0
            ? mergedClasses.Average(c => c.BranchRate)
            : 0.0;

        return new CoverageReport
        {
            LineRate = totalLineRate,
            BranchRate = totalBranchRate,
            Classes = mergedClasses
        };
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage] // XDocument.Load always produces non-null Root for valid XML
    private static XElement GetRoot(XDocument doc, string xmlPath)
        => doc.Root ?? throw new InvalidOperationException($"Cobertura XML file '{xmlPath}' has no root element.");

    private static double ParseDouble(string? value, string attributeName)
    {
        if (value is null)
            return 0.0;

        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
            return result;

        throw new FormatException($"Could not parse Cobertura attribute '{attributeName}' value '{value}' as a number.");
    }
}
