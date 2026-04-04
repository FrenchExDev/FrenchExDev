# QualityGate -- Philosophy

## Quality gates belong in the build, not in dashboards

Dashboards show metrics. They don't enforce them. A dashboard that turns red on Tuesday gets investigated on Thursday and fixed next sprint -- maybe. A quality gate that fails the build gets fixed before the PR merges.

The difference is not the metric -- it's the feedback loop. A dashboard is a lagging indicator. A build gate is an inline constraint. Constraints shape behavior; indicators inform it. Both matter, but only one prevents regressions.

---

## Measure what matters, at the right granularity

Most code quality tools report at the project or solution level. An average cyclomatic complexity of 4 across a project tells you nothing useful -- it hides the one method at 47.

QualityGate evaluates at the **natural granularity** of each metric:

- Complexity: per method
- Cohesion (LCOM4): per type
- Coupling: per type and per namespace
- Distance from main sequence: per namespace
- Coverage and mutation: global

A single violation at any level fails the gate. Averages are reported but never gated on -- they hide problems.

---

## Static analyzers are pure functions

Every analyzer in QualityGate takes Roslyn types in and returns data models out. No state. No side effects. No I/O. No DI interfaces.

This is deliberate. Pure functions are:

- **Trivially testable** -- pass a compilation, assert the result
- **Trivially composable** -- `TypeMetricsBuilder` calls `CohesionAnalyzer` and `ComplexityAnalyzer` without any wiring
- **Trivially parallelizable** -- no shared state to coordinate

The only abstractions in the project are the 4 interfaces at I/O boundaries: loading solutions, parsing reports, writing output. Everything else is a static method.

---

## Four seams, not forty

Abstraction at infrastructure boundaries. Direct calls everywhere else.

The temptation in a metrics tool is to abstract everything: an `IAnalyzer` interface, a registry of analyzers, a pipeline of stages, a plugin system. QualityGate rejects this. There are exactly 4 interfaces, all at I/O seams:

1. `ISolutionLoader` -- because MSBuild is slow and non-deterministic in tests
2. `ICoverageReportParser` -- because coverage files come from external tools
3. `IMutationReportParser` -- because mutation files come from external tools
4. `IReportWriter` -- because filesystem writes are side effects

Everything else is a direct method call. Adding a new analyzer means writing a static class and calling it from `ProjectAnalyzer` or `TypeMetricsBuilder`. No registration, no discovery, no plugin contract.

---

## Tests prove the tool, the tool proves the code

QualityGate is self-hosting: it runs its own quality gates on itself. The project maintains 100% line coverage, 100% branch coverage, and a test quality score of 1.0.

This is not vanity. A quality tool that doesn't meet its own standards has no credibility. If the thresholds are unreasonable, you'll discover it immediately because your own build breaks.

The testing approach is equally deliberate:

- **No mocking framework** -- 4 hand-written fakes for the 4 seam interfaces
- **RoslynTestHelper** -- creates in-memory compilations, no MSBuild, no filesystem
- **xUnit + Shouldly** -- readable assertions, no magic

Hand-written fakes are better than auto-generated mocks because they are explicit. You can read a fake and understand exactly what it does. A mock setup with `Returns()` and `Callback()` chains requires mental evaluation.

---

## Coverage is necessary but not sufficient

100% line coverage means every line runs during tests. It does not mean every line is tested correctly. A test that calls a method and doesn't assert anything achieves coverage without value.

Mutation testing closes the gap: it changes the code and checks whether tests notice. A high mutation score means tests are actually verifying behavior, not just executing paths.

QualityGate combines both into the **test quality score** (average of coverage rate and mutation score). This single number captures both breadth and depth of testing.

---

## Fail fast, fail completely

When validation runs, it accumulates **all** errors, not just the first one. The quality gate report lists every violation, not just the first failure. The developer gets a complete picture in one run.

This is the same principle as the Builder project's validation: callers fix all problems in one round-trip, not one at a time through trial and error.
