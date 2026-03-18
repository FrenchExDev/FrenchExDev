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
    /// parses the first match found, and returns the result.
    /// Returns <c>null</c> if no matching files are found or <paramref name="globs"/> is null/empty.
    /// </summary>
    public static CoverageReport? TryParseGlobs(string baseDir, List<string>? globs)
    {
        if (globs is null || globs.Count == 0)
            return null;

        foreach (var glob in globs)
        {
            var files = GlobResolver.Resolve(baseDir, glob);
            if (files.Length > 0)
                return Parse(files[0]);
        }

        return null;
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
