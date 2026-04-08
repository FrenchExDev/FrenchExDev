using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace FrenchExDev.Net.Traefik.Bundle.SourceGenerator.Analyzers;

/// <summary>
/// TFK001: flags object initializers on Traefik flat-union types where more
/// than one branch property is set in the same expression. The generated
/// builder also enforces this at runtime; the analyzer just shifts the
/// failure to compile time so common usage mistakes are caught in the IDE.
///
/// The marker we look for is the [TraefikDiscriminatedUnion] attribute that
/// the source generator stamps on flat-union model classes.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DiscriminatedUnionAnalyzer : DiagnosticAnalyzer
{
    private const string AttributeFullName =
        "FrenchExDev.Net.Traefik.Bundle.Attributes.TraefikDiscriminatedUnionAttribute";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(TraefikDiagnostics.MultipleBranchesSet);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeObjectCreation, SyntaxKind.ObjectCreationExpression);
        context.RegisterSyntaxNodeAction(AnalyzeImplicitObjectCreation, SyntaxKind.ImplicitObjectCreationExpression);
    }

    private static void AnalyzeObjectCreation(SyntaxNodeAnalysisContext ctx)
    {
        var node = (ObjectCreationExpressionSyntax)ctx.Node;
        AnalyzeInitializer(ctx, node.Initializer, node);
    }

    private static void AnalyzeImplicitObjectCreation(SyntaxNodeAnalysisContext ctx)
    {
        var node = (ImplicitObjectCreationExpressionSyntax)ctx.Node;
        AnalyzeInitializer(ctx, node.Initializer, node);
    }

    private static void AnalyzeInitializer(
        SyntaxNodeAnalysisContext ctx,
        InitializerExpressionSyntax? initializer,
        ExpressionSyntax creationNode)
    {
        if (initializer is null) return;
        if (initializer.Expressions.Count < 2) return;

        var typeInfo = ctx.SemanticModel.GetTypeInfo(creationNode, ctx.CancellationToken);
        var type = typeInfo.Type;
        if (type is null) return;
        if (!HasDiscriminatedUnionAttribute(type)) return;

        // Count how many branch property assignments are *not* explicitly null.
        var setBranches = 0;
        var firstSetName = (string?)null;
        var secondSetSpan = (Location?)null;

        foreach (var expr in initializer.Expressions)
        {
            if (expr is not AssignmentExpressionSyntax assignment) continue;
            if (assignment.Left is not IdentifierNameSyntax id) continue;
            // null literal RHS doesn't count as a branch being set.
            if (assignment.Right is LiteralExpressionSyntax lit &&
                lit.IsKind(SyntaxKind.NullLiteralExpression))
            {
                continue;
            }

            setBranches++;
            if (setBranches == 1)
            {
                firstSetName = id.Identifier.Text;
            }
            else if (setBranches == 2)
            {
                secondSetSpan = assignment.GetLocation();
            }
        }

        if (setBranches > 1 && secondSetSpan is not null)
        {
            ctx.ReportDiagnostic(Diagnostic.Create(
                TraefikDiagnostics.MultipleBranchesSet,
                secondSetSpan,
                type.Name,
                setBranches));
        }
    }

    private static bool HasDiscriminatedUnionAttribute(ITypeSymbol type)
    {
        foreach (var attr in type.GetAttributes())
        {
            var attrClass = attr.AttributeClass;
            if (attrClass is null) continue;
            if (attrClass.ToDisplayString() == AttributeFullName) return true;
        }
        return false;
    }
}
