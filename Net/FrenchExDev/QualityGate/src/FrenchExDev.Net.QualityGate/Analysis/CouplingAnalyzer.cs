using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace FrenchExDev.Net.QualityGate.Analysis;

/// <summary>
/// Computes afferent (Ca) and efferent (Ce) coupling at the namespace level
/// and efferent coupling at the type level.
/// </summary>
public static class CouplingAnalyzer
{
    /// <summary>
    /// Returns a dictionary of namespace full-name to (Ca, Ce) coupling counts.
    /// </summary>
    public static async Task<Dictionary<string, (int Ca, int Ce)>> AnalyzeNamespaceCouplingAsync(Project project)
    {
        ArgumentNullException.ThrowIfNull(project);

        var compilation = await project.GetCompilationAsync().ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Could not obtain compilation for project '{project.Name}'.");

        var (typesByNamespace, allProjectTypes) = await CollectTypesByNamespaceAsync(compilation).ConfigureAwait(false);
        var efferentMap = await BuildEfferentMapAsync(compilation, allProjectTypes).ConfigureAwait(false);

        return AggregateByNamespace(typesByNamespace, efferentMap);
    }

    /// <summary>
    /// Counts the distinct external types referenced by the given type symbol.
    /// </summary>
    public static int TypeEfferentCoupling(INamedTypeSymbol type, SemanticModel model, SyntaxNode root)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(root);

        var typeDecl = FindTypeDeclaration(type, model, root);
        if (typeDecl is null)
            return 0;

        var referenced = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

        foreach (var node in typeDecl.DescendantNodes())
        {
            var resolvedType = ResolveTypeSymbol(node, model);
            if (resolvedType is not null && !SymbolEqualityComparer.Default.Equals(resolvedType, type))
                referenced.Add(resolvedType);
        }

