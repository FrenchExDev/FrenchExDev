using System.Collections.Generic;
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
        // Stage 1: filter the AdditionalTexts to schemas we care about, then
        // parse each one to a SchemaModel inside a Select so Roslyn can cache
        // the parsed result. SchemaModel implements structural equality, so
        // editing whitespace in a .json schema doesn't bust the downstream
        // emit cache when the parsed shape is identical.
        var parsedSchemas = context.AdditionalTextsProvider
            .Where(static f => System.IO.Path.GetFileName(f.Path).StartsWith("traefik-v") &&
                               f.Path.EndsWith(".json"))
            .Select(static (file, ct) =>
            {
                var text = file.GetText(ct);
                if (text is null) return null;
                var filename = System.IO.Path.GetFileName(file.Path);
                var kind = TraefikSchemaReader.DetectKind(filename);
                var version = TraefikSchemaReader.ExtractVersion(filename);
                try
                {
                    return TraefikSchemaReader.Parse(text.ToString(), version, kind);
                }
                catch
                {
                    return null;
                }
            });

        // Stage 2: collect + merge into the UnifiedSchema. Also value-equal,
        // so this stage caches too. Schemas are sorted by version so that
        // properties first introduced in a later version get a SinceVersion
        // marker, and properties dropped in a later version get UntilVersion.
        var unifiedSchema = parsedSchemas.Collect().Select(static (schemas, ct) =>
        {
            var ordered = schemas
                .Where(static s => s is not null)
                .OrderBy(static s => s!.Version, System.StringComparer.Ordinal)
                .ToList();

            var allDefinitions = new Dictionary<string, UnifiedDefinition>();
            var staticRootProperties = new List<UnifiedProperty>();
            var dynamicRootProperties = new List<UnifiedProperty>();
            var versions = new HashSet<string>();

            // Tracks the version a (definitionName, propertyJsonName) pair
            // first appeared in. Used to stamp SinceVersion when later
            // versions add new properties.
            var firstSeen = new Dictionary<(string Def, string Prop), string>();

            foreach (var schema in ordered)
            {
                ct.ThrowIfCancellationRequested();
                if (schema is null) continue;

                versions.Add(schema.Version);

                foreach (var kvp in schema.Definitions)
                {
                    // Union merge: existing properties are preserved across
                    // schema versions; new ones are appended and stamped with
                    // SinceVersion when they first appear after the earliest
                    // loaded schema.
                    if (!allDefinitions.TryGetValue(kvp.Key, out var existing))
                    {
                        existing = new UnifiedDefinition
                        {
                            Name = kvp.Key,
                            Description = kvp.Value.Description,
                            Properties = new List<UnifiedProperty>(),
                            IsOneOfDiscriminated = kvp.Value.IsOneOfDiscriminated,
                            Branches = kvp.Value.Branches,
                        };
                        allDefinitions[kvp.Key] = existing;
                    }
                    else if (kvp.Value.Description is { Length: > 0 } d && existing.Description is null)
                    {
                        existing.Description = d;
                    }

                    var existingByName = new HashSet<string>();
                    foreach (var ep in existing.Properties) existingByName.Add(ep.Property.JsonName);

                    foreach (var p in kvp.Value.Properties)
                    {
                        var key = (kvp.Key, p.JsonName);
                        if (!firstSeen.ContainsKey(key))
                        {
                            firstSeen[key] = schema.Version;
                        }
                        if (existingByName.Contains(p.JsonName))
                        {
                            continue; // already merged from an earlier version
                        }
                        var stampSince = firstSeen[key] != ordered[0]!.Version;
                        existing.Properties.Add(new UnifiedProperty
                        {
                            Property = p,
                            SinceVersion = stampSince ? firstSeen[key] : null,
                        });
                    }
                }

                var rootProps = schema.RootProperties
                    .ConvertAll(p => new UnifiedProperty { Property = p });

                if (schema.Kind == SchemaKind.Static)
                    staticRootProperties.AddRange(rootProps);
                else
                    dynamicRootProperties.AddRange(rootProps);
            }

            return new UnifiedSchema
            {
                Versions = versions.OrderBy(static v => v, System.StringComparer.Ordinal).ToList(),
                Definitions = allDefinitions,
                RootProperties = staticRootProperties
                    .Concat(dynamicRootProperties).ToList()
            };
        });

        // Stage 3: emit. Only re-runs when the UnifiedSchema's structural
        // equality differs from the previously cached one.
        context.RegisterSourceOutput(unifiedSchema, static (ctx, unified) =>
        {
            if (unified.Definitions.Count == 0 && unified.RootProperties.Count == 0)
            {
                // TFK004: no schemas were wired in. The consumer's csproj is
                // missing <AdditionalFiles Include="schemas\traefik-v3-*.json" />.
                ctx.ReportDiagnostic(Diagnostic.Create(
                    Analyzers.TraefikDiagnostics.NoSchemasFound,
                    Location.None));
                return;
            }

            var ns = "FrenchExDev.Net.Traefik.Bundle";
            Emit(ctx, ns, unified);
        });
    }

    private static void Emit(SourceProductionContext ctx, string ns, UnifiedSchema unified)
    {
        try
        {
            // Partition root properties back into static vs dynamic by looking
            // at where they came from. Today the merge step concatenates them
            // (static first, dynamic last), so we re-split using the section
            // class-name prefix the dynamic parser stamps on every section.
            var staticRootProperties = new List<UnifiedProperty>();
            var dynamicRootProperties = new List<UnifiedProperty>();
            foreach (var up in unified.RootProperties)
            {
                if (up.Property.InlineClassName is { } cn && cn.StartsWith("TraefikDynamic"))
                    dynamicRootProperties.Add(up);
                else
                    staticRootProperties.Add(up);
            }

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
