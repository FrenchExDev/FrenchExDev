# QualityGate -- Developer Guide (HOW-TO)

This guide covers onboarding, development workflows, CI/CD integration, customization, and extending the tool.

---

## Table of Contents

1. [Onboarding](#1-onboarding)
2. [Development Loops](#2-development-loops)
3. [CI/CD Integration](#3-cicd-integration)
4. [Configuration](#4-configuration)
5. [Understanding the Report](#5-understanding-the-report)
6. [Running Tests](#6-running-tests)
7. [Extending: New Analyzer](#7-extending-new-analyzer)
8. [Extending: New Quality Gate](#8-extending-new-quality-gate)
9. [Ratcheting Strategy](#9-ratcheting-strategy)

---

## 1. Onboarding

### Prerequisites

- .NET 10.0 SDK
- Node.js (for `npx serve` in `--serve` mode)

### First-time setup

```bash
cd Net/FrenchExDev/QualityGate

# Restore the local tool
dotnet tool restore

# Scaffold config files (if not already present)
dotnet quality-gate init

# Run the full pipeline
dotnet quality-gate test --serve
```

This runs all tests with XPlat Code Coverage, analyzes the solution, evaluates quality gates, and opens the HTML dashboard in your browser.

### Installing / updating the tool after code changes

```bash
dotnet pack src/FrenchExDev.Net.QualityGate.Cli -o /tmp/quality-gate-tool
dotnet tool update FrenchExDev.Net.QualityGate.Cli --add-source /tmp/quality-gate-tool
```

---

## 2. Development Loops

### Interactive mode (recommended)

```bash
dotnet quality-gate test --interactive
```

This combines `--loop --manual --serve`: runs the full pipeline, opens the dashboard, and waits for you to press Enter to re-run. The browser auto-refreshes via WebSocket.

### Watch mode

```bash
dotnet quality-gate test --loop --watch --serve
```

Triggers re-analysis on file changes (FileSystemWatcher).

### Analysis-only loop

```bash
dotnet quality-gate analyze --loop --manual --serve
```

Skips test execution -- useful when iterating on code metrics and you already have coverage data.

---

## 3. CI/CD Integration

### Gate check (exit code 1 on failure)

```bash
dotnet quality-gate check
```

Use this in CI pipelines. Returns exit code 0 if all gates pass, 1 if any gate fails.

### Full pipeline in CI

```bash
dotnet quality-gate test --config quality-gate.yml
```

Pair with artifact upload for the `.quality-gate/` output directory to preserve reports.

---

## 4. Configuration

### quality-gate.yml

```yaml
solution: FrenchExDev.Net.QualityGate.slnx

coverage:
  - "**/coverage.cobertura.xml"

mutations:
  - "**/mutation-report.json"

output: .quality-gate/

exclude:
  - "**/obj/**"
  - "**/bin/**"

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

Controls coverage collection scope:

```xml
<Include>[FrenchExDev.Net.QualityGate]*,[FrenchExDev.Net.QualityGate.Html]*</Include>
<ExcludeByAttribute>ExcludeFromCodeCoverageAttribute,CompilerGeneratedAttribute</ExcludeByAttribute>
```

### Common CLI options

| Option | Commands | Description |
|--------|----------|-------------|
| `--config <path>` | all | Path to `quality-gate.yml` (default: cwd) |
| `--solution <path>` | all except serve | Override solution path |
| `--serve` | test, analyze | Launch browser server after analysis |
| `--settings <path>` | test, coverage | Path to `coverage.runsettings` |
| `--port <int>` | serve | Server port (default: 3000) |

---

## 5. Understanding the Report

The output directory (default `.quality-gate/`) contains:

- `report.json` -- full analysis data (all metrics, coverage, mutations, gate results)
- `summary.txt` -- human-readable pass/fail summary
- `index.html` -- static dashboard entry point
- `run-dashboard.html` -- per-run Scriban-rendered report

### Key metrics explained

| Metric | What it measures | Why it matters |
|--------|-----------------|----------------|
| Cyclomatic complexity | Number of linearly independent paths through a method | High values = hard to test and reason about |
| Cognitive complexity | Perceived difficulty of understanding a method | Better than cyclomatic for human readability |
| LCOM4 | Lack of Cohesion of Methods (connected components) | High values = class should be split |
| Maintainability index | Composite: volume, complexity, LOC | Below 55 = hard to maintain |
| Distance from main sequence | Balance between abstractness and instability | 0 = balanced, 1 = zone of pain/uselessness |
| Test quality score | Average of coverage rate and mutation score | Combined confidence in test suite |

---

## 6. Running Tests

```bash
# All tests
dotnet test QualityGate/FrenchExDev.Net.QualityGate.slnx

# Specific test class
dotnet test --filter "FullyQualifiedName~ComplexityAnalyzerTests"

# With coverage via quality-gate
dotnet quality-gate test
```

### Test patterns

- **No mocking framework** -- hand-written fakes at the 4 infrastructure seams
- **RoslynTestHelper** -- creates in-memory compilations via `AdhocWorkspace`
- **xUnit + Shouldly** for fluent assertions

```csharp
// Analyzer test
var project = RoslynTestHelper.CreateProject("namespace Ns { public class C { } }");
var result = await SomeAnalyzer.AnalyzeAsync(project);
result.ShouldNotBeNull();

// QualityEngine test (no MSBuild)
var engine = new QualityEngine(
    new QualityGateConfig { Solution = "fake.slnx" },
    solutionLoader: FakeSolutionLoader.WithSource("class C {}"),
    coverageParser: new FakeCoverageParser(someCoverage));
var report = await engine.AnalyzeAsync();
```

---

## 7. Extending: New Analyzer

1. Create a static class in `Analysis/` -- pure function of Roslyn types (Compilation, SemanticModel, SyntaxNode)
2. Call it from `ProjectAnalyzer.AnalyzeAsync()` or `TypeMetricsBuilder.BuildTypeMetrics()`
3. Add the result to the appropriate model class (`ProjectMetrics`, `TypeMetrics`, `MethodMetrics`)
4. If it needs a quality gate, see next section
5. Write tests using `RoslynTestHelper.CreateProject()` or `RoslynTestHelper.Compile()`

---

## 8. Extending: New Quality Gate

1. Add threshold property to `GateThresholds` in `Config/QualityGateConfig.cs` with `[YamlMember]`
2. Add evaluation logic in `Gates/QualityGateEvaluator.cs` (in the appropriate `Evaluate*` method)
3. Write tests in `QualityGateEvaluatorTests.cs`
4. Update `quality-gate.yml` with the new threshold key and default value

---

## 9. Ratcheting Strategy

Start with relaxed thresholds and tighten them over time:

1. Run `dotnet quality-gate analyze` to see current metric values
2. Set gates slightly tighter than current worst values
3. Fix violations, tighten thresholds, repeat
4. Target: all gates green in CI before merge

This prevents regressions while allowing incremental improvement on legacy codebases.
