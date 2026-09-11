# RESULT-PATTERN — Claude Context

Expected failures as return values, not exceptions. Three types: `Result` (void commands), `Result<T>` (with `IReadOnlyList<ValidationResult>` errors), `Result<T,TError>` (typed domain errors). Immutable sealed records. Combinators in sync, async, and pipeline (on `Task<Result>`) forms.

## Skill docs
- [Philosophy](PHILOSOPHY.md) — design rationale and trade-offs
- [Architecture](ARCHITECTURE.md) — internal structure
- [How-To](HOW-TO.md) — step-by-step tasks
- [Requirements](REQUIREMENTS.md) — formal requirements

## Related skills
- [DDD](../DDD/) — invariant results
- [SAGA-PATTERN](../SAGA-PATTERN/) — step results
- [REQUIREMENTS](../REQUIREMENTS/) — AcceptanceCriterionResult
- [GUARD-CLAUSES](../GUARD-CLAUSES/) — ToResult bridge

## Related packages
- [`FrenchExDev.Net.Result`](../../../../Net/FrenchExDev/Result/)

## Notes for Claude
- No implicit conversion from `T` to `Result<T>` — construction is explicit via factories
- Defensive copy of `ValidationResult.MemberNames` at construction time
- Multi-error constructor is `internal` — public path is `Combine` or combinator propagation
- `ValueOrThrow()` throws `InvalidOperationException`, not the stored error
- `FromTry<T, TError>` catches only the specified exception type — controlled boundary
- `ValidationResult.MemberNames` is NEVER null (SDK returns `Enumerable.Empty<string>()`)
