using FrenchExDev.Net.QualityGate.Analysis;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

using Shouldly;

using TypeKind = FrenchExDev.Net.QualityGate.Model.TypeKind;

namespace FrenchExDev.Net.QualityGate.Tests;

public class TypeMetricsBuilderTests
{
    private static async Task<Model.TypeMetrics> BuildSingleTypeMetrics(string source)
    {
        var (compilation, _, _) = RoslynTestHelper.Compile(source);
        var result = await TypeMetricsBuilder.BuildAsync(compilation, CancellationToken.None);
        return result.Values.SelectMany(l => l).First();
    }

    // ─── Simple class ────────────────────────────────────────────────────

    [Fact]
    public async Task BuildAsync_SimpleClass_CorrectMethodPropertyFieldCount()
    {
        var source = """
            public class Simple
            {
                private int _field;
                public string Name { get; set; }
                public void DoWork() { _field = 1; }
                public int Calculate(int x) { return x * 2; }
            }
            """;
        var metrics = await BuildSingleTypeMetrics(source);

        metrics.Name.ShouldBe("Simple");
        metrics.Kind.ShouldBe(TypeKind.Class);
        metrics.MethodCount.ShouldBe(2); // DoWork, Calculate
        metrics.PropertyCount.ShouldBe(1); // Name
        metrics.FieldCount.ShouldBe(1); // _field
    }

    // ─── TypeKind mapping ───────────────────────────────────────────────

    [Fact]
    public async Task BuildAsync_Interface_TypeKindIsInterface()
    {
        var source = """
            public interface IMyInterface
            {
                void Execute();
            }
            """;
        var metrics = await BuildSingleTypeMetrics(source);

        metrics.Kind.ShouldBe(TypeKind.Interface);
    }

    [Fact]
    public async Task BuildAsync_Enum_TypeKindIsEnum()
    {
        var source = """
            public enum Color
            {
                Red,
                Green,
                Blue
            }
            """;
        var metrics = await BuildSingleTypeMetrics(source);

        metrics.Kind.ShouldBe(TypeKind.Enum);
    }

    [Fact]
    public async Task BuildAsync_Struct_TypeKindIsStruct()
    {
        var source = """
            public struct Point
            {
                public int X;
                public int Y;
            }
            """;
        var metrics = await BuildSingleTypeMetrics(source);

        metrics.Kind.ShouldBe(TypeKind.Struct);
        metrics.FieldCount.ShouldBe(2);
    }

    [Fact]
    public async Task BuildAsync_Record_TypeKindIsRecord()
    {
        var source = """
            public record Person(string Name, int Age);
            """;
        var metrics = await BuildSingleTypeMetrics(source);

        metrics.Kind.ShouldBe(TypeKind.Record);
    }

    // ─── Inheritance depth ──────────────────────────────────────────────

    [Fact]
    public async Task BuildAsync_NoBaseClass_InheritanceDepth0()
    {
        var source = "public class Root { }";
        var metrics = await BuildSingleTypeMetrics(source);

        metrics.InheritanceDepth.ShouldBe(0);
    }

    [Fact]
    public async Task BuildAsync_OneLevel_InheritanceDepth1()
    {
        var source = """
            public class Base { }
            public class Derived : Base { }
            """;
        var (compilation, _, _) = RoslynTestHelper.Compile(source);
        var result = await TypeMetricsBuilder.BuildAsync(compilation, CancellationToken.None);
        var derived = result.Values.SelectMany(l => l).First(t => t.Name == "Derived");

        derived.InheritanceDepth.ShouldBe(1);
    }

    [Fact]
    public async Task BuildAsync_TwoLevels_InheritanceDepth2()
    {
        var source = """
            public class A { }
            public class B : A { }
            public class C : B { }
            """;
        var (compilation, _, _) = RoslynTestHelper.Compile(source);
        var result = await TypeMetricsBuilder.BuildAsync(compilation, CancellationToken.None);
        var c = result.Values.SelectMany(l => l).First(t => t.Name == "C");

        c.InheritanceDepth.ShouldBe(2);
    }

    // ─── Method metrics populated ───────────────────────────────────────

    [Fact]
    public async Task BuildAsync_MethodWithBody_MetricsPopulated()
    {
        var source = """
            public class WithMethod
            {
                public int Compute(int x)
                {
                    if (x > 0)
                        return x * 2;
                    else
                        return -x;
                }
            }
            """;
        var metrics = await BuildSingleTypeMetrics(source);

        metrics.Methods.Count.ShouldBe(1);
        var method = metrics.Methods[0];
        method.Name.ShouldBe("Compute");
        method.CyclomaticComplexity.ShouldBeGreaterThanOrEqualTo(2); // if
        method.CognitiveComplexity.ShouldBeGreaterThanOrEqualTo(1);
        method.LinesOfCode.ShouldBeGreaterThanOrEqualTo(1);
        method.ParameterCount.ShouldBe(1);
        method.MaintainabilityIndex.ShouldNotBeNull();
        method.MaintainabilityIndex!.Value.ShouldBeInRange(0.0, 100.0);
    }

