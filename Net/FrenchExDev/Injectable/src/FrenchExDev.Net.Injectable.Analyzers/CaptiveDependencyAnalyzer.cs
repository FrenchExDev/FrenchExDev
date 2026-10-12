using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace FrenchExDev.Net.Injectable.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class CaptiveDependencyAnalyzer : DiagnosticAnalyzer
{
    private const string AttributeFullName = "FrenchExDev.Net.Injectable.Attributes.InjectableAttribute";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(DiagnosticDescriptors.CaptiveDependency);

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

        // Phase 1: collect all [Injectable] classes and their scopes
        var scopeMap = new ConcurrentDictionary<INamedTypeSymbol, string>(SymbolEqualityComparer.Default);

        context.RegisterSymbolAction(ctx =>
        {
            if (ctx.Symbol is not INamedTypeSymbol classSymbol)
                return;

            foreach (var attr in classSymbol.GetAttributes())
            {
                if (!SymbolEqualityComparer.Default.Equals(attr.AttributeClass, attrSymbol))
                    continue;

                var scope = "Transient";
                foreach (var arg in attr.NamedArguments)
                {
                    if (arg.Key == "Scope" && arg.Value.Value is int scopeInt)
                    {
                        switch (scopeInt)
                        {
                            case 0: scope = "Transient"; break;
                            case 1: scope = "Scoped"; break;
                            case 2: scope = "Singleton"; break;
                        }
                    }
                }
                scopeMap[classSymbol] = scope;
            }
        }, SymbolKind.NamedType);

        // Phase 2: check constructor dependencies for captive violations
        context.RegisterCompilationEndAction(endCtx =>
        {
            foreach (var entry in scopeMap)
            {
                var classSymbol = entry.Key;
                var outerScope = entry.Value;

                if (outerScope != "Singleton")
                    continue;

                // Check primary constructor parameters and instance constructors
                foreach (var ctor in classSymbol.Constructors)
                {
                    foreach (var param in ctor.Parameters)
                    {
                        var paramType = param.Type as INamedTypeSymbol;
                        if (paramType is null)
                            continue;

                        // Find if any [Injectable] class implements this parameter's interface
                        foreach (var inner in scopeMap)
                        {
                            var candidate = inner.Key;
                            var innerScope = inner.Value;

                            if (innerScope == "Singleton")
                                continue;

                            var isCaptive = false;

                            // Direct match
                            if (SymbolEqualityComparer.Default.Equals(candidate, paramType))
                                isCaptive = true;

                            // Interface match
                            if (!isCaptive)
                            {
                                foreach (var iface in candidate.AllInterfaces)
                                {
                                    if (SymbolEqualityComparer.Default.Equals(iface, paramType))
                                    {
                                        isCaptive = true;
                                        break;
                                    }
                                }
                            }

                            if (isCaptive)
                            {
                                var diagnostic = Diagnostic.Create(
                                    DiagnosticDescriptors.CaptiveDependency,
                                    classSymbol.Locations.FirstOrDefault(),
                                    classSymbol.Name,
                                    outerScope,
                                    candidate.Name,
                                    innerScope);
                                endCtx.ReportDiagnostic(diagnostic);
                            }
                        }
                    }
                }
            }
        });
    }
}
