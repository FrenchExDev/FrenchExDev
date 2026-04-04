using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using FrenchExDev.Net.Builder.SourceGenerator.Lib;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace FrenchExDev.Net.GitLab.Ci.Yaml.SourceGenerator;

[Generator]
public sealed class GitLabCiBundleGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var schemaFiles = context.AdditionalTextsProvider
            .Where(static f => System.IO.Path.GetFileName(f.Path).StartsWith("gitlab-ci-") &&
                               f.Path.EndsWith(".json"));

        context.RegisterSourceOutput(schemaFiles.Collect(), static (ctx, files) =>
        {
            if (files.IsDefaultOrEmpty || files.Length == 0)
                return;

            var ns = "FrenchExDev.Net.GitLab.Ci.Yaml";
            Generate(ctx, ns, files);
        });
    }

    private static void Generate(SourceProductionContext ctx, string ns,
        ImmutableArray<AdditionalText> files)
    {
        try
        {
            var schemas = new List<SchemaModel>();
            foreach (var file in files)
            {
                ctx.CancellationToken.ThrowIfCancellationRequested();
                var text = file.GetText(ctx.CancellationToken);
                if (text is null) continue;

                var version = SchemaReader.ExtractVersion(System.IO.Path.GetFileName(file.Path));
                var schema = SchemaReader.Parse(text.ToString(), version);
                schemas.Add(schema);
            }

            if (schemas.Count == 0)
                return;

            var unified = SchemaVersionMerger.Merge(schemas);

            // Version metadata
            ctx.AddSource("GitLabCiSchemaVersions.g.cs",
                SourceText.From(VersionMetadataEmitter.Emit(ns, unified), Encoding.UTF8));

            // GitLabCiFile model + builder
            ctx.AddSource("GitLabCiFile.g.cs",
                SourceText.From(ModelClassEmitter.EmitGitLabCiFile(ns, unified), Encoding.UTF8));

            var rootBuilderModel = BuilderHelper.CreateRootBuilderModel(ns, "GitLabCiFile", unified.RootProperties);
            ctx.AddSource("GitLabCiFileBuilder.g.cs",
                SourceText.From(BuilderEmitter.Emit(rootBuilderModel), Encoding.UTF8));

            // Definition models + builders + inline class builders
            var emittedClasses = new HashSet<string>();
            emittedClasses.Add("GitLabCiFile");

            foreach (var item in ModelClassEmitter.EmitDefinitions(ns, unified))
            {
                ctx.CancellationToken.ThrowIfCancellationRequested();

                var className = item.FileName;
                if (className.EndsWith(".g.cs"))
                    className = className.Substring(0, className.Length - ".g.cs".Length);

                // Skip duplicate class emissions (e.g., same inline class from allOf-resolved definitions)
                if (!emittedClasses.Add(className))
                    continue;

                ctx.AddSource(item.FileName, SourceText.From(item.Source, Encoding.UTF8));

                var builderModel = FindBuilderModel(ns, className, unified);
                if (builderModel is not null)
                {
                    ctx.AddSource($"{className}Builder.g.cs",
                        SourceText.From(BuilderEmitter.Emit(builderModel), Encoding.UTF8));
                }
            }
        }
        catch (System.Exception ex)
        {
            ctx.AddSource("GenerateError.g.cs",
                SourceText.From($"// Generator error: {ex.GetType().Name}: {ex.Message}\n// {ex.StackTrace?.Replace("\n", "\n// ")}\n", Encoding.UTF8));
        }
    }

    private static BuilderEmitModel? FindBuilderModel(string ns, string className, UnifiedSchema schema)
    {
        // Check top-level definitions
        foreach (var kvp in schema.Definitions)
        {
            var defClassName = NamingHelper.DefinitionToClassName(kvp.Key);
            if (defClassName == className)
                return BuilderHelper.CreateBuilderModel(ns, className, kvp.Value.Properties);
        }

        // Check inline objects
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

            if (prop.InlineClassName == className && prop.InlineObjectProperties is not null)
            {
                var unifiedProps = prop.InlineObjectProperties
                    .ConvertAll(p => new UnifiedProperty { Property = p });
                return BuilderHelper.CreateBuilderModel(ns, className, unifiedProps);
            }

            if (prop.OneOfObjectClassName == className && prop.OneOfObjectProperties is not null)
            {
                var unifiedProps = prop.OneOfObjectProperties
                    .ConvertAll(p => new UnifiedProperty { Property = p });
                return BuilderHelper.CreateBuilderModel(ns, className, unifiedProps);
            }

            if (prop.Items?.InlineClassName == className && prop.Items.InlineObjectProperties is not null)
            {
                var unifiedProps = prop.Items.InlineObjectProperties
                    .ConvertAll(p => new UnifiedProperty { Property = p });
                return BuilderHelper.CreateBuilderModel(ns, className, unifiedProps);
            }

            if (prop.Items?.OneOfObjectClassName == className && prop.Items.OneOfObjectProperties is not null)
            {
                var unifiedProps = prop.Items.OneOfObjectProperties
                    .ConvertAll(p => new UnifiedProperty { Property = p });
                return BuilderHelper.CreateBuilderModel(ns, className, unifiedProps);
            }

            // Recurse
            if (prop.InlineObjectProperties is not null)
            {
                var nestedUnified = prop.InlineObjectProperties
                    .ConvertAll(p => new UnifiedProperty { Property = p });
                var result = SearchPropertiesForInline(ns, className, nestedUnified);
                if (result is not null) return result;
            }

            if (prop.OneOfObjectProperties is not null)
            {
                var nestedUnified = prop.OneOfObjectProperties
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

            if (prop.Items?.OneOfObjectProperties is not null)
            {
                var nestedUnified = prop.Items.OneOfObjectProperties
                    .ConvertAll(p => new UnifiedProperty { Property = p });
                var result = SearchPropertiesForInline(ns, className, nestedUnified);
                if (result is not null) return result;
            }
        }

        return null;
    }
}
