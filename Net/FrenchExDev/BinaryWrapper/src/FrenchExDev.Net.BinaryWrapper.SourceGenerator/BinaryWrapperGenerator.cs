using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Immutable;
using System.Text;
using System.Threading;

namespace FrenchExDev.Net.BinaryWrapper.SourceGenerator;

[Generator]
public sealed class BinaryWrapperGenerator : IIncrementalGenerator
{
    private const string AttributeFullName =
        "FrenchExDev.Net.BinaryWrapper.Attributes.BinaryWrapperAttribute";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Step 1: Find [BinaryWrapper] descriptor classes
        var descriptors = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                AttributeFullName,
                predicate: static (node, _) => node is ClassDeclarationSyntax,
                transform: static (ctx, ct) => GetDescriptorModel(ctx, ct))
            .Where(static m => m is not null);

        // Step 2: Collect AdditionalFiles (JSON help files)
        var jsonFiles = context.AdditionalTextsProvider
            .Where(static f => f.Path.EndsWith(".json", System.StringComparison.OrdinalIgnoreCase));

        // Step 3: Combine descriptors with JSON files
        var combined = descriptors.Combine(jsonFiles.Collect());

        // Step 4: Generate source
        context.RegisterSourceOutput(combined, static (ctx, pair) =>
        {
            var (descriptor, allJsonFiles) = pair;
            if (descriptor is null) return;

            // Find JSON files matching "{binaryName}-*.json" pattern
            var matchingFiles = new System.Collections.Generic.List<AdditionalText>();
            foreach (var file in allJsonFiles)
            {
                var fileName = System.IO.Path.GetFileName(file.Path);
                if (fileName.StartsWith(descriptor.BinaryName + "-", System.StringComparison.OrdinalIgnoreCase)
                    && fileName.EndsWith(".json", System.StringComparison.OrdinalIgnoreCase))
                {
                    matchingFiles.Add(file);
                }
            }

            // Emit diagnostic if no matching files found
            if (matchingFiles.Count == 0)
            {
                ctx.ReportDiagnostic(Diagnostic.Create(
                    Diagnostics.BW001_NoFilesFound,
                    descriptor.Location,
                    descriptor.BinaryName));
                return;
            }

            // Read and parse all JSON files
            var versionedTrees = new System.Collections.Generic.List<(string Version, CommandTreeModel Tree)>();
            foreach (var file in matchingFiles)
            {
                var text = file.GetText(ctx.CancellationToken)?.ToString();
                if (text is null) continue;

                var tree = CommandTreeReader.Parse(text);
                if (tree is null)
                {
                    ctx.ReportDiagnostic(Diagnostic.Create(
                        Diagnostics.BW002_JsonParseError,
                        descriptor.Location,
                        System.IO.Path.GetFileName(file.Path),
                        "Invalid JSON"));
                    continue;
                }

                var fileName = System.IO.Path.GetFileName(file.Path);
                var version = CommandTreeReader.ExtractVersionFromFileName(fileName, descriptor.BinaryName)
                    ?? tree.Version ?? "0.0.0";

                versionedTrees.Add((version, tree));
            }

            if (versionedTrees.Count == 0) return;

            // Compute unified tree (with version diff if multiple versions)
            var unifiedTree = versionedTrees.Count == 1
                ? VersionDiffer.FromSingle(versionedTrees[0].Version, versionedTrees[0].Tree)
                : VersionDiffer.Merge(versionedTrees);

            // Emit descriptor marker (partial class with constants)
            ctx.AddSource(
                $"{descriptor.ClassName}.BinaryWrapper.g.cs",
                SourceText.From(GenerateMarker(descriptor, matchingFiles.Count), Encoding.UTF8));

            // Emit Command + Builder per leaf command
            foreach (var cmd in unifiedTree.Commands)
            {
                var commandClassName = NamingHelper.CommandClassName(descriptor.BinaryName, cmd);
                var builderClassName = NamingHelper.BuilderClassName(descriptor.BinaryName, cmd);

                var commandSource = CommandClassEmitter.Emit(descriptor, cmd);
                ctx.AddSource($"{commandClassName}.g.cs",
                    SourceText.From(commandSource, Encoding.UTF8));

                var builderSource = BuilderClassEmitter.Emit(descriptor, cmd);
                ctx.AddSource($"{builderClassName}.g.cs",
                    SourceText.From(builderSource, Encoding.UTF8));
            }

            // Emit Client
            var clientSource = ClientClassEmitter.Emit(descriptor, unifiedTree);
            ctx.AddSource($"{NamingHelper.ClientClassName(descriptor.BinaryName)}.g.cs",
                SourceText.From(clientSource, Encoding.UTF8));
        });
    }

    private static DescriptorModel? GetDescriptorModel(
        GeneratorAttributeSyntaxContext ctx, CancellationToken ct)
    {
        if (ctx.TargetSymbol is not INamedTypeSymbol classSymbol)
            return null;

        ct.ThrowIfCancellationRequested();

        // Check that the class is partial
        var syntax = ctx.TargetNode as ClassDeclarationSyntax;
        if (syntax is null) return null;

        var isPartial = false;
        foreach (var modifier in syntax.Modifiers)
        {
            if (modifier.ValueText == "partial")
            {
                isPartial = true;
                break;
            }
        }

        if (!isPartial)
        {
            // BW004: non-partial class
            return null;
        }

        // Read attribute arguments
        var attr = ctx.Attributes[0];
        string? binaryName = null;
        string flagPrefix = "--";
        string flagValueSeparator = " ";
        bool useBoolEqualsFormat = false;

        // Constructor arg: binaryName
        if (attr.ConstructorArguments.Length > 0 && attr.ConstructorArguments[0].Value is string bn)
            binaryName = bn;

        // Named args
        foreach (var arg in attr.NamedArguments)
        {
            switch (arg.Key)
            {
                case "FlagPrefix" when arg.Value.Value is string fp:
                    flagPrefix = fp;
                    break;
                case "FlagValueSeparator" when arg.Value.Value is string fvs:
                    flagValueSeparator = fvs;
                    break;
                case "UseBoolEqualsFormat" when arg.Value.Value is bool ube:
                    useBoolEqualsFormat = ube;
                    break;
            }
        }

        if (binaryName is null) return null;

        var ns = classSymbol.ContainingNamespace is { IsGlobalNamespace: false } nsSym
            ? nsSym.ToDisplayString()
            : string.Empty;

        var location = syntax.GetLocation();

        return new DescriptorModel(
            ns, classSymbol.Name, binaryName,
            flagPrefix, flagValueSeparator, useBoolEqualsFormat,
            location);
    }

    private static string GenerateMarker(DescriptorModel model, int jsonFileCount)
    {
        var sb = new StringBuilder(512);
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("// BinaryWrapper source generator marker — Phase 1 skeleton");
        sb.AppendLine($"// Binary: {model.BinaryName}");
        sb.AppendLine($"// FlagPrefix: {model.FlagPrefix}");
        sb.AppendLine($"// JSON files found: {jsonFileCount}");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();

        if (!string.IsNullOrEmpty(model.Namespace))
        {
            sb.AppendLine($"namespace {model.Namespace};");
            sb.AppendLine();
        }

        sb.AppendLine($"partial class {model.ClassName}");
        sb.AppendLine("{");
        sb.AppendLine($"    internal const string __BinaryName = \"{model.BinaryName}\";");
        sb.AppendLine($"    internal const string __FlagPrefix = \"{model.FlagPrefix}\";");
        sb.AppendLine($"    internal const int __JsonFileCount = {jsonFileCount};");
        sb.AppendLine("}");

        return sb.ToString();
    }
}

