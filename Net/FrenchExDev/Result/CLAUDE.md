# Result — Claude Context

Lightweight, immutable Result type library for .NET — replaces exceptions and null with explicit, composable success/failure values across three sealed-record types.

## Package docs
- [README](README.md)
- [Architecture](doc/ARCHITECTURE.md)
- [How-To](doc/HOW-TO.md)
- [Philosophy](doc/PHILOSOPHY.md)
- [Comparison Table](doc/COMPARISON-TABLE.md)

## Relevant skills
- [RESULT-PATTERN](../../../Skills/Net/Programming/RESULT-PATTERN/PHILOSOPHY.md)
- [SOLID](../../../Skills/Net/Programming/SOLID/PHILOSOPHY.md)
- [Solution Layout](../../../Skills/Net/Programming/SOLUTION-LAYOUT/ARCHITECTURE.md)
- [Central Package Management](../../../Skills/Net/Programming/CENTRAL-PACKAGE-MANAGEMENT/ARCHITECTURE.md)

## Solution
- `FrenchExDev.Net.Result.slnx`

## Notes for Claude
- Three distinct types: `Result`, `Result<T>`, `Result<T, TError>` — they are NOT a class hierarchy. Never collapse them into one.
- All three are `sealed record` with `private` constructors. Construction is only via `Success(...)` / `Failure(...)`.
- `Result<T>.Failure(ValidationResult)` defensively copies `MemberNames` via `.ToArray()` — never remove this copy.
- The multi-error `Failure(IReadOnlyList<ValidationResult>)` overload is `internal` on purpose — public access goes through `Combine` or combinator propagation only.
- No implicit conversions exist anywhere — never add `T → Result<T>` operators.
- `Combine` overloads cover arities 2..7. Beyond 7, decompose the domain instead.
- Targets `net10.0` only — not backported to `netstandard2.0`.
- 47 tests at 100% branch coverage.
