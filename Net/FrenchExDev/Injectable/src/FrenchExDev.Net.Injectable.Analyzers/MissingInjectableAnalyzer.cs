using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace FrenchExDev.Net.Injectable.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MissingInjectableAnalyzer : DiagnosticAnalyzer
{
    private const string AttributeFullName = "FrenchExDev.Net.Injectable.Attributes.InjectableAttribute";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(DiagnosticDescriptors.MissingInjectable);

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

            // Skip abstract, static, and generated classes
            if (classSymbol.IsAbstract || classSymbol.IsStatic)
                return;

            // Skip if already decorated with [Injectable]
            foreach (var attr in classSymbol.GetAttributes())
            {
                if (SymbolEqualityComparer.Default.Equals(attr.AttributeClass, attrSymbol))
                    return;
            }

            // Skip classes that don't implement any "service-like" interfaces
            // Heuristic: interface name starts with "I" and is followed by an uppercase letter
            var serviceInterface = classSymbol.Interfaces
                .FirstOrDefault(i => IsServiceInterface(i, classSymbol));

            if (serviceInterface is null)
                return;

            // Skip if in a test assembly (common convention)
            var assemblyName = ctx.Compilation.AssemblyName ?? "";
            if (assemblyName.Contains("Test") || assemblyName.Contains("Mock") || assemblyName.Contains("Fake"))
                return;

            ctx.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.MissingInjectable,
                classSymbol.Locations[0],
                classSymbol.Name,
                serviceInterface.Name));
        }, SymbolKind.NamedType);
    }

    private static bool IsServiceInterface(INamedTypeSymbol iface, INamedTypeSymbol implementor)
    {
        var name = iface.Name;

        // Must follow I{Name} convention
        if (name.Length < 2 || name[0] != 'I' || !char.IsUpper(name[1]))
            return false;

        // Skip common non-service interfaces
        if (name == "IDisposable" || name == "IAsyncDisposable" ||
            name == "IEquatable" || name == "IComparable" ||
            name == "IEnumerable" || name == "IEnumerator" ||
            name == "ICloneable" || name == "IFormattable" ||
            name == "IConvertible" || name == "ISerializable")
            return false;

        // The interface should be in the same assembly or a referenced assembly
        // (skip BCL interfaces by checking namespace)
        var ns = iface.ContainingNamespace?.ToDisplayString() ?? "";
        if (ns.StartsWith("System") || ns.StartsWith("Microsoft"))
            return false;

        return true;
    }
}
