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
    private const string AttributeFullNameGlobal = "global::" + AttributeFullName;

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

        var (exceptionFull, instantiation) = ReadBuilderAttributeArgs(ctx.Attributes[0]);

        var properties = new List<BuilderPropertyModel>();
        foreach (var member in classSymbol.GetMembers())
        {
            ct.ThrowIfCancellationRequested();
            var model = TryClassifyProperty(member);
            if (model is not null)
                properties.Add(model);
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

    private static (string? ExceptionFull, string Instantiation) ReadBuilderAttributeArgs(AttributeData attr)
    {
        string? exceptionFull = null;
        var instantiation = "init";
        foreach (var arg in attr.NamedArguments)
        {
            if (arg.Key == "Exception" && arg.Value.Value is INamedTypeSymbol exType)
                exceptionFull = exType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            else if (arg.Key == "Instantiation" && arg.Value.Value is string instStr)
                instantiation = instStr;
        }
        return (exceptionFull, instantiation);
    }

    // ── Property classification ─────────────────────────────────────────────

    private static BuilderPropertyModel? TryClassifyProperty(ISymbol member)
    {
        if (member is not IPropertySymbol prop) return null;
        if (prop.IsStatic || prop.IsAbstract || prop.IsIndexer) return null;
        if (prop.DeclaredAccessibility != Accessibility.Public) return null;
        if (prop.SetMethod is null || prop.SetMethod.DeclaredAccessibility != Accessibility.Public) return null;
        if (prop.GetMethod is null) return null;

        var typeFull = prop.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var nullableTypeFull = GetNullableTypeFull(prop.Type, typeFull);
        var (isCollection, itemTypeFull, itemSymbol) = GetCollectionInfo(prop.Type);
        var (isDict, dictKeyFull, dictValueFull, dictValueSymbol) = GetDictionaryInfo(prop.Type);

        var (itemBuilderClassName, collectionSingularName) = ResolveCollectionOverload(prop.Type, prop.Name, isCollection, itemTypeFull, itemSymbol);
        var (dictValueBuilderClassName, dictSingularName) = ResolveDictionaryOverload(prop.Name, isDict, dictValueSymbol);

        return new BuilderPropertyModel(
            prop.Name, typeFull, nullableTypeFull,
            isCollection, itemTypeFull,
            isDictionary: isDict,
            dictKeyTypeFull: dictKeyFull,
            dictValueTypeFull: dictValueFull,
            dictValueBuilderClassName: dictValueBuilderClassName,
            dictSingularName: dictSingularName,
            itemBuilderClassName: itemBuilderClassName,
            collectionSingularName: collectionSingularName);
    }

    private static (string? ItemBuilderClassName, string? CollectionSingularName) ResolveCollectionOverload(
        ITypeSymbol propertyType, string propertyName, bool isCollection, string? itemTypeFull, ITypeSymbol? itemSymbol)
    {
        if (!isCollection || itemTypeFull is null)
            return (null, null);

        string? itemBuilderClassName = null;
        if (itemSymbol is not null && HasBuilderAttribute(itemSymbol))
            itemBuilderClassName = itemSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) + "Builder";

        // Tier 1 (simple Add) requires mutable collection; Tier 2 (ListBuilder) works for all
        var isMutable = IsMutableCollection(propertyType);
        var singularName = (itemBuilderClassName is not null || isMutable) ? Singularize(propertyName) : null;

        return (itemBuilderClassName, singularName);
    }

    private static (string? DictValueBuilderClassName, string? DictSingularName) ResolveDictionaryOverload(
        string propertyName, bool isDict, ITypeSymbol? dictValueSymbol)
    {
        if (!isDict)
            return (null, null);

        string? dictValueBuilderClassName = null;
        if (dictValueSymbol is not null && HasBuilderAttribute(dictValueSymbol))
            dictValueBuilderClassName = dictValueSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) + "Builder";

        return (dictValueBuilderClassName, Singularize(propertyName));
    }

    // ── Type analysis helpers ───────────────────────────────────────────────

    private static string GetNullableTypeFull(ITypeSymbol type, string typeFull)
    {
        if (type.IsValueType)
        {
            if (type is INamedTypeSymbol nt &&
                nt.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
                return typeFull;

            return typeFull + "?";
        }

        return typeFull + "?";
    }

    private static readonly HashSet<string> CollectionMetadataNames = new()
    {
        "IEnumerable`1", "ICollection`1", "IList`1",
        "List`1", "IReadOnlyList`1", "IReadOnlyCollection`1"
    };

    private static (bool IsCollection, string? ItemTypeFull, ITypeSymbol? ItemSymbol) GetCollectionInfo(ITypeSymbol type)
    {
        if (type is IArrayTypeSymbol arr)
            return (true, arr.ElementType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), arr.ElementType);

        if (type is INamedTypeSymbol named && named.IsGenericType && named.TypeArguments.Length == 1 &&
            IsSystemCollectionsGeneric(named) && CollectionMetadataNames.Contains(named.OriginalDefinition.MetadataName))
        {
            var itemSymbol = named.TypeArguments[0];
            return (true, itemSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), itemSymbol);
        }

        return (false, null, null);
    }

    private static (bool IsDictionary, string? KeyTypeFull, string? ValueTypeFull, ITypeSymbol? ValueSymbol) GetDictionaryInfo(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol named && named.IsGenericType && named.TypeArguments.Length == 2)
        {
            var meta = named.OriginalDefinition.MetadataName;
            if (IsSystemCollectionsGeneric(named) &&
                (meta == "Dictionary`2" || meta == "IDictionary`2" || meta == "IReadOnlyDictionary`2"))
            {
                var keyFull = named.TypeArguments[0].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                var valueFull = named.TypeArguments[1].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                return (true, keyFull, valueFull, named.TypeArguments[1]);
            }
        }

        return (false, null, null, null);
    }

    private static bool IsSystemCollectionsGeneric(INamedTypeSymbol type)
        => type.OriginalDefinition.ContainingNamespace?.ToDisplayString() == "System.Collections.Generic";

    private static bool IsMutableCollection(ITypeSymbol type)
    {
        if (type is not INamedTypeSymbol named) return false;
        if (!IsSystemCollectionsGeneric(named)) return false;
        var meta = named.OriginalDefinition.MetadataName;
        return meta == "List`1" || meta == "IList`1" || meta == "ICollection`1";
    }

    private static bool HasBuilderAttribute(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol { IsGenericType: true } nullable &&
            nullable.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
        {
            type = nullable.TypeArguments[0];
        }

        foreach (var a in type.GetAttributes())
        {
            if (a.AttributeClass?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == AttributeFullNameGlobal)
                return true;
        }
        return false;
    }

    private static string? Singularize(string name)
    {
        if (name.Length > 1 && name.EndsWith("s"))
            return name.Substring(0, name.Length - 1);
        return null;
    }
}
