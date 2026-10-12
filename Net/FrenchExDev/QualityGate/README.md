# QualityGate

Static analysis and quality metrics tool for .NET solutions. Loads solutions via Roslyn MSBuildWorkspace, computes code metrics (complexity, cohesion, coupling, maintainability), ingests coverage and mutation reports, evaluates configurable quality gates, and produces JSON + HTML dashboard reports.

## Quick Start

```bash
# Install the local tool (first time / after clone)
dotnet tool restore

# Full workflow: run tests with coverage, analyze, report
dotnet quality-gate test

# Same but serve results in browser
dotnet quality-gate test --serve

# Interactive development loop (loop + manual trigger + serve)
dotnet quality-gate test --interactive

# Analysis only (no tests, uses existing coverage data)
dotnet quality-gate analyze

# CI/CD gate check (exit code 1 on failure)
dotnet quality-gate check
```

## Projects

| Project | TFM | Purpose |
|---------|-----|---------|
| `QualityGate` | net10.0 | Core library: Roslyn analyzers, report parsers, gate evaluation |
| `QualityGate.Cli` | net10.0 | System.CommandLine CLI, local tool packaging, WebSocket reload |
| `QualityGate.Html` | net10.0 | Scriban-templated HTML/JS/CSS dashboard generation |
| `QualityGate.Tests` | net10.0 | xUnit + Shouldly, hand-written fakes, RoslynTestHelper |

## CLI Commands

| Command | Description |
|---------|-------------|
| `init` | Scaffold `quality-gate.yml` + `coverage.runsettings` |
| `validate` | Verify config file integrity |
| `test` | Run tests with XPlat Code Coverage, then analyze quality gates |
| `coverage` | Run tests with coverage collection only (no analysis) |
| `analyze` | Analyze solution and produce quality report from existing data |
| `check` | Analyze and exit code 1 if any gate fails (CI/CD) |
| `serve` | Launch `npx serve` on the output directory |
| `interfaces` | Print interface-to-implementation mapping |

## Key Design Decisions

- **4 DI interfaces at infrastructure seams** -- `ISolutionLoader`, `ICoverageReportParser`, `IMutationReportParser`, `IReportWriter` enable unit testing without MSBuild or filesystem
- **Static analyzers are pure functions** -- no abstraction needed, they take Roslyn types and return metrics
- **No mocking framework** -- hand-written fakes in `test/.../Fakes/` for the 4 seam interfaces
- **100% line + branch coverage** -- defensive Roslyn null-checks marked `[ExcludeFromCodeCoverage]` with justification
- **256 tests, test quality score 1.0**

## Documentation

- [ARCHITECTURE.md](doc/ARCHITECTURE.md) -- project structure, analysis pipeline, model graph, extensibility
- [HOW-TO.md](doc/HOW-TO.md) -- developer guide: onboarding, dev loops, CI/CD, customization, extending
- [PHILOSOPHY.md](doc/PHILOSOPHY.md) -- why quality gates belong in the build, not in dashboards

## Building

```bash
dotnet build QualityGate/FrenchExDev.Net.QualityGate.slnx
dotnet test QualityGate/FrenchExDev.Net.QualityGate.slnx
```
