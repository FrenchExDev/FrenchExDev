using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using FrenchExDev.Net.QualityGate.Model;

namespace FrenchExDev.Net.QualityGate.Analysis;

/// <summary>
/// Analyzes the public API surface of a Roslyn <see cref="Project"/>.
/// </summary>
public static class ApiSurfaceAnalyzer
{
    public static async Task<ApiSurface> AnalyzeAsync(Project project)
    {
        ArgumentNullException.ThrowIfNull(project);

        var compilation = await project.GetCompilationAsync().ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Could not obtain compilation for project '{project.Name}'.");

        var publicTypes = new List<string>();
        int methodCount = 0;
        int propertyCount = 0;

        foreach (var syntaxTree in compilation.SyntaxTrees)
        {
            var semanticModel = compilation.GetSemanticModel(syntaxTree);
            var root = await syntaxTree.GetRootAsync().ConfigureAwait(false);

            foreach (var typeDecl in root.DescendantNodes().OfType<TypeDeclarationSyntax>())
            {
                if (semanticModel.GetDeclaredSymbol(typeDecl) is not INamedTypeSymbol symbol)
                    continue;
                if (symbol.DeclaredAccessibility != Accessibility.Public)
                    continue;

                publicTypes.Add(symbol.ToDisplayString());
                var (methods, properties) = CountPublicMembers(symbol);
                methodCount += methods;
                propertyCount += properties;
            }
        }

        return new ApiSurface
        {
            PublicTypeCount = publicTypes.Count,
            PublicMethodCount = methodCount,
            PublicPropertyCount = propertyCount,
            PublicTypes = publicTypes
        };
    }

    private static (int Methods, int Properties) CountPublicMembers(INamedTypeSymbol symbol)
    {
        int methods = 0, properties = 0;

        foreach (var member in symbol.GetMembers())
        {
            if (member.IsImplicitlyDeclared || member.DeclaredAccessibility != Accessibility.Public)
                continue;

            switch (member)
            {
                case IMethodSymbol m when m.MethodKind == MethodKind.Ordinary:
                    methods++;
                    break;
                case IPropertySymbol:
                    properties++;
                    break;
            }
        }

        return (methods, properties);
    }
}
