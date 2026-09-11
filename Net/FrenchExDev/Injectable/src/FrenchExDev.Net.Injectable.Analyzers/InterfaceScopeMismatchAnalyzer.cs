using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace FrenchExDev.Net.Injectable.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class InterfaceScopeMismatchAnalyzer : DiagnosticAnalyzer
{
    private const string AttributeFullName = "FrenchExDev.Net.Injectable.Attributes.InjectableAttribute";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(DiagnosticDescriptors.InterfaceScopeMismatch);

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

            if (classSymbol.IsAbstract || classSymbol.TypeKind != TypeKind.Class)
                return;

            // Get this class's [Injectable] scope (if any)
            string? classScope = null;
            bool classScopeIsExplicit = false;

            foreach (var attr in classSymbol.GetAttributes())
            {
                if (!SymbolEqualityComparer.Default.Equals(attr.AttributeClass, attrSymbol))
                    continue;

                classScope = "Transient"; // default
                foreach (var arg in attr.NamedArguments)
                {
                    if (arg.Key == "Scope" && arg.Value.Value is int scopeInt)
                    {
                        classScope = ScopeIntToString(scopeInt);
                        classScopeIsExplicit = true;
                        break;
                    }
                }
                break;
            }

            // Only report if the class explicitly declares a different scope
            // (classes without [Injectable] are inferred — no conflict)
            if (classScope is null || !classScopeIsExplicit)
                return;

            // Check each implemented interface for [Injectable] scope contract
            foreach (var iface in classSymbol.AllInterfaces)
            {
                foreach (var attr in iface.GetAttributes())
                {
                    if (!SymbolEqualityComparer.Default.Equals(attr.AttributeClass, attrSymbol))
                        continue;

                    var interfaceScope = "Transient";
                    foreach (var arg in attr.NamedArguments)
                    {
                        if (arg.Key == "Scope" && arg.Value.Value is int scopeInt)
                        {
                            interfaceScope = ScopeIntToString(scopeInt);
                            break;
                        }
                    }

                    if (classScope != interfaceScope)
                    {
                        ctx.ReportDiagnostic(Diagnostic.Create(
                            DiagnosticDescriptors.InterfaceScopeMismatch,
                            classSymbol.Locations.FirstOrDefault(),
                            classSymbol.Name,
                            classScope,
                            iface.Name,
                            interfaceScope));
                    }
                }
            }
        }, SymbolKind.NamedType);
    }

    private static string ScopeIntToString(int scopeInt)
    {
        switch (scopeInt)
        {
            case 0: return "Transient";
            case 1: return "Scoped";
            case 2: return "Singleton";
            default: return "Transient";
        }
    }
}
