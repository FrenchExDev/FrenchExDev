# Union — Claude Context

Discriminated union types — `OneOf<T1, T2>`, `OneOf<T1, T2, T3>`, `OneOf<T1, T2, T3, T4>` — for operations with three or more distinct outcome types where exhaustive pattern matching matters.

## Package docs
- [README](README.md)
- [Architecture](doc/ARCHITECTURE.md)
- [How-To](doc/HOW-TO.md)
- [Philosophy](doc/PHILOSOPHY.md)

## Relevant skills
- [UNION-TYPES](../../../Skills/Net/Programming/UNION-TYPES/PHILOSOPHY.md)
- [SOLID](../../../Skills/Net/Programming/SOLID/PHILOSOPHY.md)
- [Solution Layout](../../../Skills/Net/Programming/SOLUTION-LAYOUT/ARCHITECTURE.md)
- [Central Package Management](../../../Skills/Net/Programming/CENTRAL-PACKAGE-MANAGEMENT/ARCHITECTURE.md)

## Solution
- `FrenchExDev.Net.Union.slnx`

## Notes for Claude
- README and `doc/*.md` files are currently empty stubs — derive intent from `src/FrenchExDev.Net.Union/OneOf{N}.cs`.
- Each arity (`OneOf2`, `OneOf3`, `OneOf4`) is its OWN sealed class — they are NOT a class hierarchy.
- All type parameters are constrained `notnull`. `Some(null)` throws `ArgumentNullException`. Never relax this.
- Internal storage is `object _value` + `byte _index`. Private constructor — only the static `From(...)` factories may construct.
- Implicit conversions exist from each `Tn` to the union — keep them, they're how callers return values without `OneOf<>.From(...)` boilerplate.
- Use `Result<T, TError>` for success/failure (two outcomes). Use `OneOf` only when there are 3+ distinct outcome types.
- Multi-targets `netstandard2.0 + net10.0`. The `netstandard2.0` build uses manual hash combine instead of `HashCode.Combine`.
