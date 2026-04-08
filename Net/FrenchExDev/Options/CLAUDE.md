# Options — Claude Context

Functional `Option<T>` type — explicit, type-safe presence/absence (not error handling). Sealed record, full functor/monad operations, LINQ query syntax, async pipelines, collection ops, and bidirectional `Result<T>` integration.

## Package docs
- [README](README.md)
- [Architecture](doc/ARCHITECTURE.md)
- [How-To](doc/HOW-TO.md)
- [Philosophy](doc/PHILOSOPHY.md)

## Relevant skills
- [OPTIONS-PATTERN](../../../Skills/Net/Programming/OPTIONS-PATTERN/PHILOSOPHY.md)
- [RESULT-PATTERN](../../../Skills/Net/Programming/RESULT-PATTERN/PHILOSOPHY.md)
- [SOLID](../../../Skills/Net/Programming/SOLID/PHILOSOPHY.md)
- [Solution Layout](../../../Skills/Net/Programming/SOLUTION-LAYOUT/ARCHITECTURE.md)
- [Central Package Management](../../../Skills/Net/Programming/CENTRAL-PACKAGE-MANAGEMENT/ARCHITECTURE.md)

## Solution
- `FrenchExDev.Net.Options.slnx`

## Notes for Claude
- `Option<T>` is for **presence/absence**, not for error handling. Use `Result<T>` for failures.
- `Option<T> where T : notnull` — never relax this constraint. `Option<string?>` must remain a compile error.
- `Option<T>` is a `sealed record` with private constructors. `Some(null)` throws — never silently coerce to None.
- LINQ query syntax (`from x in opt ...`) works via the three extensions in `LinqExtensions.cs` (`Select`/`SelectMany`/`Where`).
- Targets `netstandard2.0 + net10.0`. The `netstandard2.0` build pulls in `System.ComponentModel.Annotations` for `ValidationResult`.
- 126 tests including 13 CsCheck property-based tests verifying functor/monad laws — never remove the property tests.
- Bidirectional Result integration: `option.ToResult("error")` and `result.ToOption()`.
