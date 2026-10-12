using System.Collections.Immutable;
using System.Linq;
using FrenchExDev.Net.Traefik.Bundle.SourceGenerator.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Shouldly;

namespace FrenchExDev.Net.Traefik.Bundle.SourceGenerator.Tests;

/// <summary>
/// Verifies the DiscriminatedUnionAnalyzer (TFK001) using a hand-rolled
/// CSharpCompilation, instead of pulling in Microsoft.CodeAnalysis.Testing.
/// Same low-dependency style as the rest of the monorepo's SG tests.
/// </summary>
public sealed class AnalyzerTests
{
    private const string MarkerAttribute = """
        namespace FrenchExDev.Net.Traefik.Bundle.Attributes
        {
            [System.AttributeUsage(System.AttributeTargets.Class)]
            public sealed class TraefikDiscriminatedUnionAttribute : System.Attribute { }
        }
        """;

    private const string FakeUnion = """
        namespace FakeBundle
        {
            [global::FrenchExDev.Net.Traefik.Bundle.Attributes.TraefikDiscriminatedUnion]
            public sealed class FakeMiddleware
            {
                public string? AddPrefix { get; set; }
                public string? BasicAuth { get; set; }
                public string? StripPrefix { get; set; }
            }
        }
        """;

    [Fact]
    public async Task TFK001_FlagsTwoBranchesSet()
    {
        var diagnostics = await RunAnalyzerAsync("""
            class Test
            {
                FakeBundle.FakeMiddleware Make() => new FakeBundle.FakeMiddleware
                {
                    AddPrefix = "x",
                    BasicAuth = "y",
                };
            }
            """);

        var tfk = diagnostics.Where(d => d.Id == "TFK001").ToList();
        tfk.Count.ShouldBe(1);
        tfk[0].GetMessage().ShouldContain("FakeMiddleware");
        tfk[0].GetMessage().ShouldContain("2");
    }

    [Fact]
    public async Task TFK001_FlagsThreeBranchesSet()
    {
        var diagnostics = await RunAnalyzerAsync("""
            class Test
            {
                FakeBundle.FakeMiddleware Make() => new FakeBundle.FakeMiddleware
                {
                    AddPrefix = "x",
                    BasicAuth = "y",
                    StripPrefix = "z",
                };
            }
            """);

        diagnostics.Where(d => d.Id == "TFK001").ShouldNotBeEmpty();
    }

    [Fact]
    public async Task TFK001_DoesNotFlagSingleBranch()
    {
        var diagnostics = await RunAnalyzerAsync("""
            class Test
            {
                FakeBundle.FakeMiddleware Make() => new FakeBundle.FakeMiddleware
                {
                    AddPrefix = "x",
                };
            }
            """);

        diagnostics.Where(d => d.Id == "TFK001").ShouldBeEmpty();
    }

    [Fact]
    public async Task TFK001_DoesNotFlagExplicitNullAssignments()
    {
        var diagnostics = await RunAnalyzerAsync("""
            class Test
            {
                FakeBundle.FakeMiddleware Make() => new FakeBundle.FakeMiddleware
                {
                    AddPrefix = "x",
                    BasicAuth = null,
                };
            }
            """);

        diagnostics.Where(d => d.Id == "TFK001").ShouldBeEmpty();
    }

    [Fact]
    public async Task TFK001_FlagsImplicitObjectCreation()
    {
        var diagnostics = await RunAnalyzerAsync("""
            class Test
            {
                FakeBundle.FakeMiddleware Make()
                {
                    FakeBundle.FakeMiddleware m = new()
                    {
                        AddPrefix = "x",
                        BasicAuth = "y",
                    };
                    return m;
                }
            }
            """);

        diagnostics.Where(d => d.Id == "TFK001").ShouldNotBeEmpty();
    }

    private static async Task<ImmutableArray<Diagnostic>> RunAnalyzerAsync(string userCode)
    {
        var sources = new[] { MarkerAttribute, FakeUnion, userCode };
        var trees = sources.Select(s => CSharpSyntaxTree.ParseText(s)).ToArray();

        var refs = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => MetadataReference.CreateFromFile(a.Location))
            .ToList<MetadataReference>();

        var compilation = CSharpCompilation.Create(
            assemblyName: "AnalyzerUnderTest",
            syntaxTrees: trees,
            references: refs,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));

        var analyzer = new DiscriminatedUnionAnalyzer();
        var withAnalyzers = compilation.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(analyzer));
        return await withAnalyzers.GetAnalyzerDiagnosticsAsync();
    }
}