// ── Models ───────────────────────────────────────────────────────────────────

internal sealed class DescriptorModel
{
    public DescriptorModel(
        string ns, string className, string binaryName,
        string flagPrefix, string flagValueSeparator, bool useBoolEqualsFormat,
        Location? location)
    {
        Namespace = ns;
        ClassName = className;
        BinaryName = binaryName;
        FlagPrefix = flagPrefix;
        FlagValueSeparator = flagValueSeparator;
        UseBoolEqualsFormat = useBoolEqualsFormat;
        Location = location;
    }

    public string Namespace { get; }
    public string ClassName { get; }
    public string BinaryName { get; }
    public string FlagPrefix { get; }
    public string FlagValueSeparator { get; }
    public bool UseBoolEqualsFormat { get; }
    public Location? Location { get; }
}

// ── Diagnostics ──────────────────────────────────────────────────────────────

internal static class Diagnostics
{
    public static readonly DiagnosticDescriptor BW001_NoFilesFound = new(
        id: "BW001",
        title: "No JSON help files found",
        messageFormat: "No AdditionalFiles matching '{0}-*.json' pattern found. Add <AdditionalFiles Include=\"scrape/{0}-*.json\" /> to your project.",
        category: "BinaryWrapper",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor BW002_JsonParseError = new(
        id: "BW002",
        title: "JSON parse error",
        messageFormat: "Failed to parse help file '{0}': {1}",
        category: "BinaryWrapper",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor BW004_NonPartialClass = new(
        id: "BW004",
        title: "Non-partial class",
        messageFormat: "[BinaryWrapper] must be applied to a partial class",
        category: "BinaryWrapper",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);
}
