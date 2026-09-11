using Microsoft.CodeAnalysis;

namespace FrenchExDev.Net.Traefik.Bundle.SourceGenerator.Analyzers;

/// <summary>
/// Central registry of diagnostic descriptors for the Traefik.Bundle analyzers
/// and the source generator. Keeping IDs and messages in one place avoids drift
/// between the analyzer assembly and the AnalyzerReleases.* manifests.
/// </summary>
internal static class TraefikDiagnostics
{
    private const string Category = "TraefikBundle";

    public static readonly DiagnosticDescriptor MultipleBranchesSet = new(
        id: "TFK001",
        title: "Discriminated union has more than one branch set",
        messageFormat: "'{0}' is a Traefik discriminated union — exactly one branch must be set, but the initializer assigns {1}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Traefik flat unions (e.g. http middleware, http service) silently reject configurations with more than one branch populated.");

    // TFK002 reserved for "dangling router → service reference" — see PLAN.md.
    // Implementing it well requires walking the assembled config graph; today's
    // shape doesn't make this cheap to detect across files, so the rule is
    // documented but not yet active.

    // TFK003 ("use of deprecated Traefik property") is intentionally subsumed
    // by the standard CS0618 warning. The model emitter stamps [Obsolete] on
    // every property where the schema sets `deprecated: true`, so the C#
    // compiler reports it natively without a custom analyzer.

    public static readonly DiagnosticDescriptor NoSchemasFound = new(
        id: "TFK004",
        title: "No Traefik schemas wired as AdditionalFiles",
        messageFormat: "The Traefik bundle source generator did not find any 'traefik-v3-*.json' AdditionalFiles. No models will be generated.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Add the embedded schemas to your csproj as <AdditionalFiles> for the generator to produce models.");
}
