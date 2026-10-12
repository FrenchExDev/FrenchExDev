# UNION-TYPES — Claude Context

Discriminated unions via `OneOf<T1, T2>` through `OneOf<T1, ..., T5>` sealed classes. Type-safe exhaustive matching without class hierarchies. Internal byte discriminator, implicit conversions, `Match`/`Switch`/`TryGet` APIs.

## Skill docs
- [Philosophy](PHILOSOPHY.md) — design rationale and trade-offs
- [Architecture](ARCHITECTURE.md) — internal structure
- [How-To](HOW-TO.md) — step-by-step tasks
- [Requirements](REQUIREMENTS.md) — formal requirements

## Related skills
- [RESULT-PATTERN](../RESULT-PATTERN/) — Result for success/failure; Union for closed sets of 3+ distinct outcomes

## Related packages
- [`FrenchExDev.Net.Union`](../../../../Net/FrenchExDev/Union/)
- [`FrenchExDev.Net.Union.Testing`](../../../../Net/FrenchExDev/Union/)

## Notes for Claude
- NOT for success/failure — use `Result<T>` for that
- Upper bound ~4-5 cases for readability
- Equality: same case AND equal underlying values
- `ToString()` produces `T1(value)` / `T2(value)` form for debugging
- `netstandard2.0` needs manual hash combine (no `HashCode.Combine`)
- `notnull` constraint on each type parameter
