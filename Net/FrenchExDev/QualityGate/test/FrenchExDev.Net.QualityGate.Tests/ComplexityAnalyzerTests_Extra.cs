using FrenchExDev.Net.QualityGate.Analysis;

using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using Shouldly;

namespace FrenchExDev.Net.QualityGate.Tests;

public class ComplexityAnalyzerTests_Extra
{
    private static Microsoft.CodeAnalysis.SyntaxNode ParseMethodBody(string methodCode)
    {
        var tree = CSharpSyntaxTree.ParseText($"class C {{ {methodCode} }}");
        var root = tree.GetRoot();
        var method = root.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .First();
        return method.Body!;
    }

    [Fact]
    public void CyclomaticComplexity_ConditionalAccess_Increments()
    {
        var body = ParseMethodBody("void M() { string s = null; var x = s?.Length; }");
        // 1 + conditional access = 2
        ComplexityAnalyzer.CyclomaticComplexity(body).ShouldBe(2);
    }

    [Fact]
    public void CyclomaticComplexity_CasePatternSwitchLabel_Increments()
    {
        var body = ParseMethodBody(@"
            void M() {
                object o = 1;
                switch (o) {
                    case int i: break;
                    case string s: break;
                }
            }");
        // 1 + 2 pattern cases = 3
        ComplexityAnalyzer.CyclomaticComplexity(body).ShouldBe(3);
    }

    [Fact]
    public void CyclomaticComplexity_DoStatement_Increments()
    {
        var body = ParseMethodBody("void M() { do { } while (false); }");
        ComplexityAnalyzer.CyclomaticComplexity(body).ShouldBe(2);
    }

    [Fact]
    public void CognitiveComplexity_SwitchStatement_IncrementsWithNesting()
    {
        var body = ParseMethodBody(@"
            void M() {
                switch (1) {    // +1 (nesting 0)
                    case 1: break;
                    case 2: break;
                }
            }");
        // switch: +1 (at nesting 0)
        ComplexityAnalyzer.CognitiveComplexity(body).ShouldBe(1);
    }

    [Fact]
    public void CognitiveComplexity_CatchClause_IncrementsWithNesting()
    {
        var body = ParseMethodBody(@"
            void M() {
                try { }
                catch (System.Exception) { }  // +1 (nesting 0)
            }");
        ComplexityAnalyzer.CognitiveComplexity(body).ShouldBe(1);
    }

    [Fact]
    public void CognitiveComplexity_NestedCatch_IncrementsWithNesting()
    {
        var body = ParseMethodBody(@"
            void M() {
                if (true) {                       // +1 (nesting 0)
                    try { }
                    catch (System.Exception) { }  // +2 (nesting 1)
                }
            }");
        ComplexityAnalyzer.CognitiveComplexity(body).ShouldBe(3);
    }

    [Fact]
    public void CognitiveComplexity_Lambda_IncreasesNestingNoIncrement()
    {
        var body = ParseMethodBody(@"
            void M() {
                System.Func<int, bool> f = x => {
                    if (x > 0) { return true; }  // +2 (nesting 1 from lambda)
                    return false;
                };
            }");
        // lambda: no increment, but nesting +1
        // if inside lambda: +1 + nesting(1) = +2
        ComplexityAnalyzer.CognitiveComplexity(body).ShouldBe(2);
    }

    [Fact]
    public void CognitiveComplexity_DoStatement_IncrementsWithNesting()
    {
        var body = ParseMethodBody("void M() { do { } while (false); }");
        // do: +1 (at nesting 0)
        ComplexityAnalyzer.CognitiveComplexity(body).ShouldBe(1);
    }

    [Fact]
    public void CognitiveComplexity_NestedDoStatement_IncrementsWithNesting()
    {
        var body = ParseMethodBody(@"
            void M() {
                if (true) {           // +1 (nesting 0)
                    do { } while (false); // +2 (nesting 1)
                }
            }");
        ComplexityAnalyzer.CognitiveComplexity(body).ShouldBe(3);
    }

    [Fact]
    public void CognitiveComplexity_ConditionalAccessExpression_IncrementsNoNesting()
    {
        var body = ParseMethodBody("void M() { string s = null; var x = s?.ToString(); }");
        // conditional access: +1, no nesting increase
        ComplexityAnalyzer.CognitiveComplexity(body).ShouldBe(1);
    }

    [Fact]
    public void CognitiveComplexity_ConditionalExpression_IncrementsWithNesting()
    {
        var body = ParseMethodBody("void M() { var x = true ? 1 : 0; }");
        // ternary: +1 (nesting 0)
        ComplexityAnalyzer.CognitiveComplexity(body).ShouldBe(1);
    }

    [Fact]
    public void CognitiveComplexity_ForLoop_IncrementsWithNesting()
    {
        var body = ParseMethodBody(@"
            void M() {
                for (int i = 0; i < 10; i++) {   // +1 (nesting 0)
                    for (int j = 0; j < 5; j++) { // +2 (nesting 1)
                    }
                }
            }");
        ComplexityAnalyzer.CognitiveComplexity(body).ShouldBe(3);
    }

    [Fact]
    public void CognitiveComplexity_ForEachLoop_IncrementsWithNesting()
    {
        var body = ParseMethodBody(@"
            void M() {
                foreach (var x in new int[0]) {  // +1 (nesting 0)
                    if (x > 0) { }               // +2 (nesting 1)
                }
            }");
        ComplexityAnalyzer.CognitiveComplexity(body).ShouldBe(3);
    }

    [Fact]
    public void CognitiveComplexity_WhileLoop_IncrementsWithNesting()
    {
        var body = ParseMethodBody(@"
            void M() {
                while (true) {     // +1 (nesting 0)
                    if (true) { }  // +2 (nesting 1)
                }
            }");
        ComplexityAnalyzer.CognitiveComplexity(body).ShouldBe(3);
    }

    [Fact]
    public void MaintainabilityIndex_NegativeInputs_ClampedTo0_100()
    {
        var mi = ComplexityAnalyzer.MaintainabilityIndex(-5, -10);
        mi.ShouldBeInRange(0.0, 100.0);
    }

    [Fact]
    public void LogicalLinesOfCode_MultipleStatementTypes()
    {
        var body = ParseMethodBody(@"
            void M() {
                var x = 1;
                if (x > 0) { x++; }
                for (int i = 0; i < 10; i++) { x += i; }
                return;
            }");
        // var, if, x++, for, x+=i, return = 6
        ComplexityAnalyzer.LogicalLinesOfCode(body).ShouldBe(6);
    }
}
