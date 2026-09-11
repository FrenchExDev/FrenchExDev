using FrenchExDev.Net.QualityGate.Analysis;

using Shouldly;

namespace FrenchExDev.Net.QualityGate.Tests;

public class ApiSurfaceAnalyzerTests
{
    [Fact]
    public async Task AnalyzeAsync_NoPublicTypes_AllZeros()
    {
        var source = """
            internal class Hidden
            {
                public void NotCounted() { }
                public string NotCountedProp { get; set; }
            }
            """;
        var project = RoslynTestHelper.CreateProject(source);

        var result = await ApiSurfaceAnalyzer.AnalyzeAsync(project);

        result.PublicTypeCount.ShouldBe(0);
        result.PublicMethodCount.ShouldBe(0);
        result.PublicPropertyCount.ShouldBe(0);
        result.PublicTypes.ShouldBeEmpty();
    }

    [Fact]
    public async Task AnalyzeAsync_PublicClassWithMethodsAndProperties_CorrectCounts()
    {
        var source = """
            public class Api
            {
                public void MethodA() { }
                public void MethodB() { }
                public string PropA { get; set; }
                public int PropB { get; }
                internal void InternalMethod() { }
                private int _privateField;
            }
            """;
        var project = RoslynTestHelper.CreateProject(source);

        var result = await ApiSurfaceAnalyzer.AnalyzeAsync(project);

        result.PublicTypeCount.ShouldBe(1);
        result.PublicMethodCount.ShouldBe(2);
        result.PublicPropertyCount.ShouldBe(2);
        result.PublicTypes.ShouldContain("Api");
    }

    [Fact]
    public async Task AnalyzeAsync_InternalTypes_NotCounted()
    {
        var source = """
            internal class Internal1 { public void M() { } }
            internal class Internal2 { public string P { get; } }
            """;
        var project = RoslynTestHelper.CreateProject(source);

        var result = await ApiSurfaceAnalyzer.AnalyzeAsync(project);

        result.PublicTypeCount.ShouldBe(0);
        result.PublicMethodCount.ShouldBe(0);
        result.PublicPropertyCount.ShouldBe(0);
    }

    [Fact]
    public async Task AnalyzeAsync_MultiplePublicTypes_SumsCorrectly()
    {
        var source = """
            public class First
            {
                public void A() { }
                public string P1 { get; }
            }
            public class Second
            {
                public void B() { }
                public void C() { }
                public int P2 { get; set; }
            }
            """;
        var project = RoslynTestHelper.CreateProject(source);

        var result = await ApiSurfaceAnalyzer.AnalyzeAsync(project);

        result.PublicTypeCount.ShouldBe(2);
        result.PublicMethodCount.ShouldBe(3); // A + B + C
        result.PublicPropertyCount.ShouldBe(2); // P1 + P2
    }

    [Fact]
    public async Task AnalyzeAsync_EmptyProject_AllZeros()
    {
        var project = RoslynTestHelper.CreateProject("");

        var result = await ApiSurfaceAnalyzer.AnalyzeAsync(project);

        result.PublicTypeCount.ShouldBe(0);
        result.PublicMethodCount.ShouldBe(0);
        result.PublicPropertyCount.ShouldBe(0);
        result.PublicTypes.ShouldBeEmpty();
    }

    [Fact]
    public async Task AnalyzeAsync_NullProject_ThrowsArgumentNullException()
    {
        await Should.ThrowAsync<ArgumentNullException>(
            () => ApiSurfaceAnalyzer.AnalyzeAsync(null!));
    }

    [Fact]
    public async Task AnalyzeAsync_PublicTypesListIncludesFullNames()
    {
        var source = """
            namespace My.Api
            {
                public class Service { }
                public class Controller { }
            }
            """;
        var project = RoslynTestHelper.CreateProject(source);

        var result = await ApiSurfaceAnalyzer.AnalyzeAsync(project);

        result.PublicTypes.ShouldContain("My.Api.Service");
        result.PublicTypes.ShouldContain("My.Api.Controller");
    }

    [Fact]
    public async Task AnalyzeAsync_ConstructorsAndOperators_NotCountedAsMethods()
    {
        // Constructors have MethodKind.Constructor, operators have MethodKind.UserDefinedOperator
        // Only MethodKind.Ordinary should be counted
        var source = """
            public class WithCtor
            {
                public WithCtor() { }
                public void RealMethod() { }
            }
            """;
        var project = RoslynTestHelper.CreateProject(source);

        var result = await ApiSurfaceAnalyzer.AnalyzeAsync(project);

        result.PublicMethodCount.ShouldBe(1); // only RealMethod
    }

    [Fact]
    public async Task AnalyzeAsync_ImplicitMembers_NotCounted()
    {
        // Property accessors are implicitly declared methods; they should not be counted
        var source = """
            public class AutoProp
            {
                public string Name { get; set; }
            }
            """;
        var project = RoslynTestHelper.CreateProject(source);

        var result = await ApiSurfaceAnalyzer.AnalyzeAsync(project);

        result.PublicMethodCount.ShouldBe(0); // get_Name / set_Name are implicit
        result.PublicPropertyCount.ShouldBe(1);
    }

    [Fact]
    public async Task AnalyzeAsync_MultipleDocuments_AggregatesAll()
    {
        var project = RoslynTestHelper.CreateProject(
            ("A.cs", "public class A { public void M1() { } }"),
            ("B.cs", "public class B { public string P { get; } }")
        );

        var result = await ApiSurfaceAnalyzer.AnalyzeAsync(project);

        result.PublicTypeCount.ShouldBe(2);
        result.PublicMethodCount.ShouldBe(1);
        result.PublicPropertyCount.ShouldBe(1);
    }

    [Fact]
    public async Task AnalyzeAsync_MixedAccessibility_OnlyCountsPublic()
    {
        var source = """
            public class MixedClass
            {
                public void PublicMethod() { }
                internal void InternalMethod() { }
                protected void ProtectedMethod() { }
                private void PrivateMethod() { }
                public string PublicProp { get; }
                internal string InternalProp { get; }
            }
            """;
        var project = RoslynTestHelper.CreateProject(source);

        var result = await ApiSurfaceAnalyzer.AnalyzeAsync(project);

        result.PublicTypeCount.ShouldBe(1);
        result.PublicMethodCount.ShouldBe(1);
        result.PublicPropertyCount.ShouldBe(1);
    }
}
