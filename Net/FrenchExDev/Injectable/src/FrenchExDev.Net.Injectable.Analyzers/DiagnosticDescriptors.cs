using Microsoft.CodeAnalysis;

namespace FrenchExDev.Net.Injectable.Analyzers;

internal static class DiagnosticDescriptors
{
    public static readonly DiagnosticDescriptor CaptiveDependency = new(
        id: "INJECT001",
        title: "Captive dependency detected",
        messageFormat: "'{0}' is registered as {1} but depends on '{2}' which is registered as {3} — this creates a captive dependency",
        category: "Injectable.Lifetime",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "A longer-lived service holds a reference to a shorter-lived service, " +
                     "preventing the shorter-lived service from being garbage collected or refreshed. " +
                     "Singleton → Scoped and Singleton → Transient are the most common violations.",
        customTags: new[] { WellKnownDiagnosticTags.CompilationEnd });

    public static readonly DiagnosticDescriptor AbstractClassDecorated = new(
        id: "INJECT002",
        title: "[Injectable] on abstract class has no effect",
        messageFormat: "'{0}' is abstract and will be silently skipped by the Injectable source generator — remove [Injectable] or make the class non-abstract",
        category: "Injectable.Usage",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "The Injectable source generator ignores abstract classes. " +
                     "Decorating one with [Injectable] is a no-op that may confuse maintainers.");

    public static readonly DiagnosticDescriptor MissingInjectable = new(
        id: "INJECT003",
        title: "Class implements interface but is not decorated with [Injectable]",
        messageFormat: "'{0}' implements '{1}' but is not decorated with [Injectable] — consider adding [Injectable] for automatic DI registration",
        category: "Injectable.Usage",
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "A non-abstract class implements an interface that looks like a service contract " +
                     "but is not registered via [Injectable]. This may be intentional (e.g., DTOs, test doubles) " +
                     "or an oversight.");

    public static readonly DiagnosticDescriptor InterfaceScopeMismatch = new(
        id: "INJECT004",
        title: "Implementation scope violates interface contract",
        messageFormat: "'{0}' is registered as {1} but its interface '{2}' declares [Injectable(Scope = {3})] — all implementations must follow the interface scope contract",
        category: "Injectable.Lifetime",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "When an interface is decorated with [Injectable], it declares a scope contract. " +
                     "All implementing classes must use the same scope. " +
                     "This prevents accidental lifetime mismatches across implementations of the same service.");
}
