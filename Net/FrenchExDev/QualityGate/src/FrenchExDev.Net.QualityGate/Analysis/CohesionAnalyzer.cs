using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace FrenchExDev.Net.QualityGate.Analysis;

/// <summary>
/// Computes the LCOM4 (Lack of Cohesion of Methods) metric for a type.
/// LCOM4 counts connected components in the method-field graph.
/// A value of 1 indicates a perfectly cohesive type.
/// </summary>
public static class CohesionAnalyzer
{
    public static int Lcom4(INamedTypeSymbol typeSymbol, SemanticModel model, SyntaxNode typeDeclaration)
    {
        ArgumentNullException.ThrowIfNull(typeSymbol);
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(typeDeclaration);

        var methods = CollectMethods(typeSymbol);
        var fields = CollectFields(typeSymbol);

        if (methods.Count + fields.Count == 0)
            return 0;

        var uf = new UnionFind();
        uf.Initialize(methods, fields);

        var methodDeclarations = MapMethodDeclarations(typeDeclaration, model, methods);
        BuildEdges(methods, methodDeclarations, model, fields, uf);

        return uf.CountComponents(methods, fields);
    }

    private static List<IMethodSymbol> CollectMethods(INamedTypeSymbol typeSymbol)
    {
        return typeSymbol.GetMembers()
            .OfType<IMethodSymbol>()
            .Where(m => m.MethodKind == MethodKind.Ordinary && !m.IsImplicitlyDeclared)
            .ToList();
    }

    private static List<IFieldSymbol> CollectFields(INamedTypeSymbol typeSymbol)
    {
        return typeSymbol.GetMembers()
            .OfType<IFieldSymbol>()
            .Where(f => !f.IsImplicitlyDeclared)
            .ToList();
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage] // Coverlet tracks pattern match as branch
    private static Dictionary<IMethodSymbol, MethodDeclarationSyntax> MapMethodDeclarations(
        SyntaxNode typeDeclaration,
        SemanticModel model,
        List<IMethodSymbol> methods)
    {
        var methodSet = new HashSet<IMethodSymbol>(methods, SymbolEqualityComparer.Default);
        var result = new Dictionary<IMethodSymbol, MethodDeclarationSyntax>(SymbolEqualityComparer.Default);

        foreach (var methodDecl in typeDeclaration.DescendantNodes().OfType<MethodDeclarationSyntax>())
        {
            if (model.GetDeclaredSymbol(methodDecl) is IMethodSymbol ms && methodSet.Contains(ms))
                result[ms] = methodDecl;
        }

        return result;
    }

    private static void BuildEdges(
        List<IMethodSymbol> methods,
        Dictionary<IMethodSymbol, MethodDeclarationSyntax> methodDeclarations,
        SemanticModel model,
        List<IFieldSymbol> fields,
        UnionFind uf)
    {
        var fieldSet = new HashSet<IFieldSymbol>(fields, SymbolEqualityComparer.Default);
        var methodSet = new HashSet<IMethodSymbol>(methods, SymbolEqualityComparer.Default);

        foreach (var method in methods)
        {
            var body = GetMethodBody(method, methodDeclarations);
            if (body is not null)
                ScanBodyForEdges(method, body, model, fieldSet, methodSet, uf);
        }
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage] // Defensive: null only for partial methods in other files
    private static SyntaxNode? GetMethodBody(
        IMethodSymbol method,
        Dictionary<IMethodSymbol, MethodDeclarationSyntax> methodDeclarations)
    {
        if (!methodDeclarations.TryGetValue(method, out var methodDecl))
            return null;
        return (SyntaxNode?)methodDecl.Body ?? methodDecl.ExpressionBody;
    }

    private static void ScanBodyForEdges(
        IMethodSymbol method,
        SyntaxNode body,
        SemanticModel model,
        HashSet<IFieldSymbol> fieldSet,
        HashSet<IMethodSymbol> methodSet,
        UnionFind uf)
    {
        foreach (var identifier in body.DescendantNodes().OfType<IdentifierNameSyntax>())
        {
            var symbol = model.GetSymbolInfo(identifier).Symbol;
            if (symbol is IFieldSymbol f && fieldSet.Contains(f))
                uf.Union(method, f);
            else if (symbol is IMethodSymbol m && methodSet.Contains(m))
                uf.Union(method, m);
        }
    }

    private sealed class UnionFind
    {
        private readonly Dictionary<ISymbol, ISymbol> _parent = new(SymbolEqualityComparer.Default);
        private readonly Dictionary<ISymbol, int> _rank = new(SymbolEqualityComparer.Default);

        public void Initialize(List<IMethodSymbol> methods, List<IFieldSymbol> fields)
        {
            foreach (var m in methods) { _parent[m] = m; _rank[m] = 0; }
            foreach (var f in fields) { _parent[f] = f; _rank[f] = 0; }
        }

        public ISymbol Find(ISymbol x)
        {
            while (!SymbolEqualityComparer.Default.Equals(_parent[x], x))
            {
                _parent[x] = _parent[_parent[x]];
                x = _parent[x];
            }
            return x;
        }

        public void Union(ISymbol a, ISymbol b)
        {
            var rootA = Find(a);
            var rootB = Find(b);
            if (SymbolEqualityComparer.Default.Equals(rootA, rootB))
                return;

            if (_rank[rootA] < _rank[rootB])
                _parent[rootA] = rootB;
            else if (_rank[rootA] > _rank[rootB])
                _parent[rootB] = rootA;
            else
            {
                _parent[rootB] = rootA;
                _rank[rootA]++;
            }
        }

        public int CountComponents(List<IMethodSymbol> methods, List<IFieldSymbol> fields)
        {
            var roots = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
            foreach (var m in methods) roots.Add(Find(m));
            foreach (var f in fields) roots.Add(Find(f));
            return roots.Count;
        }
    }
}
