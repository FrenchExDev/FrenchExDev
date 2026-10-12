using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace FrenchExDev.Net.QualityGate.Html;

[ExcludeFromCodeCoverage] // Coverlet tracks ?? throw on embedded resources as uncovered branch
public static class IndexGenerator
{
    private static readonly string[] ResourceFiles = ["index.html", "app.js", "style.css"];

    public static void Generate(string outputDir)
    {
        Directory.CreateDirectory(outputDir);

        var assembly = typeof(RunReportGenerator).Assembly;

        foreach (var fileName in ResourceFiles)
        {
            var resourceName = $"FrenchExDev.Net.QualityGate.Html.Templates.{fileName}";
            using var stream = assembly.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException(
                    $"Embedded resource 'Templates/{fileName}' not found.");

            var outputPath = Path.Combine(outputDir, fileName);
            using var fileStream = File.Create(outputPath);
            stream.CopyTo(fileStream);
        }
    }
}
