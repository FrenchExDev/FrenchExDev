using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Text.Json;
using FrenchExDev.Net.Packer.Bundle.SourceGenerator.Lib;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace FrenchExDev.Net.Packer.Bundle.SourceGenerator;

[Generator]
public sealed class PackerBundleGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var scrapeFiles = context.AdditionalTextsProvider
            .Where(static f => f.Path.EndsWith(".json") &&
                               !System.IO.Path.GetFileName(f.Path).StartsWith("packer-"));

        context.RegisterSourceOutput(scrapeFiles.Collect(), static (ctx, files) =>
        {
            if (files.IsDefaultOrEmpty || files.Length == 0)
                return;

            var ns = "FrenchExDev.Net.Packer.Bundle";
            Generate(ctx, ns, files);
        });
    }

    private static void Generate(SourceProductionContext ctx, string ns,
        ImmutableArray<AdditionalText> files)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        foreach (var file in files)
        {
            ctx.CancellationToken.ThrowIfCancellationRequested();

            var text = file.GetText(ctx.CancellationToken);
            if (text is null) continue;

            ScrapePluginModel? model;
            try
            {
                model = JsonSerializer.Deserialize<ScrapePluginModel>(text.ToString(), options);
            }
            catch
            {
                continue;
            }

            if (model is null || string.IsNullOrEmpty(model.TypeName))
                continue;

            var className = NamingHelper.ToClassName(model.TypeName, model.PluginKind);
            var source = PluginTypeEmitter.EmitRecord(ns, model);

            ctx.AddSource($"{className}.g.cs",
                SourceText.From(source, Encoding.UTF8));
        }
    }
}
