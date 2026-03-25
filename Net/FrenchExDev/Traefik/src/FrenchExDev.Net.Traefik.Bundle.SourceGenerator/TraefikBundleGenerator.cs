using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using FrenchExDev.Net.Builder.SourceGenerator.Lib;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace FrenchExDev.Net.Traefik.Bundle.SourceGenerator;

[Generator]
public sealed class TraefikBundleGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var schemaFiles = context.AdditionalTextsProvider
            .Where(static f => System.IO.Path.GetFileName(f.Path).StartsWith("traefik-v3-") &&
                               f.Path.EndsWith(".json"));

        context.RegisterSourceOutput(schemaFiles.Collect(), static (ctx, files) =>
        {
            if (files.IsDefaultOrEmpty || files.Length == 0)
                return;

            var ns = "FrenchExDev.Net.Traefik.Bundle";
            Generate(ctx, ns, files);
        });
    }

    private static void Generate(SourceProductionContext ctx, string ns,
        ImmutableArray<AdditionalText> files)
    {
        try
        {
            var allDefinitions = new Dictionary<string, UnifiedDefinition>();
            var staticRootProperties = new List<UnifiedProperty>();
            var dynamicRootProperties = new List<UnifiedProperty>();
            var versions = new HashSet<string>();

            foreach (var file in files)
            {
                ctx.CancellationToken.ThrowIfCancellationRequested();
                var text = file.GetText(ctx.CancellationToken);
                if (text is null) continue;

                var filename = System.IO.Path.GetFileName(file.Path);
                var kind = TraefikSchemaReader.DetectKind(filename);
                var version = TraefikSchemaReader.ExtractVersion(filename);
                versions.Add(version);

                var schema = TraefikSchemaReader.Parse(text.ToString(), version, kind);

                // Collect definitions
                foreach (var kvp in schema.Definitions)
                {
                    allDefinitions[kvp.Key] = new UnifiedDefinition
                    {
                        Name = kvp.Key,
                        Description = kvp.Value.Description,
                        Properties = kvp.Value.Properties
                            .ConvertAll(p => new UnifiedProperty { Property = p }),
                        IsOneOfDiscriminated = kvp.Value.IsOneOfDiscriminated,
                        Branches = kvp.Value.Branches,
                    };
                }

                // Collect root properties
                var rootProps = schema.RootProperties
                    .ConvertAll(p => new UnifiedProperty { Property = p });

                if (kind == SchemaKind.Static)
                    staticRootProperties.AddRange(rootProps);
                else
                    dynamicRootProperties.AddRange(rootProps);
            }

            var unified = new UnifiedSchema
            {
                Versions = versions.OrderBy(v => v).ToList(),
                Definitions = allDefinitions,
                RootProperties = staticRootProperties
                    .Concat(dynamicRootProperties).ToList()
            };

            // Version metadata
            ctx.AddSource("TraefikSchemaVersions.g.cs",
                SourceText.From(VersionMetadataEmitter.Emit(ns, unified), Encoding.UTF8));

            // Static config root model + builder
            if (staticRootProperties.Count > 0)
            {
                ctx.AddSource("TraefikStaticConfig.g.cs",
                    SourceText.From(TraefikModelClassEmitter.EmitRootClass(
                        ns, "TraefikStaticConfig", staticRootProperties), Encoding.UTF8));

                var staticBuilderModel = TraefikBuilderHelper.CreateBuilderModel(
                    ns, "TraefikStaticConfig", staticRootProperties);
                ctx.AddSource("TraefikStaticConfigBuilder.g.cs",
                    SourceText.From(BuilderEmitter.Emit(staticBuilderModel), Encoding.UTF8));
            }

            // Dynamic config root model + builder
            if (dynamicRootProperties.Count > 0)
            {
                ctx.AddSource("TraefikDynamicConfig.g.cs",
                    SourceText.From(TraefikModelClassEmitter.EmitRootClass(
                        ns, "TraefikDynamicConfig", dynamicRootProperties), Encoding.UTF8));

                var dynamicBuilderModel = TraefikBuilderHelper.CreateBuilderModel(
                    ns, "TraefikDynamicConfig", dynamicRootProperties);
                ctx.AddSource("TraefikDynamicConfigBuilder.g.cs",
                    SourceText.From(BuilderEmitter.Emit(dynamicBuilderModel), Encoding.UTF8));
            }

            // Definition models + builders
            var emittedBuilders = new HashSet<string>();
            emittedBuilders.Add("TraefikStaticConfig");
            emittedBuilders.Add("TraefikDynamicConfig");

            foreach (var item in TraefikModelClassEmitter.EmitDefinitions(ns, unified))
            {
                ctx.CancellationToken.ThrowIfCancellationRequested();
                ctx.AddSource(item.FileName, SourceText.From(item.Source, Encoding.UTF8));

                var className = item.FileName;
                if (className.EndsWith(".g.cs"))
                    className = className.Substring(0, className.Length - ".g.cs".Length);

                if (!emittedBuilders.Add(className))
                    continue;

                var builderModel = FindBuilderModel(ns, className, unified);
                if (builderModel is not null)
                {
                    ctx.AddSource($"{className}Builder.g.cs",
                        SourceText.From(BuilderEmitter.Emit(builderModel), Encoding.UTF8));
                }
            }

            // Debug info
            ctx.AddSource("DebugInfo.g.cs",
                SourceText.From(
                    $"// Traefik Bundle Generator: {unified.Definitions.Count} definitions, " +
                    $"{staticRootProperties.Count} static root props, " +
                    $"{dynamicRootProperties.Count} dynamic root props\n",
                    Encoding.UTF8));
        }
        catch (System.Exception ex)
        {
            ctx.AddSource("GenerateError.g.cs",
                SourceText.From(
                    $"// Generator error: {ex.GetType().Name}: {ex.Message}\n// {ex.StackTrace?.Replace("\n", "\n// ")}\n",
                    Encoding.UTF8));
        }
    }

    private static BuilderEmitModel? FindBuilderModel(string ns, string className, UnifiedSchema schema)
    {
        // Check definitions
        foreach (var kvp in schema.Definitions)
        {
            var defClassName = TraefikNamingHelper.DefinitionToClassName(kvp.Key);
            if (defClassName == className)
            {
                if (kvp.Value.IsOneOfDiscriminated && kvp.Value.Branches is not null)
                    return TraefikBuilderHelper.CreateDiscriminatedBuilderModel(ns, className, kvp.Value.Branches);
                return TraefikBuilderHelper.CreateBuilderModel(ns, className, kvp.Value.Properties);
            }
        }

        // Check inline objects recursively
        return FindInlineBuilderModel(ns, className, schema.Definitions, schema.RootProperties);
    }

    private static BuilderEmitModel? FindInlineBuilderModel(
        string ns, string className,
        Dictionary<string, UnifiedDefinition> definitions,
        List<UnifiedProperty> rootProperties)
    {
        foreach (var kvp in definitions)
        {
            var result = SearchPropertiesForInline(ns, className, kvp.Value.Properties);
            if (result is not null) return result;
        }

        var rootResult = SearchPropertiesForInline(ns, className, rootProperties);
        if (rootResult is not null) return rootResult;

        return null;
    }

    private static BuilderEmitModel? SearchPropertiesForInline(
        string ns, string className, List<UnifiedProperty> properties)
    {
        foreach (var up in properties)
        {
            var prop = up.Property;

            // Inline object
            if (prop.InlineClassName == className && prop.InlineObjectProperties is not null)
            {
                var unifiedProps = prop.InlineObjectProperties
                    .ConvertAll(p => new UnifiedProperty { Property = p });
                return TraefikBuilderHelper.CreateBuilderModel(ns, className, unifiedProps);
            }

            // Array item inline object
            if (prop.Items?.InlineClassName == className && prop.Items.InlineObjectProperties is not null)
            {
                var unifiedProps = prop.Items.InlineObjectProperties
                    .ConvertAll(p => new UnifiedProperty { Property = p });
                return TraefikBuilderHelper.CreateBuilderModel(ns, className, unifiedProps);
            }

            // Pattern properties inline
            if (prop.PatternPropsInlineClassName == className && prop.PatternPropsInlineProperties is not null)
            {
                var unifiedProps = prop.PatternPropsInlineProperties
                    .ConvertAll(p => new UnifiedProperty { Property = p });
                return TraefikBuilderHelper.CreateBuilderModel(ns, className, unifiedProps);
            }

            // Recurse
            if (prop.InlineObjectProperties is not null)
            {
                var nestedUnified = prop.InlineObjectProperties
                    .ConvertAll(p => new UnifiedProperty { Property = p });
                var result = SearchPropertiesForInline(ns, className, nestedUnified);
                if (result is not null) return result;
            }

            if (prop.Items?.InlineObjectProperties is not null)
            {
                var nestedUnified = prop.Items.InlineObjectProperties
                    .ConvertAll(p => new UnifiedProperty { Property = p });
                var result = SearchPropertiesForInline(ns, className, nestedUnified);
                if (result is not null) return result;
            }

            if (prop.PatternPropsInlineProperties is not null)
            {
                var nestedUnified = prop.PatternPropsInlineProperties
                    .ConvertAll(p => new UnifiedProperty { Property = p });
                var result = SearchPropertiesForInline(ns, className, nestedUnified);
                if (result is not null) return result;
            }
        }

        return null;
    }
}
