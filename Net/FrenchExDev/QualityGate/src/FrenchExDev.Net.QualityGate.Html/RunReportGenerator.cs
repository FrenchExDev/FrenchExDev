using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using FrenchExDev.Net.QualityGate.Model;
using Scriban;

namespace FrenchExDev.Net.QualityGate.Html;

[ExcludeFromCodeCoverage]
public static class RunReportGenerator
{
    public static void Generate(QualityReport report, string outputPath)
    {
        var assembly = typeof(RunReportGenerator).Assembly;
        using var stream = assembly.GetManifestResourceStream(
            "FrenchExDev.Net.QualityGate.Html.Templates.run-dashboard.html")
            ?? throw new InvalidOperationException(
                "Embedded resource 'Templates/run-dashboard.html' not found.");

        using var reader = new StreamReader(stream);
        var templateText = reader.ReadToEnd();

        var template = Template.Parse(templateText);
        if (template.HasErrors)
        {
            throw new InvalidOperationException(
                $"Template parse errors: {string.Join("; ", template.Messages)}");
        }

        var result = template.Render(new { report }, member => member.Name);

        var dir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        File.WriteAllText(outputPath, result);
    }
}
