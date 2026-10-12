# QUALITY-GATES — Requirements

Non-negotiable rules for quality gate enforcement.

## Coverage

- **100% line and branch coverage is the target.** No exceptions without `[ExcludeFromCodeCoverage]` and a written justification comment explaining why coverage is impossible or meaningless.
- **Test quality score must be >= 0.95.** Assertion density matters — tests that cover code but assert nothing are insufficient.

## Testing

- **All unit tests must run without MSBuild.** Use `RoslynTestHelper.CreateProject()` to create in-memory Roslyn compilations. Never depend on `dotnet build` or filesystem access in unit tests.
- **Use hand-written Fakes, not mocking frameworks.** See SOLID skill for the pattern. Fakes live in `test/.../Fakes/`.
- **xUnit + Shouldly** for test framework and assertions.

## Analyzers

- **Every analyzer must be a pure static class.** No constructor, no instance state, no side effects. Input `Compilation` or `SyntaxNode`, output metrics.
- **One analyzer per metric family.** Never combine unrelated metrics in a single class.
- **New analyzers must have corresponding gate evaluations.** Adding an analyzer without a threshold check in `QualityGateEvaluator` is incomplete work.

## Configuration

- **Gate thresholds must be configured in `quality-gate.yml`.** Never hardcode thresholds in analyzer code. Thresholds are per-project configuration.
- **Coverage globs must be configured in `quality-gate.yml`.** Pattern: `"**/coverage.cobertura.xml"`.
- **Coverage scope must be configured in `coverage.runsettings`.** Include only the assemblies under test, exclude generated code and test assemblies.

## CLI

- **`dotnet quality-gate test` must pass before any PR.** This is the single command that runs tests, collects coverage, and evaluates all gates.
- **`dotnet quality-gate check` is the CI gate.** Exit code 1 on any gate failure. Use this in CI pipelines.

## Adding New Gates

- **Every new gate needs:** a threshold key in `quality-gate.yml`, evaluation logic in `QualityGateEvaluator`, a corresponding analyzer (if metric is new), and tests covering both pass and fail scenarios.
