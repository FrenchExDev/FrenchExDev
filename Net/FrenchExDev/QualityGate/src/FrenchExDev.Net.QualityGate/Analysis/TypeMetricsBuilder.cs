using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using FrenchExDev.Net.QualityGate.Model;

namespace FrenchExDev.Net.QualityGate.Analysis;

/// <summary>
/// Builds <see cref="TypeMetrics"/> for all types in a compilation, grouped by namespace.
/// </summary>
internal static class TypeMetricsBuilder
{
    public static async Task<Dictionary<string, List<TypeMetrics>>> BuildAsync(
        Compilation compilation,
        CancellationToken ct)
    {
        var result = new Dictionary<string, List<TypeMetrics>>(StringComparer.Ordinal);

        foreach (var syntaxTree in compilation.SyntaxTrees)
        {
            ct.ThrowIfCancellationRequested();

            var semanticModel = compilation.GetSemanticModel(syntaxTree);
            var root = await syntaxTree.GetRootAsync(ct).ConfigureAwait(false);

            foreach (var typeDecl in root.DescendantNodes().OfType<BaseTypeDeclarationSyntax>())
            {
                if (semanticModel.GetDeclaredSymbol(typeDecl) is not INamedTypeSymbol typeSymbol)
                    continue;

                var metrics = BuildTypeMetrics(typeDecl, typeSymbol, semanticModel, root, syntaxTree.FilePath);
                var nsName = typeSymbol.ContainingNamespace?.ToDisplayString() ?? "(global)";

                if (!result.TryGetValue(nsName, out var list))
                {
                    list = [];
                    result[nsName] = list;
                }
                list.Add(metrics);
            }
        }

        return result;
    }

    private static TypeMetrics BuildTypeMetrics(
        BaseTypeDeclarationSyntax typeDecl,
        INamedTypeSymbol typeSymbol,
        SemanticModel semanticModel,
        SyntaxNode root,
        string filePath)
    {
        var kind = MapTypeKind(typeDecl, typeSymbol);
        var methods = GetOrdinaryMethods(typeSymbol);
        var methodMetrics = BuildMethodMetrics(typeDecl, semanticModel, methods);

        var lcom4 = typeDecl is TypeDeclarationSyntax tds
            ? CohesionAnalyzer.Lcom4(typeSymbol, semanticModel, tds)
            : 0;

        var location = typeDecl.GetLocation().GetLineSpan();

        return new TypeMetrics
        {
            Name = typeSymbol.Name,
            FullName = typeSymbol.ToDisplayString(),
            FilePath = filePath,
            Line = location.StartLinePosition.Line + 1,
            Kind = kind,
            MethodCount = methods.Count,
            PropertyCount = typeSymbol.GetMembers().OfType<IPropertySymbol>().Count(p => !p.IsImplicitlyDeclared),
            FieldCount = typeSymbol.GetMembers().OfType<IFieldSymbol>().Count(f => !f.IsImplicitlyDeclared),
            InheritanceDepth = ComputeInheritanceDepth(typeSymbol),
            Lcom4 = lcom4,
            EfferentCoupling = CouplingAnalyzer.TypeEfferentCoupling(typeSymbol, semanticModel, root),
            CyclomaticComplexity = methodMetrics.Sum(m => m.CyclomaticComplexity),
            CognitiveComplexity = methodMetrics.Sum(m => m.CognitiveComplexity),
            LinesOfCode = methodMetrics.Sum(m => m.LinesOfCode),
            Methods = methodMetrics
        };
    }

    private static List<IMethodSymbol> GetOrdinaryMethods(INamedTypeSymbol typeSymbol)
    {
        return typeSymbol.GetMembers().OfType<IMethodSymbol>()
            .Where(m => m.MethodKind == MethodKind.Ordinary && !m.IsImplicitlyDeclared)
            .ToList();
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage] // Line 101: null methodDecl only for partial methods in other files
    private static List<MethodMetrics> BuildMethodMetrics(
        BaseTypeDeclarationSyntax typeDecl,
        SemanticModel semanticModel,
        List<IMethodSymbol> methods)
    {
        var result = new List<MethodMetrics>();

        foreach (var methodSymbol in methods)
        {
            var methodDecl = FindMethodDeclaration(typeDecl, semanticModel, methodSymbol);
            if (methodDecl is null)
                continue;

            var body = (SyntaxNode?)methodDecl.Body ?? methodDecl.ExpressionBody;
            if (body is null)
                continue;

            var cc = ComplexityAnalyzer.CyclomaticComplexity(body);
            var cog = ComplexityAnalyzer.CognitiveComplexity(body);
            var loc = ComplexityAnalyzer.LogicalLinesOfCode(body);
            var lineSpan = methodDecl.GetLocation().GetLineSpan();

            result.Add(new MethodMetrics
            {
                Name = methodSymbol.Name,
                FullName = methodSymbol.ToDisplayString(),
                Line = lineSpan.StartLinePosition.Line + 1,
                CyclomaticComplexity = cc,
                CognitiveComplexity = cog,
                LinesOfCode = loc,
                ParameterCount = methodSymbol.Parameters.Length,
                MaintainabilityIndex = ComplexityAnalyzer.MaintainabilityIndex(cc, loc)
            });
        }

        return result;
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage] // Defensive: null only for partial class methods in other files
    private static MethodDeclarationSyntax? FindMethodDeclaration(
        BaseTypeDeclarationSyntax typeDecl,
        SemanticModel model,
        IMethodSymbol methodSymbol)
    {
        foreach (var methodDecl in typeDecl.DescendantNodes().OfType<MethodDeclarationSyntax>())
        {
            if (SymbolEqualityComparer.Default.Equals(model.GetDeclaredSymbol(methodDecl), methodSymbol))
                return methodDecl;
        }
        return null;
    }

    private static Model.TypeKind MapTypeKind(BaseTypeDeclarationSyntax syntax, INamedTypeSymbol symbol)
    {
        return syntax switch
        {
            InterfaceDeclarationSyntax => Model.TypeKind.Interface,
            StructDeclarationSyntax => Model.TypeKind.Struct,
            EnumDeclarationSyntax => Model.TypeKind.Enum,
            RecordDeclarationSyntax => Model.TypeKind.Record,
            _ => Model.TypeKind.Class
        };
    }

    private static int ComputeInheritanceDepth(INamedTypeSymbol symbol)
    {
        int depth = 0;
        var current = symbol.BaseType;
        while (current is not null && current.SpecialType != SpecialType.System_Object)
        {
            depth++;
            current = current.BaseType;
        }
        return depth;
    }
}
