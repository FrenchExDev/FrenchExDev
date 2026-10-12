using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace FrenchExDev.Net.Injectable.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AbstractClassDecoratedAnalyzer : DiagnosticAnalyzer
{
    private const string AttributeFullName = "FrenchExDev.Net.Injectable.Attributes.InjectableAttribute";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(DiagnosticDescriptors.AbstractClassDecorated);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(OnCompilationStart);
    }

    private static void OnCompilationStart(CompilationStartAnalysisContext context)
    {
        var attrSymbol = context.Compilation.GetTypeByMetadataName(AttributeFullName);
        if (attrSymbol is null)
            return;

        context.RegisterSymbolAction(ctx =>
        {
            if (ctx.Symbol is not INamedTypeSymbol classSymbol)
                return;

            if (!classSymbol.IsAbstract)
                return;

            foreach (var attr in classSymbol.GetAttributes())
            {
                if (SymbolEqualityComparer.Default.Equals(attr.AttributeClass, attrSymbol))
                {
                    ctx.ReportDiagnostic(Diagnostic.Create(
                        DiagnosticDescriptors.AbstractClassDecorated,
                        attr.ApplicationSyntaxReference?.GetSyntax(ctx.CancellationToken).GetLocation()
                            ?? classSymbol.Locations[0],
                        classSymbol.Name));
                    break;
                }
            }
        }, SymbolKind.NamedType);
    }
}
