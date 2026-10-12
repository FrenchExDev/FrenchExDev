# SOLID — Architecture

## Interface Segregation Map

| Interface | Method | Default Implementation | Fake | Project |
|-----------|--------|----------------------|------|---------|
| `ISolutionLoader` | `LoadAsync(string)` | `MsBuildSolutionLoader` | `FakeSolutionLoader` | QualityGate |
| `ICoverageReportParser` | `TryParseGlobs(string, List<string>?)` | `DefaultCoverageReportParser` | `FakeCoverageParser` | QualityGate |
| `IMutationReportParser` | `TryParseGlobs(string, List<string>?)` | `DefaultMutationReportParser` | `FakeMutationParser` | QualityGate |
| `IReportWriter` | `WriteAsync(QualityReport, string, CancellationToken)` | `DefaultReportWriter` | `FakeReportWriter` | QualityGate |
| `IVosBackend` | multiple (see Liskov section) | `VagrantBackend` | — | Vos |
| `IVersionCollector` | `CollectAsync(...)` | `GitHubReleasesVersionCollector` | — | BinaryWrapper.Design.Lib |
| `IHelpParser` | `Parse(string)` | per-tool implementations | — | BinaryWrapper.Design.Lib |

Interfaces live in `src/.../Abstractions/`. Fakes live in `test/.../Fakes/`.

## Dependency Inversion Topology

```
QualityEngine (orchestrator)
  |-- ISolutionLoader?       --> MsBuildSolutionLoader
  |-- ICoverageReportParser? --> DefaultCoverageReportParser
  |-- IMutationReportParser? --> DefaultMutationReportParser
  +-- IReportWriter?         --> DefaultReportWriter

VosOrchestrator (orchestrator)
  +-- IVosBackend            --> VagrantBackend

DesignPipelineRunner (orchestrator)
  |-- IVersionCollector      --> GitHubReleasesVersionCollector / GitLabReleasesVersionCollector
  +-- IHelpParser            --> VagrantHelpParser / PodmanHelpParser / GlabHelpParser
```

Pattern: orchestrator accepts abstractions via constructor. Optional params (`?`) with `?? new Default...()` for production defaults. Required params for backends where no default makes sense.

## Open/Closed Extension Points

| Extension Point | Location | How It Extends |
|----------------|----------|---------------|
| `BuilderAttribute.Instantiation` | `Builder/src/.../BuilderAttribute.cs` | `init`, `ctor`, `factory:X`, `custom` strategies — `BuilderEmitter` generates different `CreateInstance()` bodies without modification |
| `BuilderEmitModel.Preamble` | `Builder/src/.../SourceGenerator.Lib/BuilderEmitModel.cs` | Raw C# injected after class opening brace. Domain SGs insert custom fields/methods |
| `BuilderPropertyModel.WithMethodAttributes` | same file | `[Attributes]` added to generated `With*()` methods |
| `BuilderPropertyModel.WithMethodBodyPrefix` | same file | Code injected before the assignment in `With*()` |
| `BuilderPropertyModel.InstantiationExpression` | same file | Custom expression in `CreateInstance()` for this property |
| `DesignPipeline.Use()` | `BinaryWrapper/src/.../Design.Lib/DesignPipeline.cs` | Middleware functions compose scraping steps |

All extension points have defaults (null or empty). Existing consumers are unaffected when a new point is added.

## SRP Boundary Catalog

Each static analyzer in QualityGate computes exactly one metric family:

| Class | Responsibility | Location |
|-------|---------------|----------|
| `ComplexityAnalyzer` | Cyclomatic complexity, cognitive complexity, LOC, maintainability index | `QualityGate/src/.../Analysis/ComplexityAnalyzer.cs` |
| `CohesionAnalyzer` | LCOM4 via union-find on method-field graph | `CohesionAnalyzer.cs` |
| `CouplingAnalyzer` | Afferent/efferent coupling at namespace and type level | `CouplingAnalyzer.cs` |
| `InterfaceAnalyzer` | Interface discovery, implementations, orphans | `InterfaceAnalyzer.cs` |
| `ApiSurfaceAnalyzer` | Public API surface counting | `ApiSurfaceAnalyzer.cs` |
| `TypeMetricsBuilder` | Aggregates per-type metrics from compilation | `TypeMetricsBuilder.cs` |

These are **pure static classes** — no constructors, no instance state, no side effects. Input in, output out.

## Liskov Contracts

`IVosBackend` advertises capabilities via `SupportedActions`:

```csharp
// Vos/src/FrenchExDev.Net.Vos/IVosBackend.cs
public interface IVosBackend
{
    string Name { get; }
    IReadOnlySet<string> SupportedActions { get; }

    // Mutations
    Task<VosActionResult> UpAsync(ResolvedInstance instance, CancellationToken ct = default);
    Task<VosActionResult> HaltAsync(...);
    Task<VosActionResult> DestroyAsync(...);

    // Queries
    Task<VosActionResult> StatusAsync(...);
    Task<VosActionResult> SshAsync(...);
}
```

Callers check `SupportedActions` before invoking. Unsupported actions return `VosError.UnsupportedAction` — they never silently succeed or throw unexpected exceptions.
