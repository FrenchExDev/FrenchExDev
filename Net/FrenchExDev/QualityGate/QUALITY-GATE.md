# FrenchExDev.Net.QualityGate

Static analysis and quality metrics tool for .NET solutions. Loads solutions via Roslyn MSBuildWorkspace, computes code metrics, ingests coverage/mutation reports, evaluates quality gates, and produces JSON + HTML reports.

## Quick Start

```bash
cd Net/FrenchExDev/QualityGate

# Full workflow: run tests with coverage, analyze, report
dotnet quality-gate test

# Same but serve results in browser
dotnet quality-gate test --serve

# Analysis only (no tests, uses existing coverage data)
dotnet quality-gate analyze

# CI/CD gate check (exit code 1 on failure)
dotnet quality-gate check
```

### Installing the local tool

The CLI is packaged as a .NET local tool. To install (or reinstall after cloning):

```bash
dotnet tool restore
```

### Updating the tool after code changes

After modifying the CLI or any of its dependencies, re-pack and update:

```bash
dotnet pack src/FrenchExDev.Net.QualityGate.Cli -o /tmp/quality-gate-tool
dotnet tool update FrenchExDev.Net.QualityGate.Cli --add-source /tmp/quality-gate-tool
```

## CLI Commands

| Command | Description |
|---|---|
| `test` | Run tests with XPlat Code Coverage, then analyze quality gates |
| `coverage` | Run tests with coverage collection only (no analysis) |
| `analyze` | Analyze solution and produce quality report (uses existing coverage data) |
| `check` | Analyze and exit with code 1 if any gate fails (CI/CD) |
| `serve` | Launch `npx serve` on the output directory (no analysis) |
| `interfaces` | Print interface-to-implementation mapping |

### Common Options

| Option | Commands | Description |
|---|---|---|
| `--config <path>` | all | Path to `quality-gate.yml` (default: cwd) |
| `--solution <path>` | all except serve | Override solution path |
| `--serve` | test, analyze | Launch browser server after analysis |
| `--settings <path>` | test, coverage | Path to `coverage.runsettings` |
| `--port <int>` | serve | Server port (default: 3000) |

## Configuration

### quality-gate.yml

```yaml
solution: FrenchExDev.Net.QualityGate.slnx

coverage:
  - "**/coverage.cobertura.xml"

mutations:
  - "**/mutation-report.json"

output: .quality-gate/

gates:
  max-cyclomatic-complexity: 15
  max-cognitive-complexity: 20
  max-class-coupling: 55
  max-inheritance-depth: 5
  min-maintainability-index: 55
  max-lcom: 15
  max-distance-from-main-sequence: 1.0
  max-duplication-percent: 5
  min-test-quality-score: 0.95
```

### coverage.runsettings

Controls which assemblies are included in coverage measurement:

```xml
<Include>[FrenchExDev.Net.QualityGate]*,[FrenchExDev.Net.QualityGate.Html]*</Include>
<ExcludeByAttribute>ExcludeFromCodeCoverageAttribute,CompilerGeneratedAttribute</ExcludeByAttribute>
```

## Architecture

### Core Library (`FrenchExDev.Net.QualityGate`)

```
QualityEngine (orchestrator, accepts DI interfaces)
  |-- ISolutionLoader        -> MsBuildSolutionLoader (MSBuild)
  |-- ICoverageReportParser  -> DefaultCoverageReportParser (Cobertura XML)
  |-- IMutationReportParser  -> DefaultMutationReportParser (Stryker JSON)
  |-- IReportWriter          -> DefaultReportWriter (filesystem)
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

### Interfaces (Abstractions/)

4 interfaces at infrastructure seams — enables unit testing without MSBuild or filesystem:

- `ISolutionLoader` — loads Roslyn `Solution` from path
- `ICoverageReportParser` — parses coverage report globs
- `IMutationReportParser` — parses mutation report globs
- `IReportWriter` — writes report files to output directory

### Static Analyzers (no abstraction needed — pure functions)

- `ComplexityAnalyzer` — cyclomatic, cognitive complexity, LOC, maintainability index
- `CohesionAnalyzer` — LCOM4 via union-find on method-field graph
- `CouplingAnalyzer` — afferent/efferent coupling at namespace and type level
- `InterfaceAnalyzer` — interface discovery, implementations, orphans
- `ApiSurfaceAnalyzer` — public API surface counting
- `TypeMetricsBuilder` — aggregates per-type metrics from compilation
- `QualityGateEvaluator` — evaluates thresholds against report model

## Testing

### Running Tests

```bash
# All tests
dotnet test

# With coverage
dotnet quality-gate test

# Specific test class
dotnet test --filter "FullyQualifiedName~ComplexityAnalyzerTests"
```

### Test Patterns

- **No mocking framework** — uses hand-written fakes and direct construction
- **RoslynTestHelper** (`test/.../RoslynTestHelper.cs`) — creates in-memory compilations and projects via `AdhocWorkspace`
- **Fakes** (`test/.../Fakes/`) — `FakeSolutionLoader`, `FakeCoverageParser`, `FakeMutationParser`, `FakeReportWriter`
- **xUnit + Shouldly** for assertions

### Coverage Targets

- **Line coverage**: 100%
- **Branch coverage**: 100%
- **Test quality score**: >= 0.95
- Defensive branches on Roslyn null-checks marked `[ExcludeFromCodeCoverage]` with justification comments

### Adding a New Test

For Roslyn-based analyzers:
```csharp
var project = RoslynTestHelper.CreateProject("namespace Ns { public class C { } }");
var result = await SomeAnalyzer.AnalyzeAsync(project);
result.ShouldNotBeNull();
```

For QualityEngine (no MSBuild):
```csharp
var engine = new QualityEngine(
    new QualityGateConfig { Solution = "fake.slnx" },
    solutionLoader: FakeSolutionLoader.WithSource("class C {}"),
    coverageParser: new FakeCoverageParser(someCoverage));
var report = await engine.AnalyzeAsync();
```

## Adding a New Analyzer

1. Create static class in `Analysis/` with analysis method taking Roslyn types
2. Call it from `ProjectAnalyzer.AnalyzeAsync()` or `TypeMetricsBuilder.BuildTypeMetrics()`
3. Add result to appropriate model class (`ProjectMetrics`, `TypeMetrics`, etc.)
4. If it needs a quality gate, add threshold to `GateThresholds` and check in `QualityGateEvaluator`
5. Write tests using `RoslynTestHelper.CreateProject()`

## Adding a New Quality Gate

1. Add threshold property to `Config/QualityGateConfig.cs` (`GateThresholds` class) with `[YamlMember]` attribute
2. Add evaluation logic in `Gates/QualityGateEvaluator.cs` (in the appropriate `Evaluate*` method)
3. Add tests in `QualityGateEvaluatorTests.cs`
4. Update `quality-gate.yml` with the new threshold

## Project References

```
FrenchExDev.Net.QualityGate.Cli
  -> FrenchExDev.Net.QualityGate (core)
  -> FrenchExDev.Net.QualityGate.Html (report generation)

FrenchExDev.Net.QualityGate.Html
  -> FrenchExDev.Net.QualityGate (core)

FrenchExDev.Net.QualityGate.Tests
  -> FrenchExDev.Net.QualityGate (core, InternalsVisibleTo)
  -> FrenchExDev.Net.QualityGate.Cli
  -> FrenchExDev.Net.QualityGate.Html
```

## Key Packages (Central Package Management)

Versions in `Net/FrenchExDev/Directory.Packages.props` — never add `Version=` to `.csproj`.
