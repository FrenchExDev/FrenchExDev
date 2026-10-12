using FrenchExDev.Net.QualityGate.Analysis;

using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using Shouldly;

namespace FrenchExDev.Net.QualityGate.Tests;

public class CouplingAnalyzerTests
{
    // ─── TypeEfferentCoupling ────────────────────────────────────────────

    [Fact]
    public void TypeEfferentCoupling_NoExternalRefs_Returns0()
    {
        var source = """
            public class Isolated
            {
                private int _x;
                public void DoWork() { _x = 42; }
            }
            """;
        var (typeSymbol, model, typeDecl) = RoslynTestHelper.CompileType(source);
        var root = typeDecl.SyntaxTree.GetRoot();

        CouplingAnalyzer.TypeEfferentCoupling(typeSymbol, model, root).ShouldBe(0);
    }

    [Fact]
    public void TypeEfferentCoupling_ReferencesOtherType_CountsThem()
    {
        var source = """
            public class Other { public int Value; }
            public class Consumer
            {
                public void Use()
                {
                    var o = new Other();
                    o.Value = 1;
                }
            }
            """;
        var (_, model, root) = RoslynTestHelper.Compile(source);

        // Find the Consumer type specifically
        var consumerDecl = root.DescendantNodes().OfType<TypeDeclarationSyntax>()
            .First(t => t.Identifier.Text == "Consumer");
        var consumerSymbol = model.GetDeclaredSymbol(consumerDecl)!;

        CouplingAnalyzer.TypeEfferentCoupling(consumerSymbol, model, root).ShouldBe(1);
    }

    [Fact]
    public void TypeEfferentCoupling_SelfReference_NotCounted()
    {
        var source = """
            public class SelfRef
            {
                public SelfRef Create() { return new SelfRef(); }
            }
            """;
        var (typeSymbol, model, typeDecl) = RoslynTestHelper.CompileType(source);
        var root = typeDecl.SyntaxTree.GetRoot();

        CouplingAnalyzer.TypeEfferentCoupling(typeSymbol, model, root).ShouldBe(0);
    }

    [Fact]
    public void TypeEfferentCoupling_MultipleDistinctReferences_CountsDistinct()
    {
        var source = """
            public class A { }
            public class B { }
            public class C
            {
                public void Use()
                {
                    var a = new A();
                    var b = new B();
                    var a2 = new A(); // duplicate, should not increase count
                }
            }
            """;
        var (_, model, root) = RoslynTestHelper.Compile(source);

        var typeDecl = root.DescendantNodes().OfType<TypeDeclarationSyntax>()
            .First(t => t.Identifier.Text == "C");
        var typeSymbol = model.GetDeclaredSymbol(typeDecl)!;

        CouplingAnalyzer.TypeEfferentCoupling(typeSymbol, model, root).ShouldBe(2);
    }

    [Fact]
    public void TypeEfferentCoupling_TypeNotFoundInRoot_Returns0()
    {
        // Compile two separate sources, use the type from one with the root from another
        var source1 = "public class Alpha { }";
        var source2 = "public class Beta { }";

        var (_, model1, root1) = RoslynTestHelper.Compile(source1);
        var (_, model2, _) = RoslynTestHelper.Compile(source2);

        var alphaDecl = root1.DescendantNodes().OfType<TypeDeclarationSyntax>().First();
        var alphaSymbol = model1.GetDeclaredSymbol(alphaDecl)!;

        // Beta's semantic model + root don't contain Alpha -> FindTypeDeclaration returns null -> 0
        var (_, betaModel, betaRoot) = RoslynTestHelper.Compile(source2);
        CouplingAnalyzer.TypeEfferentCoupling(alphaSymbol, betaModel, betaRoot).ShouldBe(0);
    }

    [Fact]
    public void TypeEfferentCoupling_NullType_Throws()
    {
        var (_, model, root) = RoslynTestHelper.Compile("public class C { }");
        Should.Throw<ArgumentNullException>(() =>
            CouplingAnalyzer.TypeEfferentCoupling(null!, model, root));
    }

    [Fact]
    public void TypeEfferentCoupling_NullModel_Throws()
    {
        var (_, model, root) = RoslynTestHelper.Compile("public class C { }");
        var typeDecl = root.DescendantNodes().OfType<TypeDeclarationSyntax>().First();
        var typeSymbol = model.GetDeclaredSymbol(typeDecl)!;

        Should.Throw<ArgumentNullException>(() =>
            CouplingAnalyzer.TypeEfferentCoupling(typeSymbol, null!, root));
    }

