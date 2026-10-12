# SOLID — Philosophy

These are the SOLID principles as applied in this codebase. Follow these patterns when making design decisions.

## Interface Segregation Is the Primary Driver

Default to **1-method interfaces** at infrastructure seams. This is not aspirational — it is the standard.

Canonical example — QualityGate's 4 interfaces each have exactly one method:

```csharp
// QualityGate/src/.../Abstractions/ISolutionLoader.cs
public interface ISolutionLoader
{
    Task<Solution> LoadAsync(string solutionPath);
}

// QualityGate/src/.../Abstractions/ICoverageReportParser.cs
public interface ICoverageReportParser
{
    CoverageReport? TryParseGlobs(string baseDir, List<string>? globs);
}
```

Why: narrow interfaces make Fakes trivial to write (3-5 lines), eliminate mocking frameworks entirely, and make dependency graphs explicit.

## Dependency Inversion via Optional Constructor Params

Never require a DI container. Accept interfaces as optional constructor params with sensible defaults:

```csharp
// QualityGate/src/.../QualityEngine.cs
public QualityEngine(
    QualityGateConfig config,
    ISolutionLoader? solutionLoader = null,
    ICoverageReportParser? coverageParser = null,
    IMutationReportParser? mutationParser = null,
    IReportWriter? reportWriter = null)
{
    _solutionLoader = solutionLoader ?? new MsBuildSolutionLoader();
    _coverageParser = coverageParser ?? new DefaultCoverageReportParser();
    _mutationParser = mutationParser ?? new DefaultMutationReportParser();
    _reportWriter = reportWriter ?? new DefaultReportWriter();
}
```

Production callers pass nothing (defaults kick in). Tests inject Fakes. No service locator, no container.

## Open/Closed Through Extension Points

Add new behavior without modifying existing code. Extension points must have defaults so existing consumers work unchanged.

Key examples:
- `BuilderAttribute.Instantiation` — strategies (`init`, `ctor`, `factory:X`, `custom`) extend builder behavior without touching `BuilderEmitter.Emit()`
- `BuilderEmitModel.Preamble` — raw C# injected after class opening. Domain SGs (BinaryWrapper, DockerCompose, Diem) customize generated builders without forking the emitter
- `BuilderPropertyModel.WithMethodAttributes`, `.WithMethodBodyPrefix`, `.InstantiationExpression` — per-property extension points
- `DesignPipeline.Use()` — middleware pipeline for scraping. New steps compose without editing the runner

See: `Builder/src/FrenchExDev.Net.Builder.SourceGenerator.Lib/BuilderEmitModel.cs`

## Single Responsibility for Analyzers

Each analyzer computes **one metric family**. No God-class aggregator.

QualityGate analyzers — each is a pure static class:
- `ComplexityAnalyzer` — cyclomatic complexity, cognitive complexity, LOC, maintainability index
- `CohesionAnalyzer` — LCOM4 via union-find on method-field graph
- `CouplingAnalyzer` — afferent/efferent coupling at namespace and type level
- `InterfaceAnalyzer` — interface discovery, implementations, orphans
- `ApiSurfaceAnalyzer` — public API surface counting

See: `QualityGate/src/FrenchExDev.Net.QualityGate/Analysis/`

## Liskov via Backend Substitutability

Backend interfaces must be honoured by all implementations. `IVosBackend` lists `SupportedActions` so callers can check capabilities before invoking. Any implementation that silently ignores an action violates Liskov.

See: `Vos/src/FrenchExDev.Net.Vos/IVosBackend.cs`

## No Mocking Frameworks

Hand-written Fakes are the only test double pattern. No Moq, no NSubstitute, no FakeItEasy.

ISP makes this cheap — a 1-method interface yields a 3-line Fake:

```csharp
// QualityGate/test/.../Fakes/FakeCoverageParser.cs
internal sealed class FakeCoverageParser : ICoverageReportParser
{
    private readonly CoverageReport? _report;
    public FakeCoverageParser(CoverageReport? report = null) => _report = report;
    public CoverageReport? TryParseGlobs(string baseDir, List<string>? globs) => _report;
}
```

Fakes live in `test/.../Fakes/`. They are explicit, debuggable, and have zero magic.