    [Fact]
    public async Task BuildAsync_MethodWithExpressionBody_MetricsPopulated()
    {
        var source = """
            public class ExprBody
            {
                public int Double(int x) => x * 2;
            }
            """;
        var metrics = await BuildSingleTypeMetrics(source);

        metrics.Methods.Count.ShouldBe(1);
        metrics.Methods[0].Name.ShouldBe("Double");
        metrics.Methods[0].CyclomaticComplexity.ShouldBe(1); // no branching
    }

    // ─── Aggregated metrics ─────────────────────────────────────────────

    [Fact]
    public async Task BuildAsync_AggregatesCyclomaticComplexityAcrossMethods()
    {
        var source = """
            public class Multi
            {
                public void A() { if (true) { } }
                public void B() { if (true) { } if (false) { } }
            }
            """;
        var metrics = await BuildSingleTypeMetrics(source);

        // A: CC=2, B: CC=3 => total CC = 5
        metrics.CyclomaticComplexity.ShouldBe(5);
    }

    // ─── Namespace grouping ─────────────────────────────────────────────

    [Fact]
    public async Task BuildAsync_GroupsByNamespace()
    {
        var source = """
            namespace NsA { public class TypeA { } }
            namespace NsB { public class TypeB { } }
            """;
        var (compilation, _, _) = RoslynTestHelper.Compile(source);
        var result = await TypeMetricsBuilder.BuildAsync(compilation, CancellationToken.None);

        result.ShouldContainKey("NsA");
        result.ShouldContainKey("NsB");
        result["NsA"].Count.ShouldBe(1);
        result["NsB"].Count.ShouldBe(1);
    }

    [Fact]
    public async Task BuildAsync_GlobalNamespace_UsedAsKey()
    {
        var source = "public class GlobalType { }";
        var (compilation, _, _) = RoslynTestHelper.Compile(source);
        var result = await TypeMetricsBuilder.BuildAsync(compilation, CancellationToken.None);

        // The global namespace key should exist
        result.Count.ShouldBe(1);
    }

    // ─── LCOM4 and coupling integrated ──────────────────────────────────

    [Fact]
    public async Task BuildAsync_Lcom4_ComputedForClass()
    {
        var source = """
            public class Cohesive
            {
                private int _field;
                public void Set(int v) { _field = v; }
                public int Get() { return _field; }
            }
            """;
        var metrics = await BuildSingleTypeMetrics(source);

        metrics.Lcom4.ShouldBe(1); // both methods access _field
    }

    [Fact]
    public async Task BuildAsync_Enum_Lcom4Is0()
    {
        var source = "public enum Status { Active, Inactive }";
        var metrics = await BuildSingleTypeMetrics(source);

        // Enums are BaseTypeDeclarationSyntax but not TypeDeclarationSyntax
        // So they get lcom4 = 0
        metrics.Lcom4.ShouldBe(0);
    }

    [Fact]
    public async Task BuildAsync_EfferentCoupling_Computed()
    {
        var source = """
            public class Dep { }
            public class Consumer
            {
                public void Use() { var d = new Dep(); }
            }
            """;
        var (compilation, _, _) = RoslynTestHelper.Compile(source);
        var result = await TypeMetricsBuilder.BuildAsync(compilation, CancellationToken.None);
        var consumer = result.Values.SelectMany(l => l).First(t => t.Name == "Consumer");

        consumer.EfferentCoupling.ShouldBeGreaterThanOrEqualTo(1);
    }

    // ─── Cancellation ───────────────────────────────────────────────────

    [Fact]
    public async Task BuildAsync_CancelledToken_ThrowsOperationCancelled()
    {
        var source = "public class C { }";
        var (compilation, _, _) = RoslynTestHelper.Compile(source);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        await Should.ThrowAsync<OperationCanceledException>(
            () => TypeMetricsBuilder.BuildAsync(compilation, cts.Token));
    }

    // ─── Empty compilation ──────────────────────────────────────────────

    [Fact]
    public async Task BuildAsync_EmptyCompilation_ReturnsEmptyDictionary()
    {
        var (compilation, _, _) = RoslynTestHelper.Compile("");
        var result = await TypeMetricsBuilder.BuildAsync(compilation, CancellationToken.None);

        result.ShouldBeEmpty();
    }

    // ─── Line number ────────────────────────────────────────────────────

    [Fact]
    public async Task BuildAsync_LineIsCorrect()
    {
        var source = """
            namespace Test
            {
                public class OnLine3
                {
                    public void M() { }
                }
            }
            """;
        var metrics = await BuildSingleTypeMetrics(source);

        metrics.Line.ShouldBeGreaterThan(0);
    }

    // ─── Method without body skipped ────────────────────────────────────

    [Fact]
    public async Task BuildAsync_AbstractMethod_NoMethodMetrics()
    {
        var source = """
            public abstract class Abs
            {
                public abstract void NoBody();
            }
            """;
        var metrics = await BuildSingleTypeMetrics(source);

        // Abstract methods have no body, so no MethodMetrics entry
        metrics.Methods.ShouldBeEmpty();
        metrics.MethodCount.ShouldBe(1); // symbol still exists
    }

    // ─── FullName includes namespace ────────────────────────────────────

    [Fact]
    public async Task BuildAsync_FullNameIncludesNamespace()
    {
        var source = """
            namespace My.Deep.Ns
            {
                public class Nested { }
            }
            """;
        var metrics = await BuildSingleTypeMetrics(source);

        metrics.FullName.ShouldBe("My.Deep.Ns.Nested");
    }
}
