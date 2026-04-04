# QualityGate -- Architecture

## 1. Overview

QualityGate is a Roslyn-based static analysis tool that computes code metrics, ingests external coverage and mutation reports, evaluates configurable quality gates, and produces JSON + HTML dashboard reports. It is packaged as a .NET local tool (`quality-gate`) with a System.CommandLine CLI.

The architecture separates **analysis** (pure functions of Roslyn types), **infrastructure** (4 DI interfaces), **evaluation** (pure function of model types), and **presentation** (CLI + HTML dashboard).

---

## 2. Project Structure

```
QualityGate/
  FrenchExDev.Net.QualityGate.slnx
  quality-gate.yml
  coverage.runsettings
  src/
    FrenchExDev.Net.QualityGate/            (Core library)
    FrenchExDev.Net.QualityGate.Cli/        (CLI + local tool)
    FrenchExDev.Net.QualityGate.Html/        (Scriban HTML dashboard)
  test/
    FrenchExDev.Net.QualityGate.Tests/       (xUnit + Shouldly)
```

| Project | TFM | Purpose |
|---------|-----|---------|
| **QualityGate** | net10.0 | Roslyn analyzers, report parsers, gate evaluator, data models |
| **QualityGate.Cli** | net10.0 | System.CommandLine CLI, WebSocket live-reload server, tool packaging |
| **QualityGate.Html** | net10.0 | Scriban templating, static HTML/JS/CSS embedded resources |
| **QualityGate.Tests** | net10.0 | 256 tests, RoslynTestHelper for in-memory compilations, 4 fakes |

---

## 3. Dependency Graph

```
QualityGate.Cli
  -> QualityGate (core)
  -> QualityGate.Html (report generation)

QualityGate.Html
  -> QualityGate (core)

QualityGate.Tests
  -> QualityGate (core, InternalsVisibleTo)
  -> QualityGate.Cli
  -> QualityGate.Html
```

---

## 4. Core Library Architecture

### Orchestrator

```
QualityEngine (orchestrates all analysis)
  |-- ISolutionLoader        -> MsBuildSolutionLoader
  |-- ICoverageReportParser  -> DefaultCoverageReportParser (Cobertura XML)
  |-- IMutationReportParser  -> DefaultMutationReportParser (Stryker JSON)
  |-- IReportWriter          -> DefaultReportWriter (filesystem + JSON)
  |
  |-- ProjectAnalyzer (static, per-project entry point)
  |     |-- InterfaceAnalyzer      (interface discovery, implementations, orphans)
  |     |-- ApiSurfaceAnalyzer     (public type/method/property counts)
  |     |-- CouplingAnalyzer       (afferent/efferent at namespace + type level)
  |     +-- TypeMetricsBuilder     (aggregates per-type metrics from compilation)
  |           |-- CohesionAnalyzer    (LCOM4 via union-find on method-field graph)
  |           +-- ComplexityAnalyzer  (cyclomatic, cognitive, LOC, maintainability index)
  |
  +-- QualityGateEvaluator (static, pure function of model types)
```

### Infrastructure Interfaces (4 seams)

These are the only abstractions in the project -- placed at I/O boundaries to enable unit testing without MSBuild or filesystem:

| Interface | Default Implementation | Purpose |
|-----------|----------------------|---------|
| `ISolutionLoader` | `MsBuildSolutionLoader` | Load Roslyn `Solution` from .slnx/.sln |
| `ICoverageReportParser` | `DefaultCoverageReportParser` | Parse Cobertura XML coverage globs |
| `IMutationReportParser` | `DefaultMutationReportParser` | Parse Stryker JSON mutation globs |
| `IReportWriter` | `DefaultReportWriter` | Write report.json + summary.txt |

### Static Analyzers (pure functions -- no abstraction needed)

| Analyzer | Input | Output |
|----------|-------|--------|
| `ComplexityAnalyzer` | Method syntax + semantic model | Cyclomatic, cognitive complexity, LOC, maintainability index |
| `CohesionAnalyzer` | Type declaration + semantic model | LCOM4 via union-find on method-field graph |
| `CouplingAnalyzer` | Compilation | Afferent/efferent coupling per namespace and type |
| `InterfaceAnalyzer` | Compilation | Interface list, implementations, orphan interfaces |
| `ApiSurfaceAnalyzer` | Compilation | Public type/method/property counts |
| `TypeMetricsBuilder` | Compilation | Aggregated per-type metrics (calls all above) |
| `QualityGateEvaluator` | QualityReport + GateThresholds | List of pass/fail gate results |

