# Guard — Claude Context

Guard clause library with two failure modes: `Guard.Against` throws standard .NET exceptions for system boundaries; `Guard.ToResult` returns `Result<T>` for use inside functional pipelines.

## Package docs
- [README](README.md)
- [Architecture](doc/ARCHITECTURE.md)
- [How-To](doc/HOW-TO.md)
- [Philosophy](doc/PHILOSOPHY.md)

## Relevant skills
- [GUARD-CLAUSES](../../../Skills/Net/Programming/GUARD-CLAUSES/PHILOSOPHY.md)
- [RESULT-PATTERN](../../../Skills/Net/Programming/RESULT-PATTERN/PHILOSOPHY.md)
- [SOLID](../../../Skills/Net/Programming/SOLID/PHILOSOPHY.md)
- [Solution Layout](../../../Skills/Net/Programming/SOLUTION-LAYOUT/ARCHITECTURE.md)
- [Central Package Management](../../../Skills/Net/Programming/CENTRAL-PACKAGE-MANAGEMENT/ARCHITECTURE.md)

## Solution
- `FrenchExDev.Net.Guard.slnx`

## Notes for Claude
- Three modes: `Guard.Against` (throws), `Guard.ToResult` (returns `Result<T>`), `Guard.Ensure` (invariants — throws `InvalidOperationException`).
- Mirror API surface: same guard names exist in all modes. Switching is a one-word change at the call site.
- Every guard returns the validated value — enables inline assignment `_clock = Guard.Against.Null(clock);`.
- Throwing-mode methods use `[CallerArgumentExpression(nameof(value))]` for auto parameter names — there's a polyfill in `Polyfills.cs` for `netstandard2.0`.
- Targets `netstandard2.0` AND `net10.0` so guards work in legacy services.
- Never use `Guard.Against.*` inside a method that returns `Result<T>` — it will throw and break the pipeline. Use `Guard.ToResult.*` instead.
- README and doc/*.md files are currently empty stubs — derive intent from `src/FrenchExDev.Net.Guard/*.cs`.
