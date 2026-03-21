namespace FrenchExDev.Net.Requirements.Analyzers;

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

/// <summary>
/// Placeholder for REQ1xx-REQ3xx analyzers. Will be implemented with full diagnostics.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class RequirementCoverageAnalyzer : DiagnosticAnalyzer
{
    public static readonly DiagnosticDescriptor REQ100 = new DiagnosticDescriptor(
        id: "REQ100",
        title: "Feature has no specification",
        messageFormat: "{0} has {1} acceptance criteria but no specification interface references it",
        category: "Requirements",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(REQ100);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        // Full implementation in a future phase
    }
}
