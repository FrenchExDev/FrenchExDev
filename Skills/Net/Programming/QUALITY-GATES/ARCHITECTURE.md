# QUALITY-GATES — Architecture

## Component Diagram

```
QualityEngine (orchestrator, accepts DI interfaces)
  |-- ISolutionLoader        --> MsBuildSolutionLoader (MSBuild)
  |-- ICoverageReportParser  --> DefaultCoverageReportParser (Cobertura XML)
  |-- IMutationReportParser  --> DefaultMutationReportParser (Stryker JSON)
  |-- IReportWriter          --> DefaultReportWriter (filesystem)
  |
  |-- ProjectAnalyzer (static, per-project)
  |     |-- InterfaceAnalyzer (static, Roslyn)
  |     |-- ApiSurfaceAnalyzer (static, Roslyn)
  |     |-- CouplingAnalyzer (static, Roslyn)
  |     +-- TypeMetricsBuilder (static, Roslyn)
  |           |-- CohesionAnalyzer (static, Roslyn)
  |           +-- ComplexityAnalyzer (static, pure syntax walk)
  |
  +-- QualityGateEvaluator (static, pure function of model types)
```

## Data Flow

```
quality-gate.yml --> QualityGateConfig
                        |
Solution path --------> QualityEngine.AnalyzeAsync()
                           |
                           |--> ISolutionLoader.LoadAsync() --> Roslyn Solution
                           |
                           |--> ProjectAnalyzer (per project in solution)
                           |      |--> TypeMetricsBuilder --> per-type metrics
                           |      |--> InterfaceAnalyzer --> interface map
                           |      |--> CouplingAnalyzer --> coupling metrics
                           |      +-- ApiSurfaceAnalyzer --> API surface
                           |
                           |--> ICoverageReportParser.TryParseGlobs() --> CoverageReport?
                           |--> IMutationReportParser.TryParseGlobs() --> MutationReport?
                           |
                           |--> QualityGateEvaluator.Evaluate() --> QualityGateResult
                           |
                           +-- QualityReport (JSON model)
                                  |
                                  +--> IReportWriter.WriteAsync() --> HTML + JSON files
```

## Static Analyzer Catalog

| Analyzer | Metrics | Input | Location |
|----------|---------|-------|----------|
| `ComplexityAnalyzer` | Cyclomatic complexity, cognitive complexity, LOC, maintainability index | `SyntaxNode` | `Analysis/ComplexityAnalyzer.cs` |
| `CohesionAnalyzer` | LCOM4 via union-find on method-field graph | `INamedTypeSymbol` | `Analysis/CohesionAnalyzer.cs` |
| `CouplingAnalyzer` | Afferent/efferent coupling at namespace and type level | `Compilation` | `Analysis/CouplingAnalyzer.cs` |
| `InterfaceAnalyzer` | Interface discovery, implementations, orphans | `Compilation` | `Analysis/InterfaceAnalyzer.cs` |
| `ApiSurfaceAnalyzer` | Public API surface counting | `Compilation` | `Analysis/ApiSurfaceAnalyzer.cs` |
| `TypeMetricsBuilder` | Aggregates per-type metrics from all analyzers | `Compilation` | `Analysis/TypeMetricsBuilder.cs` |
| `QualityGateEvaluator` | Evaluates thresholds against report model | `QualityReport` + `GateThresholds` | `Gates/QualityGateEvaluator.cs` |

All analyzers are **pure static classes** in `QualityGate/src/FrenchExDev.Net.QualityGate/Analysis/`.

## 4 SOLID Interfaces

| Interface | Method | Default | Fake |
|-----------|--------|---------|------|
| `ISolutionLoader` | `LoadAsync(string)` | `MsBuildSolutionLoader` | `FakeSolutionLoader` |
| `ICoverageReportParser` | `TryParseGlobs(string, List<string>?)` | `DefaultCoverageReportParser` | `FakeCoverageParser` |
| `IMutationReportParser` | `TryParseGlobs(string, List<string>?)` | `DefaultMutationReportParser` | `FakeMutationParser` |
| `IReportWriter` | `WriteAsync(QualityReport, string, CancellationToken)` | `DefaultReportWriter` | `FakeReportWriter` |

See: `QualityGate/src/.../Abstractions/`, `QualityGate/test/.../Fakes/`

## CLI Commands

| Command | Description |
|---------|-------------|
| `dotnet quality-gate test` | Run tests + coverage + analyze quality gates |
| `dotnet quality-gate test --serve` | Same + serve results in browser |
| `dotnet quality-gate analyze` | Analysis only (uses existing coverage data) |
| `dotnet quality-gate check` | CI/CD gate check (exit code 1 on failure) |
| `dotnet quality-gate coverage` | Run tests with coverage collection only |
| `dotnet quality-gate serve` | Launch browser server on output directory |
| `dotnet quality-gate interfaces` | Print interface-to-implementation mapping |

## Key Files

- `QualityGate/QUALITY-GATE.md` — canonical documentation
- `QualityGate/src/FrenchExDev.Net.QualityGate/QualityEngine.cs` — orchestrator
- `QualityGate/src/FrenchExDev.Net.QualityGate/Analysis/` — all static analyzers
- `QualityGate/src/FrenchExDev.Net.QualityGate/Gates/QualityGateEvaluator.cs` — gate evaluation
- `QualityGate/src/FrenchExDev.Net.QualityGate/Abstractions/` — 4 interfaces
- `QualityGate/test/.../Fakes/` — 4 hand-written fakes
- `QualityGate/test/.../RoslynTestHelper.cs` — in-memory Roslyn compilations
