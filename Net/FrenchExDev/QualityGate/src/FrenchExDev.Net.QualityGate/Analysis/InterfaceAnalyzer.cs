using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using FrenchExDev.Net.QualityGate.Model;

namespace FrenchExDev.Net.QualityGate.Analysis;

/// <summary>
/// Discovers interfaces and their implementations within a Roslyn <see cref="Project"/>.
/// </summary>
public static class InterfaceAnalyzer
{
    public static async Task<(List<InterfaceInfo> Interfaces, List<InterfaceImplementation> Implementations)>
        AnalyzeAsync(Project project)
    {
        ArgumentNullException.ThrowIfNull(project);

        var compilation = await project.GetCompilationAsync().ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Could not obtain compilation for project '{project.Name}'.");

        var (interfaces, interfaceSymbols) = await CollectInterfacesAsync(compilation).ConfigureAwait(false);
        var implementations = await CollectImplementationsAsync(compilation, interfaceSymbols).ConfigureAwait(false);

        return (interfaces, implementations);
    }

    private static async Task<(List<InterfaceInfo> List, Dictionary<INamedTypeSymbol, InterfaceInfo> Map)>
        CollectInterfacesAsync(Compilation compilation)
    {
        var interfaces = new List<InterfaceInfo>();
        var interfaceSymbols = new Dictionary<INamedTypeSymbol, InterfaceInfo>(SymbolEqualityComparer.Default);

        foreach (var syntaxTree in compilation.SyntaxTrees)
        {
            var semanticModel = compilation.GetSemanticModel(syntaxTree);
            var root = await syntaxTree.GetRootAsync().ConfigureAwait(false);

            foreach (var typeDecl in root.DescendantNodes().OfType<InterfaceDeclarationSyntax>())
            {
                if (semanticModel.GetDeclaredSymbol(typeDecl) is not INamedTypeSymbol symbol)
                    continue;

                var info = BuildInterfaceInfo(symbol, typeDecl, syntaxTree.FilePath);
                interfaces.Add(info);
                interfaceSymbols[symbol] = info;
            }
        }

        return (interfaces, interfaceSymbols);
    }

    private static InterfaceInfo BuildInterfaceInfo(INamedTypeSymbol symbol, TypeDeclarationSyntax typeDecl, string filePath)
    {
        var members = symbol.GetMembers()
            .Where(m => m.Kind != SymbolKind.NamedType && !m.IsImplicitlyDeclared)
            .Select(m => m.Name)
            .ToList();

        var lineSpan = typeDecl.GetLocation().GetLineSpan();

        return new InterfaceInfo(
            FullName: symbol.ToDisplayString(),
            FilePath: filePath,
            Line: lineSpan.StartLinePosition.Line + 1,
            Members: members);
    }

    private static async Task<List<InterfaceImplementation>> CollectImplementationsAsync(
        Compilation compilation,
        Dictionary<INamedTypeSymbol, InterfaceInfo> interfaceSymbols)
    {
        var implementations = new List<InterfaceImplementation>();

        foreach (var syntaxTree in compilation.SyntaxTrees)
        {
            var semanticModel = compilation.GetSemanticModel(syntaxTree);
            var root = await syntaxTree.GetRootAsync().ConfigureAwait(false);

            foreach (var typeDecl in root.DescendantNodes().OfType<TypeDeclarationSyntax>())
            {
                if (semanticModel.GetDeclaredSymbol(typeDecl) is not INamedTypeSymbol symbol)
                    continue;
                if (symbol.TypeKind == Microsoft.CodeAnalysis.TypeKind.Interface)
                    continue;

                AddImplementations(symbol, typeDecl, syntaxTree.FilePath, interfaceSymbols, implementations);
            }
        }

        return implementations;
    }

    private static void AddImplementations(
        INamedTypeSymbol symbol,
        TypeDeclarationSyntax typeDecl,
        string filePath,
        Dictionary<INamedTypeSymbol, InterfaceInfo> interfaceSymbols,
        List<InterfaceImplementation> implementations)
    {
        foreach (var iface in symbol.AllInterfaces)
        {
            if (!interfaceSymbols.ContainsKey(iface))
                continue;

            var lineSpan = typeDecl.GetLocation().GetLineSpan();
            implementations.Add(new InterfaceImplementation(
                InterfaceFullName: iface.ToDisplayString(),
                ImplementingTypeFullName: symbol.ToDisplayString(),
                FilePath: filePath,
                Line: lineSpan.StartLinePosition.Line + 1));
        }
    }
}