        return referenced.Count;
    }

    private static async Task<(Dictionary<string, HashSet<INamedTypeSymbol>> TypesByNs, HashSet<INamedTypeSymbol> AllTypes)>
        CollectTypesByNamespaceAsync(Compilation compilation)
    {
        var typesByNamespace = new Dictionary<string, HashSet<INamedTypeSymbol>>(StringComparer.Ordinal);
        var allProjectTypes = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

        foreach (var syntaxTree in compilation.SyntaxTrees)
        {
            var semanticModel = compilation.GetSemanticModel(syntaxTree);
            var root = await syntaxTree.GetRootAsync().ConfigureAwait(false);

            foreach (var typeDecl in root.DescendantNodes().OfType<TypeDeclarationSyntax>())
            {
                var symbol = semanticModel.GetDeclaredSymbol(typeDecl);
                if (symbol is null)
                    continue;

                allProjectTypes.Add(symbol);

                var ns = symbol.ContainingNamespace?.ToDisplayString() ?? "(global)";
                if (!typesByNamespace.TryGetValue(ns, out var set))
                {
                    set = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
                    typesByNamespace[ns] = set;
                }
                set.Add(symbol);
            }
        }

        return (typesByNamespace, allProjectTypes);
    }

    private static async Task<Dictionary<INamedTypeSymbol, HashSet<INamedTypeSymbol>>> BuildEfferentMapAsync(
        Compilation compilation,
        HashSet<INamedTypeSymbol> projectTypes)
    {
        var efferentMap = new Dictionary<INamedTypeSymbol, HashSet<INamedTypeSymbol>>(SymbolEqualityComparer.Default);

        foreach (var syntaxTree in compilation.SyntaxTrees)
        {
            var semanticModel = compilation.GetSemanticModel(syntaxTree);
            var root = await syntaxTree.GetRootAsync().ConfigureAwait(false);

            foreach (var typeDecl in root.DescendantNodes().OfType<TypeDeclarationSyntax>())
            {
                var symbol = semanticModel.GetDeclaredSymbol(typeDecl);
                if (symbol is null)
                    continue;

                efferentMap[symbol] = CollectReferencedTypes(typeDecl, semanticModel, symbol, projectTypes);
            }
        }

        return efferentMap;
    }

    private static Dictionary<string, (int Ca, int Ce)> AggregateByNamespace(
        Dictionary<string, HashSet<INamedTypeSymbol>> typesByNamespace,
        Dictionary<INamedTypeSymbol, HashSet<INamedTypeSymbol>> efferentMap)
    {
        var result = new Dictionary<string, (int Ca, int Ce)>(StringComparer.Ordinal);

        foreach (var ns in typesByNamespace.Keys)
        {
            var typesInNs = typesByNamespace[ns];
            var ce = CountEfferent(typesInNs, efferentMap, ns);
            var ca = CountAfferent(typesInNs, efferentMap, ns);
            result[ns] = (ca, ce);
        }

        return result;
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage] // Coverlet tracks ?./?? as branches; ContainingNamespace is never null
    private static int CountEfferent(
        HashSet<INamedTypeSymbol> typesInNs,
        Dictionary<INamedTypeSymbol, HashSet<INamedTypeSymbol>> efferentMap,
        string ns)
    {
        var ceSet = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        foreach (var type in typesInNs)
        {
            if (!efferentMap.TryGetValue(type, out var refs))
                continue;

            foreach (var r in refs)
            {
                var rNs = r.ContainingNamespace?.ToDisplayString() ?? "(global)";
                if (!string.Equals(rNs, ns, StringComparison.Ordinal))
                    ceSet.Add(r);
            }
        }
        return ceSet.Count;
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage] // Coverlet tracks ?./?? as branches; ContainingNamespace is never null
    private static int CountAfferent(
        HashSet<INamedTypeSymbol> typesInNs,
        Dictionary<INamedTypeSymbol, HashSet<INamedTypeSymbol>> efferentMap,
        string ns)
    {
        var caSet = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        foreach (var (otherType, refs) in efferentMap)
        {
            var otherNs = otherType.ContainingNamespace?.ToDisplayString() ?? "(global)";
            if (string.Equals(otherNs, ns, StringComparison.Ordinal))
                continue;

            foreach (var r in refs)
            {
                if (typesInNs.Contains(r))
                {
                    caSet.Add(otherType);
                    break;
                }
            }
        }
        return caSet.Count;
    }

    private static TypeDeclarationSyntax? FindTypeDeclaration(INamedTypeSymbol type, SemanticModel model, SyntaxNode root)
    {
        foreach (var node in root.DescendantNodes().OfType<TypeDeclarationSyntax>())
        {
            if (SymbolEqualityComparer.Default.Equals(model.GetDeclaredSymbol(node), type))
                return node;
        }
        return null;
    }

    private static HashSet<INamedTypeSymbol> CollectReferencedTypes(
        TypeDeclarationSyntax typeDecl,
        SemanticModel model,
        INamedTypeSymbol declaringType,
        HashSet<INamedTypeSymbol> projectTypes)
    {
        var referenced = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

        foreach (var node in typeDecl.DescendantNodes())
        {
            var resolvedType = ResolveTypeSymbol(node, model);
            if (resolvedType is null)
                continue;

            if (SymbolEqualityComparer.Default.Equals(resolvedType, declaringType))
                continue;

            if (projectTypes.Contains(resolvedType))
                referenced.Add(resolvedType);
        }

        return referenced;
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage] // Coverlet tracks ?? as branch; CandidateSymbols fallback rarely fires
    private static INamedTypeSymbol? ResolveTypeSymbol(SyntaxNode node, SemanticModel model)
    {
        switch (node)
        {
            case IdentifierNameSyntax:
            case GenericNameSyntax:
            case MemberAccessExpressionSyntax:
                break;
            default:
                return null;
        }

        var symbolInfo = model.GetSymbolInfo(node);
        var symbol = symbolInfo.Symbol ?? symbolInfo.CandidateSymbols.FirstOrDefault();

        return symbol switch
        {
            INamedTypeSymbol namedType => namedType,
            IMethodSymbol method => method.ContainingType,
            IPropertySymbol prop => prop.ContainingType,
            IFieldSymbol field => field.ContainingType,
            IEventSymbol evt => evt.ContainingType,
            _ => null
        };
    }
}
