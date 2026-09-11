using FrenchExDev.Net.QualityGate.Analysis;

using Microsoft.CodeAnalysis.CSharp.Syntax;

using Shouldly;

namespace FrenchExDev.Net.QualityGate.Tests;

public class CohesionAnalyzerTests
{
    [Fact]
    public void Lcom4_EmptyClass_Returns0()
    {
        // A class with no methods and no fields has LCOM4 = 0
        var (typeSymbol, model, typeDecl) = RoslynTestHelper.CompileType(
            "public class Empty { }");

        CohesionAnalyzer.Lcom4(typeSymbol, model, typeDecl).ShouldBe(0);
    }

    [Fact]
    public void Lcom4_OneMethodOneFieldAccessed_Returns1()
    {
        var source = """
            public class Cohesive
            {
                private int _value;
                public void SetValue(int v) { _value = v; }
            }
            """;
        var (typeSymbol, model, typeDecl) = RoslynTestHelper.CompileType(source);

        CohesionAnalyzer.Lcom4(typeSymbol, model, typeDecl).ShouldBe(1);
    }

    [Fact]
    public void Lcom4_TwoUnrelatedMethodsAndFields_Returns2()
    {
        var source = """
            public class Incohesive
            {
                private int _a;
                private int _b;
                public void UseA() { _a = 1; }
                public void UseB() { _b = 2; }
            }
            """;
        var (typeSymbol, model, typeDecl) = RoslynTestHelper.CompileType(source);

        CohesionAnalyzer.Lcom4(typeSymbol, model, typeDecl).ShouldBe(2);
    }

    [Fact]
    public void Lcom4_MethodsCallingEachOther_MergesComponents()
    {
        // Two methods that share no field but call each other should be in the same component.
        var source = """
            public class Connected
            {
                private int _x;
                private int _y;
                public void First() { _x = 1; Second(); }
                public void Second() { _y = 2; }
            }
            """;
        var (typeSymbol, model, typeDecl) = RoslynTestHelper.CompileType(source);

        // First -> _x + Second, Second -> _y => all connected
        CohesionAnalyzer.Lcom4(typeSymbol, model, typeDecl).ShouldBe(1);
    }

    [Fact]
    public void Lcom4_SharedField_MergesComponents()
    {
        // Two methods that access the same field are in the same component.
        var source = """
            public class SharedField
            {
                private int _shared;
                public void A() { _shared = 1; }
                public void B() { var x = _shared; }
            }
            """;
        var (typeSymbol, model, typeDecl) = RoslynTestHelper.CompileType(source);

        CohesionAnalyzer.Lcom4(typeSymbol, model, typeDecl).ShouldBe(1);
    }

    [Fact]
    public void Lcom4_MethodWithNoBody_StillCountsAsComponent()
    {
        // A method with only a declaration (no body) and a field that is not accessed.
        // Since extern methods have no body they remain isolated.
        var source = """
            using System.Runtime.InteropServices;
            public class WithExtern
            {
                private int _field;
                [DllImport("test")]
                public static extern void NoBody();
                public void UsesField() { _field = 1; }
            }
            """;
        var (typeSymbol, model, typeDecl) = RoslynTestHelper.CompileType(source);

        // NoBody has no body -> isolated component, UsesField + _field -> another component
        CohesionAnalyzer.Lcom4(typeSymbol, model, typeDecl).ShouldBe(2);
    }

    [Fact]
    public void Lcom4_OnlyFields_NoMethods_ReturnsFieldCount()
    {
        var source = """
            public class FieldsOnly
            {
                private int _a;
                private int _b;
            }
            """;
        var (typeSymbol, model, typeDecl) = RoslynTestHelper.CompileType(source);

        // 2 fields, each its own component (no methods to connect them)
        CohesionAnalyzer.Lcom4(typeSymbol, model, typeDecl).ShouldBe(2);
    }

    [Fact]
    public void Lcom4_OnlyMethods_NoFields_ReturnsMethodCount()
    {
        var source = """
            public class MethodsOnly
            {
                public void A() { }
                public void B() { }
            }
            """;
        var (typeSymbol, model, typeDecl) = RoslynTestHelper.CompileType(source);

        // 2 methods that don't call each other or share fields => 2 components
        CohesionAnalyzer.Lcom4(typeSymbol, model, typeDecl).ShouldBe(2);
    }

    [Fact]
    public void Lcom4_SingleMethod_NoFields_Returns1()
    {
        var source = """
            public class SingleMethod
            {
                public void DoSomething() { }
            }
            """;
        var (typeSymbol, model, typeDecl) = RoslynTestHelper.CompileType(source);

        CohesionAnalyzer.Lcom4(typeSymbol, model, typeDecl).ShouldBe(1);
    }

    [Fact]
    public void Lcom4_ExpressionBodiedMethod_FieldAccess_Returns1()
    {
        var source = """
            public class ExprBody
            {
                private int _x;
                public int GetX() => _x;
            }
            """;
        var (typeSymbol, model, typeDecl) = RoslynTestHelper.CompileType(source);

        CohesionAnalyzer.Lcom4(typeSymbol, model, typeDecl).ShouldBe(1);
    }

    [Fact]
    public void Lcom4_NullTypeSymbol_ThrowsArgumentNullException()
    {
        var (_, model, typeDecl) = RoslynTestHelper.CompileType("public class C { }");

        Should.Throw<ArgumentNullException>(() =>
            CohesionAnalyzer.Lcom4(null!, model, typeDecl));
    }

    [Fact]
    public void Lcom4_NullModel_ThrowsArgumentNullException()
    {
        var (typeSymbol, _, typeDecl) = RoslynTestHelper.CompileType("public class C { }");

        Should.Throw<ArgumentNullException>(() =>
            CohesionAnalyzer.Lcom4(typeSymbol, null!, typeDecl));
    }

    [Fact]
    public void Lcom4_NullTypeDeclaration_ThrowsArgumentNullException()
    {
        var (typeSymbol, model, _) = RoslynTestHelper.CompileType("public class C { }");

        Should.Throw<ArgumentNullException>(() =>
            CohesionAnalyzer.Lcom4(typeSymbol, model, null!));
    }

    [Fact]
    public void Lcom4_ThreeComponentsMixedConnectivity()
    {
        // 3 separate clusters:
        //   Cluster 1: A() -> _f1
        //   Cluster 2: B() -> _f2, C() -> _f2
        //   Cluster 3: D() (no field, no call)
        var source = """
            public class ThreeComponents
            {
                private int _f1;
                private int _f2;
                public void A() { _f1 = 1; }
                public void B() { _f2 = 2; }
                public void C() { var x = _f2; }
                public void D() { }
            }
            """;
        var (typeSymbol, model, typeDecl) = RoslynTestHelper.CompileType(source);

        CohesionAnalyzer.Lcom4(typeSymbol, model, typeDecl).ShouldBe(3);
    }
}
