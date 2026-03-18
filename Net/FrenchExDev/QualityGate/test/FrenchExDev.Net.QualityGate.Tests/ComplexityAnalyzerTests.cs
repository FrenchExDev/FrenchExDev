using FrenchExDev.Net.QualityGate.Analysis;

using Microsoft.CodeAnalysis.CSharp;

using Shouldly;

namespace FrenchExDev.Net.QualityGate.Tests;

public class ComplexityAnalyzerTests
{
    private static Microsoft.CodeAnalysis.SyntaxNode ParseMethodBody(string methodCode)
    {
        var tree = CSharpSyntaxTree.ParseText($"class C {{ {methodCode} }}");
        var root = tree.GetRoot();
        var method = root.DescendantNodes()
            .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax>()
            .First();
        return method.Body!;
    }

    [Fact]
    public void CyclomaticComplexity_EmptyMethod_Returns1()
    {
        var body = ParseMethodBody("void M() { }");
        ComplexityAnalyzer.CyclomaticComplexity(body).ShouldBe(1);
    }

    [Fact]
    public void CyclomaticComplexity_SingleIf_Returns2()
    {
        var body = ParseMethodBody("void M() { if (true) { } }");
        ComplexityAnalyzer.CyclomaticComplexity(body).ShouldBe(2);
    }

    [Fact]
    public void CyclomaticComplexity_IfElseIf_Returns3()
    {
        var body = ParseMethodBody("void M() { if (true) { } else if (false) { } else { } }");
        ComplexityAnalyzer.CyclomaticComplexity(body).ShouldBe(3);
    }

    [Fact]
    public void CyclomaticComplexity_ForLoop_Returns2()
    {
        var body = ParseMethodBody("void M() { for (int i = 0; i < 10; i++) { } }");
        ComplexityAnalyzer.CyclomaticComplexity(body).ShouldBe(2);
    }

    [Fact]
    public void CyclomaticComplexity_ForEachLoop_Returns2()
    {
        var body = ParseMethodBody("void M() { foreach (var x in new int[0]) { } }");
        ComplexityAnalyzer.CyclomaticComplexity(body).ShouldBe(2);
    }

    [Fact]
    public void CyclomaticComplexity_WhileLoop_Returns2()
    {
        var body = ParseMethodBody("void M() { while (true) { } }");
        ComplexityAnalyzer.CyclomaticComplexity(body).ShouldBe(2);
    }

    [Fact]
    public void CyclomaticComplexity_DoWhile_Returns2()
    {
        var body = ParseMethodBody("void M() { do { } while (true); }");
        ComplexityAnalyzer.CyclomaticComplexity(body).ShouldBe(2);
    }

    [Fact]
    public void CyclomaticComplexity_TryCatch_Returns2()
    {
        var body = ParseMethodBody("void M() { try { } catch (System.Exception) { } }");
        ComplexityAnalyzer.CyclomaticComplexity(body).ShouldBe(2);
    }

    [Fact]
    public void CyclomaticComplexity_LogicalAnd_Returns2()
    {
        var body = ParseMethodBody("void M() { var x = true && false; }");
        ComplexityAnalyzer.CyclomaticComplexity(body).ShouldBe(2);
    }

    [Fact]
    public void CyclomaticComplexity_LogicalOr_Returns2()
    {
        var body = ParseMethodBody("void M() { var x = true || false; }");
        ComplexityAnalyzer.CyclomaticComplexity(body).ShouldBe(2);
    }

    [Fact]
    public void CyclomaticComplexity_NullCoalesce_Returns2()
    {
        var body = ParseMethodBody("void M() { string x = null ?? \"\"; }");
        ComplexityAnalyzer.CyclomaticComplexity(body).ShouldBe(2);
    }

    [Fact]
    public void CyclomaticComplexity_Ternary_Returns2()
    {
        var body = ParseMethodBody("void M() { var x = true ? 1 : 0; }");
        ComplexityAnalyzer.CyclomaticComplexity(body).ShouldBe(2);
    }