    [Fact]
    public void TypeEfferentCoupling_NullRoot_Throws()
    {
        var (_, model, root) = RoslynTestHelper.Compile("public class C { }");
        var typeDecl = root.DescendantNodes().OfType<TypeDeclarationSyntax>().First();
        var typeSymbol = model.GetDeclaredSymbol(typeDecl)!;

        Should.Throw<ArgumentNullException>(() =>
            CouplingAnalyzer.TypeEfferentCoupling(typeSymbol, model, null!));
    }

    // ─── AnalyzeNamespaceCouplingAsync ───────────────────────────────────

    [Fact]
    public async Task AnalyzeNamespaceCouplingAsync_SingleNamespace_ZeroCoupling()
    {
        var source = """
            namespace MyApp
            {
                public class Foo { }
                public class Bar { }
            }
            """;
        var project = RoslynTestHelper.CreateProject(source);

        var result = await CouplingAnalyzer.AnalyzeNamespaceCouplingAsync(project);

        result.ShouldContainKey("MyApp");
        result["MyApp"].Ca.ShouldBe(0);
        result["MyApp"].Ce.ShouldBe(0);
    }

    [Fact]
    public async Task AnalyzeNamespaceCouplingAsync_TwoNamespacesWithCrossRefs()
    {
        var source = """
            namespace NsA
            {
                public class TypeA
                {
                    public void Use()
                    {
                        var b = new NsB.TypeB();
                    }
                }
            }
            namespace NsB
            {
                public class TypeB { }
            }
            """;
        var project = RoslynTestHelper.CreateProject(source);

        var result = await CouplingAnalyzer.AnalyzeNamespaceCouplingAsync(project);

        result.ShouldContainKey("NsA");
        result.ShouldContainKey("NsB");

        // NsA references NsB -> Ce=1 for NsA
        result["NsA"].Ce.ShouldBe(1);
        // NsB is referenced by NsA -> Ca=1 for NsB
        result["NsB"].Ca.ShouldBe(1);
        // NsB doesn't reference NsA
        result["NsB"].Ce.ShouldBe(0);
        // NsA is not referenced by anyone
        result["NsA"].Ca.ShouldBe(0);
    }

    [Fact]
    public async Task AnalyzeNamespaceCouplingAsync_MutualReferences()
    {
        var source = """
            namespace NsA
            {
                public class TypeA
                {
                    public void Use() { var b = new NsB.TypeB(); }
                }
            }
            namespace NsB
            {
                public class TypeB
                {
                    public void Use() { var a = new NsA.TypeA(); }
                }
            }
            """;
        var project = RoslynTestHelper.CreateProject(source);

        var result = await CouplingAnalyzer.AnalyzeNamespaceCouplingAsync(project);

        // Both namespaces reference each other
        result["NsA"].Ce.ShouldBe(1);
        result["NsA"].Ca.ShouldBe(1);
        result["NsB"].Ce.ShouldBe(1);
        result["NsB"].Ca.ShouldBe(1);
    }

    [Fact]
    public async Task AnalyzeNamespaceCouplingAsync_NullProject_Throws()
    {
        await Should.ThrowAsync<ArgumentNullException>(
            () => CouplingAnalyzer.AnalyzeNamespaceCouplingAsync(null!));
    }

    [Fact]
    public async Task AnalyzeNamespaceCouplingAsync_EmptyProject_ReturnsEmptyDictionary()
    {
        var project = RoslynTestHelper.CreateProject("");

        var result = await CouplingAnalyzer.AnalyzeNamespaceCouplingAsync(project);

        result.ShouldBeEmpty();
    }

    [Fact]
    public void TypeEfferentCoupling_PropertyReference_CountsContainingType()
    {
        var source = """
            public class Config { public int Setting { get; set; } }
            public class Consumer
            {
                public void Read()
                {
                    var c = new Config();
                    var s = c.Setting;
                }
            }
            """;
        var (_, model, root) = RoslynTestHelper.Compile(source);

        var consumerDecl = root.DescendantNodes().OfType<TypeDeclarationSyntax>()
            .First(t => t.Identifier.Text == "Consumer");
        var consumerSymbol = model.GetDeclaredSymbol(consumerDecl)!;

        CouplingAnalyzer.TypeEfferentCoupling(consumerSymbol, model, root).ShouldBeGreaterThanOrEqualTo(1);
    }
}
