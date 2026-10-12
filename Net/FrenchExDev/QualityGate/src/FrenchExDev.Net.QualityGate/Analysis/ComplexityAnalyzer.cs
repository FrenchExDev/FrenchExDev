using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace FrenchExDev.Net.QualityGate.Analysis;

/// <summary>
/// Computes cyclomatic complexity, cognitive complexity, logical lines of code,
/// and maintainability index by walking a Roslyn syntax tree.
/// </summary>
public static class ComplexityAnalyzer
{
    /// <summary>
    /// Compute cyclomatic complexity for a method body.
    /// CC = 1 + number of decision points.
    /// </summary>
    public static int CyclomaticComplexity(SyntaxNode methodBody)
    {
        ArgumentNullException.ThrowIfNull(methodBody);

        int complexity = 1;

        foreach (var node in methodBody.DescendantNodes())
        {
            if (IsDecisionPoint(node))
                complexity++;
        }

        return complexity;
    }

    /// <summary>
    /// Compute cognitive complexity (nesting-aware).
    /// Each decision point adds (1 + current nesting depth).
    /// </summary>
    public static int CognitiveComplexity(SyntaxNode methodBody)
    {
        ArgumentNullException.ThrowIfNull(methodBody);

        int total = 0;
        ComputeCognitive(methodBody, 0, ref total);
        return total;
    }

    /// <summary>
    /// Compute logical lines of code (counts statement nodes, excludes blank lines and comments).
    /// </summary>
    public static int LogicalLinesOfCode(SyntaxNode node)
    {
        ArgumentNullException.ThrowIfNull(node);

        int count = 0;
        foreach (var descendant in node.DescendantNodes())
        {
            if (descendant is StatementSyntax && descendant is not BlockSyntax)
                count++;
        }

        return Math.Max(count, 1);
    }

    /// <summary>
    /// Compute maintainability index (clamped to [0, 100]).
    /// </summary>
    public static double MaintainabilityIndex(int cyclomaticComplexity, int linesOfCode)
    {
        if (linesOfCode <= 0) linesOfCode = 1;
        if (cyclomaticComplexity <= 0) cyclomaticComplexity = 1;

        double vocabulary = Math.Max(2.0 * linesOfCode, 2.0);
        double halsteadVolume = Math.Max(linesOfCode * Math.Log2(vocabulary), 1.0);

        double mi = 171.0
                   - 5.2 * Math.Log(halsteadVolume)
                   - 0.23 * cyclomaticComplexity
                   - 16.2 * Math.Log(linesOfCode);

        return Math.Clamp(mi * 100.0 / 171.0, 0.0, 100.0);
    }

    private static bool IsDecisionPoint(SyntaxNode node)
    {
        return node switch
        {
            IfStatementSyntax => true,
            ForStatementSyntax => true,
            ForEachStatementSyntax => true,
            WhileStatementSyntax => true,
            DoStatementSyntax => true,
            CatchClauseSyntax => true,
            ConditionalExpressionSyntax => true,
            CaseSwitchLabelSyntax => true,
            CasePatternSwitchLabelSyntax => true,
            ConditionalAccessExpressionSyntax => true,
            BinaryExpressionSyntax binary => IsLogicalOperator(binary),
            _ => false
        };
    }

    private static bool IsLogicalOperator(BinaryExpressionSyntax binary)
    {
        return binary.IsKind(SyntaxKind.LogicalAndExpression)
            || binary.IsKind(SyntaxKind.LogicalOrExpression)
            || binary.IsKind(SyntaxKind.CoalesceExpression);
    }

    private static void ComputeCognitive(SyntaxNode node, int nesting, ref int total)
    {
        foreach (var child in node.ChildNodes())
        {
            var (isIncrement, isNesting) = ClassifyCognitiveNode(child);

            if (child is ElseClauseSyntax)
                total += 1;
            else if (isIncrement)
                total += 1 + nesting;

            ComputeCognitive(child, isNesting ? nesting + 1 : nesting, ref total);
        }
    }

    private static (bool IsIncrement, bool IsNesting) ClassifyCognitiveNode(SyntaxNode child)
    {
        return child switch
        {
            IfStatementSyntax => (true, true),
            ForStatementSyntax => (true, true),
            ForEachStatementSyntax => (true, true),
            WhileStatementSyntax => (true, true),
            DoStatementSyntax => (true, true),
            SwitchStatementSyntax => (true, true),
            CatchClauseSyntax => (true, true),
            ConditionalExpressionSyntax => (true, true),
            LambdaExpressionSyntax => (false, true),
            AnonymousMethodExpressionSyntax => (false, true),
            BinaryExpressionSyntax binary when IsLogicalOperator(binary) => (true, false),
            ConditionalAccessExpressionSyntax => (true, false),
            _ => (false, false)
        };
    }
}
