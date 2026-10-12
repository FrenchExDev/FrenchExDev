using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using FrenchExDev.Net.Builder.SourceGenerator.Lib;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace FrenchExDev.Net.GitLab.DockerCompose.SourceGenerator;

[Generator]
public sealed class GitLabOmnibusGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var rbFiles = context.AdditionalTextsProvider
            .Where(static f =>
            {
                var name = System.IO.Path.GetFileName(f.Path);
                return name.StartsWith("gitlab-") && name.EndsWith(".rb");
            });

        context.RegisterSourceOutput(rbFiles.Collect(), static (ctx, files) =>
        {
            if (files.IsDefaultOrEmpty || files.Length == 0)
                return;

            var ns = "FrenchExDev.Net.GitLab.DockerCompose";
            Generate(ctx, ns, files);
        });
    }

    private static void Generate(SourceProductionContext ctx, string ns,
        ImmutableArray<AdditionalText> files)
    {
        try
        {
            // 1. Parse each versioned .rb file
            var models = new List<GitLabRbModel>();
            foreach (var file in files)
            {
                ctx.CancellationToken.ThrowIfCancellationRequested();
                var text = file.GetText(ctx.CancellationToken);
                if (text is null) continue;

                var version = GitLabRbParser.ExtractVersion(System.IO.Path.GetFileName(file.Path));
                var model = GitLabRbParser.Parse(text.ToString(), version);
                models.Add(model);
            }

            if (models.Count == 0) return;

            // 2. Merge across versions
            var unified = GitLabRbVersionMerger.Merge(models);

            // 3. Emit version metadata + attributes
            ctx.AddSource("GitLabOmnibusVersions.g.cs",
                SourceText.From(GitLabConfigEmitter.EmitVersionMetadata(ns, unified), Encoding.UTF8));

            // 4. Emit per-prefix config classes + builders
            var metadataEntries = new List<GitLabConfigEmitter.MetadataEntry>();
            var rootProperties = new List<GitLabConfigEmitter.PropertyInfo>();

            foreach (var prefixGroup in unified.PrefixGroups)
            {
                ctx.CancellationToken.ThrowIfCancellationRequested();

                var className = NamingHelper.PrefixToClassName(prefixGroup.Prefix);
                EmitClassAndBuilder(ctx, ns, className, prefixGroup.Root,
                    prefixGroup.Prefix, new List<string>(), metadataEntries,
                    prefixGroup.SinceVersion, prefixGroup.UntilVersion);

                rootProperties.Add(new GitLabConfigEmitter.PropertyInfo
                {
                    Name = NamingHelper.ToPascalCase(prefixGroup.Prefix),
                    CSharpType = className + "?",
                    SinceVersion = prefixGroup.SinceVersion,
                    UntilVersion = prefixGroup.UntilVersion,
                });
            }

            // 5. Emit standalone URL properties on root
            foreach (var url in unified.StandaloneUrls)
            {
                rootProperties.Insert(0, new GitLabConfigEmitter.PropertyInfo
                {
                    Name = NamingHelper.ToPascalCase(url.Url.RubyKey),
                    CSharpType = "string?",
                    DocComment = url.Url.RubyKey + " '" + (url.Url.ExampleValue ?? "") + "'",
                    SinceVersion = url.SinceVersion,
                    UntilVersion = url.UntilVersion,
                });
            }

            // 6. Emit root GitLabOmnibusConfig + builder
            var rootNode = new UnifiedObjectNode { Name = "root" };
            ctx.AddSource("GitLabOmnibusConfig.g.cs",
                SourceText.From(GitLabConfigEmitter.EmitModelClass(
                    ns, "GitLabOmnibusConfig", rootNode, rootProperties, null, null), Encoding.UTF8));

            var rootBuilderProps = new List<BuilderPropertyModel>();
            foreach (var prop in rootProperties)
            {
                if (prop.CSharpType.EndsWith("Config?"))
                {
                    // Sub-object with builder
                    var subBuilderName = prop.CSharpType.TrimEnd('?') + "Builder";
                    rootBuilderProps.Add(new BuilderPropertyModel(
                        prop.Name, prop.CSharpType, prop.CSharpType,
                        withMethodAttributes: BuildVersionAttributes(prop.SinceVersion, prop.UntilVersion),
                        itemBuilderClassName: subBuilderName));
                }
                else
                {
                    rootBuilderProps.Add(new BuilderPropertyModel(
                        prop.Name, prop.CSharpType, prop.CSharpType,
                        withMethodAttributes: BuildVersionAttributes(prop.SinceVersion, prop.UntilVersion)));
                }
            }

            var rootBuilderModel = new BuilderEmitModel(ns, "GitLabOmnibusConfig",
                "GitLabOmnibusConfigBuilder", rootBuilderProps);
            ctx.AddSource("GitLabOmnibusConfigBuilder.g.cs",
                SourceText.From(BuilderEmitter.Emit(rootBuilderModel), Encoding.UTF8));

            // 7. Emit rendering metadata
            var standaloneKeys = unified.StandaloneUrls.Select(u => u.Url.RubyKey).ToList();
            ctx.AddSource("GitLabRbMetadata.g.cs",
                SourceText.From(GitLabConfigEmitter.EmitRbMetadata(ns, metadataEntries, standaloneKeys), Encoding.UTF8));
        }
        catch (Exception ex)
        {
            ctx.AddSource("GenerateError.g.cs",
                SourceText.From($"// Generator error: {ex.GetType().Name}: {ex.Message}\n// {ex.StackTrace?.Replace("\n", "\n// ")}\n", Encoding.UTF8));
        }
    }

    private static void EmitClassAndBuilder(
        SourceProductionContext ctx, string ns, string className,
        UnifiedObjectNode node, string prefix, List<string> keyPath,
        List<GitLabConfigEmitter.MetadataEntry> metadataEntries,
        string? sinceVersion, string? untilVersion)
    {
        var properties = new List<GitLabConfigEmitter.PropertyInfo>();
        var builderProps = new List<BuilderPropertyModel>();

        foreach (var kvp in node.Children.OrderBy(k => k.Key))
        {
            var childKey = kvp.Key;
            var child = kvp.Value;
            var propName = NamingHelper.ToPascalCase(childKey);
            var childKeyPath = new List<string>(keyPath) { childKey };

            if (child.LeafType != null && child.Children.Count == 0)
            {
                // Leaf node → simple property
                var csharpType = NamingHelper.MapCSharpType(child.LeafType.Value);

                properties.Add(new GitLabConfigEmitter.PropertyInfo
                {
                    Name = propName,
                    CSharpType = csharpType,
                    DocComment = child.DocComment ?? $"{prefix}['{string.Join("']['", childKeyPath)}']",
                    SinceVersion = child.SinceVersion,
                    UntilVersion = child.UntilVersion,
                });

                var versionAttrs = BuildVersionAttributes(child.SinceVersion, child.UntilVersion);

                builderProps.Add(new BuilderPropertyModel(
                    propName, csharpType, csharpType,
                    isCollection: IsCollectionType(csharpType),
                    itemTypeFull: ExtractItemType(csharpType),
                    withMethodAttributes: versionAttrs,
                    isDictionary: IsDictionaryType(csharpType),
                    dictKeyTypeFull: IsDictionaryType(csharpType) ? "string" : null,
                    dictValueTypeFull: IsDictionaryType(csharpType) ? "string?" : null));

                // Metadata for renderer
                metadataEntries.Add(new GitLabConfigEmitter.MetadataEntry
                {
                    Prefix = prefix,
                    KeyPath = childKeyPath,
                    ValueKind = MapValueKind(child.LeafType.Value),
                });
            }
            else if (child.IsArrayOfObjects)
            {
                // Array of objects → List<SubClass>
                var itemClassName = className + propName + "Item";
                EmitClassAndBuilder(ctx, ns, itemClassName, child, prefix, childKeyPath,
                    metadataEntries, child.SinceVersion, child.UntilVersion);

                var listType = $"global::System.Collections.Generic.List<{itemClassName}>?";

                properties.Add(new GitLabConfigEmitter.PropertyInfo
                {
                    Name = propName,
                    CSharpType = listType,
                    DocComment = child.DocComment,
                    SinceVersion = child.SinceVersion,
                    UntilVersion = child.UntilVersion,
                });

                builderProps.Add(new BuilderPropertyModel(
                    propName, listType, listType,
                    isCollection: true,
                    itemTypeFull: itemClassName,
                    itemBuilderClassName: itemClassName + "Builder",
                    collectionSingularName: propName.EndsWith("s") ? propName.Substring(0, propName.Length - 1) : propName,
                    withMethodAttributes: BuildVersionAttributes(child.SinceVersion, child.UntilVersion)));

                metadataEntries.Add(new GitLabConfigEmitter.MetadataEntry
                {
                    Prefix = prefix,
                    KeyPath = childKeyPath,
                    ValueKind = "ArrayOfObjects",
                });
            }
            else
            {
                // Branch node → sub-class
                var subClassName = className + propName;
                EmitClassAndBuilder(ctx, ns, subClassName, child, prefix, childKeyPath,
                    metadataEntries, child.SinceVersion, child.UntilVersion);

                properties.Add(new GitLabConfigEmitter.PropertyInfo
                {
                    Name = propName,
                    CSharpType = subClassName + "?",
                    DocComment = child.DocComment,
                    SinceVersion = child.SinceVersion,
                    UntilVersion = child.UntilVersion,
                });

                builderProps.Add(new BuilderPropertyModel(
                    propName, subClassName + "?", subClassName + "?",
                    withMethodAttributes: BuildVersionAttributes(child.SinceVersion, child.UntilVersion),
                    itemBuilderClassName: subClassName + "Builder"));

                metadataEntries.Add(new GitLabConfigEmitter.MetadataEntry
                {
                    Prefix = prefix,
                    KeyPath = childKeyPath,
                    ValueKind = "SubObject",
                });
            }
        }

        // Emit model class
        ctx.AddSource($"{className}.g.cs",
            SourceText.From(GitLabConfigEmitter.EmitModelClass(
                ns, className, node, properties, sinceVersion, untilVersion), Encoding.UTF8));

        // Emit builder class via BuilderEmitter
        if (builderProps.Count > 0)
        {
            var builderModel = new BuilderEmitModel(ns, className, className + "Builder", builderProps);
            ctx.AddSource($"{className}Builder.g.cs",
                SourceText.From(BuilderEmitter.Emit(builderModel), Encoding.UTF8));
        }
    }

    private static List<string>? BuildVersionAttributes(string? sinceVersion, string? untilVersion)
    {
        if (sinceVersion is null && untilVersion is null)
            return null;

        var attrs = new List<string>();
        if (sinceVersion is not null)
            attrs.Add($"[SinceVersion(\"{sinceVersion}\")]");
        if (untilVersion is not null)
            attrs.Add($"[UntilVersion(\"{untilVersion}\")]");
        return attrs;
    }

    private static bool IsCollectionType(string type) =>
        type.StartsWith("global::System.Collections.Generic.List<");

    private static bool IsDictionaryType(string type) =>
        type.StartsWith("global::System.Collections.Generic.Dictionary<");

    private static string? ExtractItemType(string type)
    {
        if (!IsCollectionType(type)) return null;
        const string prefix = "global::System.Collections.Generic.List<";
        var inner = type.Substring(prefix.Length);
        var end = inner.LastIndexOf('>');
        return end > 0 ? inner.Substring(0, end) : null;
    }

    private static string MapValueKind(GitLabRbValueType type)
    {
        switch (type)
        {
            case GitLabRbValueType.String: return "String";
            case GitLabRbValueType.Integer: return "Integer";
            case GitLabRbValueType.Long: return "Long";
            case GitLabRbValueType.Boolean: return "Boolean";
            case GitLabRbValueType.Float: return "Float";
            case GitLabRbValueType.StringList: return "StringList";
            case GitLabRbValueType.FloatList: return "FloatList";
            case GitLabRbValueType.StringDict: return "StringDict";
            case GitLabRbValueType.Nil: return "Nil";
            default: return "String";
        }
    }
}
