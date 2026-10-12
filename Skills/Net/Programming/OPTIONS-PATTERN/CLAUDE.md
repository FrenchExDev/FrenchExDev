# OPTIONS-PATTERN — Claude Context

Explicit value absence via `Option<T>` sealed record with `T : notnull` constraint. Full functor/monad operations, LINQ query syntax, async pipeline families, collection ops (Sequence/Traverse), and bidirectional Result integration.

## Skill docs
- [Philosophy](PHILOSOPHY.md) — design rationale and trade-offs
- [Architecture](ARCHITECTURE.md) — internal structure
- [How-To](HOW-TO.md) — step-by-step tasks
- [Requirements](REQUIREMENTS.md) — formal requirements

## Related skills
- [RESULT-PATTERN](../RESULT-PATTERN/) — complement: Option for absence, Result for failure

## Related packages
- [`FrenchExDev.Net.Options`](../../../../Net/FrenchExDev/Options/)

## Notes for Claude
- Do NOT use `Option<T>` for failures — that's `Result<T>`'s job
- `Option<string?>` is a compile error (`T : notnull` constraint)
- `Some(null)` throws `ArgumentNullException` — no silent coercion to None
- `.Value` throws on None — it's the explicit "I know" escape hatch
- Implicit conversion from `T` to `Option<T>` (not from `T?`)
- Two async families: on `Option<T>` with async lambdas, and on `Task<Option<T>>` for chaining
- `Sequence()` and `Traverse()` are all-or-nothing — any None means the whole thing is None
- Property-based tests verify functor/monad laws (CsCheck, FsCheck)
