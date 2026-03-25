# QUALITY-GATES — Philosophy

## Automated Enforcement, Not Manual Review

Quality gates are machine-enforced thresholds. They run in the dev loop (`dotnet quality-gate test`) and in CI (`dotnet quality-gate check`). No human reviewer should need to eyeball cyclomatic complexity — the gate catches it.

## 100% Coverage as Design Pressure

100% line and branch coverage is the target. This is not about "testing everything" — it is about **design pressure**. When a line is hard to cover, it often means the code is poorly structured. The fix is not `[ExcludeFromCodeCoverage]` — it is refactoring.

`[ExcludeFromCodeCoverage]` is permitted only with a written justification. Coverage exclusions must be exceptional, not routine.

## Shift-Left: Dev Loop, Not Just CI

`dotnet quality-gate test` runs tests, collects coverage, and analyzes quality gates in one command. Use it before pushing, not after. The CI gate (`dotnet quality-gate check`) is a safety net, not the primary feedback loop.

## Pure Functions Over Stateful Analysis

All static analyzers (`ComplexityAnalyzer`, `CohesionAnalyzer`, etc.) are pure static classes. No constructors, no instance state, no side effects. This makes them:
- Trivially testable (pass a `Compilation`, get results)
- Parallelizable (no shared mutable state)
- Deterministic (same input = same output)

See: `QualityGate/src/FrenchExDev.Net.QualityGate/Analysis/`

## Test Quality Score

Coverage alone is insufficient. A test that covers 100 lines but asserts nothing is worthless. The test quality score (>= 0.95) measures **assertion density** — how many assertions exist relative to code paths exercised.

## Testing Without Infrastructure

All unit tests run without MSBuild, filesystem, or external services. The 4 SOLID interfaces (`ISolutionLoader`, `ICoverageReportParser`, `IMutationReportParser`, `IReportWriter`) exist specifically to cut these dependencies. Fakes provide pre-built data; `RoslynTestHelper.CreateProject()` creates in-memory compilations.

Why: MSBuild is slow, flaky, and requires a full SDK install. Tests must be fast and isolated.

See: `QualityGate/test/.../Fakes/`, `QualityGate/test/.../RoslynTestHelper.cs`

## Thresholds Are Configuration, Not Code

Gate thresholds live in `quality-gate.yml`, not hardcoded in analyzer code. This allows per-project tuning without code changes:

```yaml
gates:
  max-cyclomatic-complexity: 15
  max-cognitive-complexity: 20
  max-class-coupling: 55
  min-maintainability-index: 55
  max-lcom: 15
  min-test-quality-score: 0.95
```

See: `QualityGate/QUALITY-GATE.md`