    [Fact]
    public void CyclomaticComplexity_SwitchCases_CountsEachCase()
    {
        var body = ParseMethodBody(@"
            void M() {
                switch (1) {
                    case 1: break;
                    case 2: break;
                    default: break;
                }
            }");
        ComplexityAnalyzer.CyclomaticComplexity(body).ShouldBe(3); // 1 + 2 cases
    }

    [Fact]
    public void CyclomaticComplexity_Complex_CombinesAll()
    {
        var body = ParseMethodBody(@"
            void M() {
                if (true && false) { }
                for (int i = 0; i < 10; i++) { }
                while (true || false) { }
            }");
        // 1 + if + && + for + while + || = 6
        ComplexityAnalyzer.CyclomaticComplexity(body).ShouldBe(6);
    }

    [Fact]
    public void CognitiveComplexity_EmptyMethod_Returns0()
    {
        var body = ParseMethodBody("void M() { }");
        ComplexityAnalyzer.CognitiveComplexity(body).ShouldBe(0);
    }

    [Fact]
    public void CognitiveComplexity_SingleIf_Returns1()
    {
        var body = ParseMethodBody("void M() { if (true) { } }");
        ComplexityAnalyzer.CognitiveComplexity(body).ShouldBe(1);
    }

    [Fact]
    public void CognitiveComplexity_NestedIf_IncrementsWithNesting()
    {
        var body = ParseMethodBody(@"
            void M() {
                if (true) {       // +1 (nesting 0)
                    if (false) {} // +2 (nesting 1)
                }
            }");
        ComplexityAnalyzer.CognitiveComplexity(body).ShouldBe(3);
    }

    [Fact]
    public void CognitiveComplexity_ElseAdds1()
    {
        var body = ParseMethodBody("void M() { if (true) { } else { } }");
        // if: +1, else: +1 = 2
        ComplexityAnalyzer.CognitiveComplexity(body).ShouldBe(2);
    }

    [Fact]
    public void CognitiveComplexity_ElseIf_Adds1NotNesting()
    {
        var body = ParseMethodBody("void M() { if (true) { } else if (false) { } }");
        // if: +1 (nesting 0), else: +1, inner if: +1+1 (nesting 1 from outer if) = 4
        ComplexityAnalyzer.CognitiveComplexity(body).ShouldBe(4);
    }

    [Fact]
    public void CognitiveComplexity_LogicalOperators_Add1EachNoNesting()
    {
        var body = ParseMethodBody("void M() { var x = true && false || true; }");
        // && +1, || +1 = 2
        ComplexityAnalyzer.CognitiveComplexity(body).ShouldBe(2);
    }

    [Fact]
    public void LogicalLinesOfCode_EmptyMethod_Returns1()
    {
        var body = ParseMethodBody("void M() { }");
        ComplexityAnalyzer.LogicalLinesOfCode(body).ShouldBe(1); // min 1
    }

    [Fact]
    public void LogicalLinesOfCode_CountsStatements()
    {
        var body = ParseMethodBody(@"
            void M() {
                var x = 1;
                var y = 2;
                var z = x + y;
            }");
        ComplexityAnalyzer.LogicalLinesOfCode(body).ShouldBe(3);
    }

    [Fact]
    public void LogicalLinesOfCode_IfCountsAsStatement()
    {
        var body = ParseMethodBody(@"
            void M() {
                if (true) {
                    var x = 1;
                }
            }");
        // if statement + var declaration = 2
        ComplexityAnalyzer.LogicalLinesOfCode(body).ShouldBe(2);
    }

    [Fact]
    public void MaintainabilityIndex_LowComplexity_HighScore()
    {
        var mi = ComplexityAnalyzer.MaintainabilityIndex(1, 1);
        mi.ShouldBeGreaterThan(80);
    }

    [Fact]
    public void MaintainabilityIndex_HighComplexity_LowerScore()
    {
        var mi = ComplexityAnalyzer.MaintainabilityIndex(50, 200);
        mi.ShouldBeLessThan(50);
    }

    [Fact]
    public void MaintainabilityIndex_ClampedTo0_100()
    {
        ComplexityAnalyzer.MaintainabilityIndex(1, 1).ShouldBeInRange(0, 100);
        ComplexityAnalyzer.MaintainabilityIndex(1000, 10000).ShouldBeInRange(0, 100);
    }

    [Fact]
    public void MaintainabilityIndex_ZeroInputs_DoesNotThrow()
    {
        var mi = ComplexityAnalyzer.MaintainabilityIndex(0, 0);
        mi.ShouldBeInRange(0, 100);
    }

    [Fact]
    public void CyclomaticComplexity_NullBody_Throws()
    {
        Should.Throw<ArgumentNullException>(() => ComplexityAnalyzer.CyclomaticComplexity(null!));
    }

    [Fact]
    public void CognitiveComplexity_NullBody_Throws()
    {
        Should.Throw<ArgumentNullException>(() => ComplexityAnalyzer.CognitiveComplexity(null!));
    }

    [Fact]
    public void LogicalLinesOfCode_NullBody_Throws()
    {
        Should.Throw<ArgumentNullException>(() => ComplexityAnalyzer.LogicalLinesOfCode(null!));
    }
}
