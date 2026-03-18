using FrenchExDev.Net.Builder.SourceGenerator.Lib;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.Collections.Immutable;

using System.Text;
using System.Threading;

namespace FrenchExDev.Net.Builder.SourceGenerator;

[Generator]
public sealed class BuilderGenerator : IIncrementalGenerator
{
    private const string AttributeFullName = "FrenchExDev.Net.Builder.Attributes.BuilderAttribute";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var models = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                AttributeFullName,
                predicate: static (node, _) => node is ClassDeclarationSyntax,
                transform: static (ctx, ct) => GetModel(ctx, ct))
            .Where(static m => m is not null);

        context.RegisterSourceOutput(models, static (ctx, model) =>
        {
            var source = BuilderEmitter.Emit(model!);
            ctx.AddSource($"{model!.TargetClassName}Builder.g.cs", SourceText.From(source, Encoding.UTF8));
        });
    }

    // ── Model extraction (Roslyn → BuilderEmitModel) ─────────────────────────

    private static BuilderEmitModel? GetModel(GeneratorAttributeSyntaxContext ctx, CancellationToken ct)
    {
        if (ctx.TargetSymbol is not INamedTypeSymbol classSymbol)
            return null;

        ct.ThrowIfCancellationRequested();

        // Read optional named arguments from [Builder(Exception = typeof(...), Instantiation = "...")]
        string? exceptionFull = null;
        string instantiation = "init";
        var attr = ctx.Attributes[0];
        foreach (var arg in attr.NamedArguments)
        {
            if (arg.Key == "Exception" && arg.Value.Value is INamedTypeSymbol exType)
            {
                exceptionFull = exType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            }
            else if (arg.Key == "Instantiation" && arg.Value.Value is string instStr)
            {
                instantiation = instStr;
            }
        }

        // Collect public, non-static, settable instance properties
        var properties = new List<BuilderPropertyModel>();
        foreach (var member in classSymbol.GetMembers())
        {
            ct.ThrowIfCancellationRequested();
            if (member is not IPropertySymbol prop) continue;
            if (prop.IsStatic || prop.IsAbstract || prop.IsIndexer) continue;
            if (prop.DeclaredAccessibility != Accessibility.Public) continue;
            if (prop.SetMethod is null) continue;
            if (prop.SetMethod.DeclaredAccessibility != Accessibility.Public) continue;
            if (prop.GetMethod is null) continue;

            var typeFull = prop.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var nullableTypeFull = GetNullableTypeFull(prop.Type, typeFull);
            var (isCollection, itemTypeFull) = GetCollectionInfo(prop.Type);

            properties.Add(new BuilderPropertyModel(prop.Name, typeFull, nullableTypeFull, isCollection, itemTypeFull));
        }

        var ns = classSymbol.ContainingNamespace is { IsGlobalNamespace: false } nsSym
            ? nsSym.ToDisplayString()
            : string.Empty;

        return new BuilderEmitModel(
            ns,
            classSymbol.Name,
            classSymbol.Name + "Builder",
            properties,
            exceptionFull,
            instantiation);
    }

    private static string GetNullableTypeFull(ITypeSymbol type, string typeFull)
    {
        if (type.IsValueType)
        {
            // Already Nullable<T> (e.g. int?) — keep as-is
            if (type is INamedTypeSymbol nt &&
                nt.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
                return typeFull;

            return typeFull + "?";
        }

        // Reference type or array → add ?
        return typeFull + "?";
    }

    private static (bool IsCollection, string? ItemTypeFull) GetCollectionInfo(ITypeSymbol type)
    {
        if (type is IArrayTypeSymbol arr)
            return (true, arr.ElementType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));

        if (type is INamedTypeSymbol named && named.IsGenericType && named.TypeArguments.Length == 1)
        {
            var ns = named.OriginalDefinition.ContainingNamespace?.ToDisplayString() ?? string.Empty;
            var meta = named.OriginalDefinition.MetadataName;

            if (ns == "System.Collections.Generic" &&
                (meta == "IEnumerable`1" || meta == "ICollection`1" || meta == "IList`1" ||
                 meta == "List`1" || meta == "IReadOnlyList`1" || meta == "IReadOnlyCollection`1"))
            {
                return (true, named.TypeArguments[0].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
            }
        }

        return (false, null);
    }
}