---

## 5. Analysis Pipeline (End-to-End)

```
1. Load Solution       MsBuildSolutionLoader -> Roslyn Solution
2. Filter Projects     Exclude transitive deps, apply exclude patterns
3. Per-Project:
   a. Compile          Roslyn Compilation
   b. InterfaceAnalyzer     -> interfaces, implementations, orphans
   c. ApiSurfaceAnalyzer    -> public API surface
   d. CouplingAnalyzer      -> namespace coupling graph
   e. TypeMetricsBuilder    -> per-type metrics
      - CohesionAnalyzer       -> LCOM4
      - ComplexityAnalyzer     -> per-method complexity
4. Parse Reports
   a. CoberturaParser       -> merge coverage.cobertura.xml globs
   b. StrykerReportParser   -> aggregate mutation-report.json globs
5. Evaluate Gates      QualityGateEvaluator compares metrics vs thresholds
6. Write Reports       report.json + summary.txt + HTML dashboard
```

---

## 6. Data Model Graph

```
QualityReport
  |-- ProjectMetrics[]
  |     |-- NamespaceMetrics[]     (abstractness, instability, distance)
  |     |     +-- TypeMetrics[]    (LCOM4, coupling, inheritance depth)
  |     |           +-- MethodMetrics[]  (cyclomatic, cognitive, MI, LOC)
  |     |-- InterfaceInfo[]
  |     |-- InterfaceImplementation[]
  |     |-- ProjectDependency[]
  |     +-- ApiSurface
  |-- CoverageReport              (line rate, branch rate, per-class)
  |-- MutationReport              (score, killed/survived/no-cov/timeout)
  |-- DuplicationReport           (%, clone groups)
  +-- QualityGateResult[]         (gate name, threshold, actual, passed)
```

All models are JSON-serializable C# records.

---

## 7. Quality Gates

| Gate | Scope | Default |
|------|-------|---------|
| `max-cyclomatic-complexity` | per method | 15 |
| `max-cognitive-complexity` | per method | 20 |
| `max-class-coupling` | per type (efferent) | 55 |
| `max-inheritance-depth` | per type | 5 |
| `min-maintainability-index` | per method | 55 |
| `max-lcom` | per type | 15 |
| `max-distance-from-main-sequence` | per namespace | 1.0 |
| `max-duplication-percent` | global | 5 |
| `min-test-quality-score` | global (coverage + mutation avg) | 0.95 |

---

## 8. CLI Architecture

`Program.cs` defines System.CommandLine commands and options. Each command delegates to `QualityEngine` or helper methods. The `--serve` flag launches `WebSocketReloadServer` (HTTP listener + WebSocket push for live reload).

### Loop/Watch modes

| Flag | Behavior |
|------|----------|
| `--loop` | Re-run after completion |
| `--watch` | Trigger re-run on file changes (FileSystemWatcher) |
| `--manual` | Trigger re-run on Enter key |
| `--interactive` | Shorthand for `--loop --manual --serve` |

---

## 9. HTML Dashboard

`QualityGate.Html` embeds static HTML/JS/CSS as resources. `IndexGenerator` writes the static shell. `RunReportGenerator` uses Scriban templates to render per-run dashboards from the `QualityReport` model.

---

## 10. Test Architecture

- **xUnit + Shouldly** -- fluent assertions, no mocking framework
- **RoslynTestHelper** -- `Compile()`, `CompileType()`, `CreateProject()` for in-memory compilations
- **4 Fakes** -- `FakeSolutionLoader`, `FakeCoverageParser`, `FakeMutationParser`, `FakeReportWriter`
- **256 tests** -- 100% line + branch coverage, test quality score 1.0
- Defensive Roslyn null-checks marked `[ExcludeFromCodeCoverage]` with justification

### Extensibility

**Adding a new analyzer:**
1. Create static class in `Analysis/` (pure function of Roslyn types)
2. Call from `ProjectAnalyzer.AnalyzeAsync()` or `TypeMetricsBuilder`
3. Add result to model class
4. If needs gate: add threshold to `GateThresholds`, check in `QualityGateEvaluator`
5. Write tests using `RoslynTestHelper`

**Adding a new quality gate:**
1. Add threshold property to `GateThresholds` with `[YamlMember]`
2. Add evaluation logic in `QualityGateEvaluator`
3. Write tests in `QualityGateEvaluatorTests`
4. Update `quality-gate.yml`
