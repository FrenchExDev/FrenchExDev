using System.Reflection;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace FrenchExDev.Net.QualityGate.Tests;

/// <summary>
/// Shared helpers for creating in-memory Roslyn compilations and projects for testing.
/// </summary>
internal static class RoslynTestHelper
{
    private static readonly MetadataReference[] References =
    [
        MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
        MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
        MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
        MetadataReference.CreateFromFile(Assembly.Load("System.Runtime").Location),
        MetadataReference.CreateFromFile(Assembly.Load("System.Collections").Location),
    ];

    /// <summary>
    /// Parses and compiles the given C# source, returning the compilation, semantic model,
    /// and the root syntax node.
    /// </summary>
    public static (Compilation Compilation, SemanticModel Model, SyntaxNode Root) Compile(string source)
    {
        var tree = CSharpSyntaxTree.ParseText(source);
        var compilation = CSharpCompilation.Create("Test",
            [tree],
            References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var model = compilation.GetSemanticModel(tree);
        var root = tree.GetRoot();
        return (compilation, model, root);
    }

    /// <summary>
    /// Compiles source and returns the first <see cref="INamedTypeSymbol"/> declared in it,
    /// along with the semantic model and the corresponding type declaration syntax node.
    /// </summary>
    public static (INamedTypeSymbol TypeSymbol, SemanticModel Model, TypeDeclarationSyntax TypeDecl) CompileType(string source)
    {
        var (_, model, root) = Compile(source);
        var typeDecl = root.DescendantNodes().OfType<TypeDeclarationSyntax>().First();
        var typeSymbol = model.GetDeclaredSymbol(typeDecl)!;
        return (typeSymbol, model, typeDecl);
    }

    /// <summary>
    /// Creates an <see cref="AdhocWorkspace"/>-backed <see cref="Project"/> with the given
    /// source documents. Suitable for analyzers that call <c>project.GetCompilationAsync()</c>.
    /// </summary>
    public static Project CreateProject(string source)
    {
        return CreateProject([("Test.cs", source)]);
    }

    /// <summary>
    /// Creates a project with multiple source documents.
    /// </summary>
    public static Project CreateProject(params (string FileName, string Source)[] documents)
    {
        var workspace = new AdhocWorkspace();
        var projectInfo = ProjectInfo.Create(
                ProjectId.CreateNewId(),
                VersionStamp.Create(),
                "TestProject",
                "TestProject",
                LanguageNames.CSharp)
            .WithMetadataReferences(References)
            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var project = workspace.AddProject(projectInfo);

        foreach (var (fileName, src) in documents)
        {
            workspace.AddDocument(project.Id, fileName, SourceText.From(src));
        }

        return workspace.CurrentSolution.GetProject(project.Id)!;
    }
}
