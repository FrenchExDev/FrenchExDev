# UNION-TYPES — Requirements

## Type Requirements

- [ ] One `sealed class` per arity: `OneOf<T1, T2>`, `OneOf<T1, T2, T3>`, ...
- [ ] Each type parameter constrained `notnull`.
- [ ] Each is its OWN closed type — not a class hierarchy.
- [ ] Implements `IEquatable<OneOf<...>>`.

## Storage

- [ ] Single `private readonly object _value` field.
- [ ] Single `private readonly byte _index` field for the discriminator.
- [ ] Private constructor — only the static factories may construct.

## Construction

- [ ] Static `From(T1)`, `From(T2)`, ... factories.
- [ ] Each factory throws `ArgumentNullException` if its argument is null (defense even though constraint should prevent it).
- [ ] Implicit conversions from each type parameter to the union.

## Accessors

- [ ] `IsT1`, `IsT2`, ... boolean predicates for every case.
- [ ] `AsT1`, `AsT2`, ... typed accessors that throw `InvalidOperationException` on mismatch.

## Pattern Matching

- [ ] `Match<TResult>(Func<T1, TResult>, Func<T2, TResult>, ...)` — value-returning, exhaustive.
- [ ] `Switch(Action<T1>, Action<T2>, ...)` — void-returning, exhaustive.
- [ ] Both null-check every delegate argument.
- [ ] Both throw `InvalidOperationException("Invalid union state")` in the unreachable default branch (defensive — only fires on internal corruption).

## Escape Hatch

- [ ] `TryGet<T>(out T value)` for code that only cares about one branch.

## Equality And Hashing

- [ ] `Equals(OneOf<...>? other)` compares both `_index` and `_value`.
- [ ] `Equals(object?)` overload delegates to typed Equals.
- [ ] `GetHashCode()` combines `_index` and `_value.GetHashCode()`.

## ToString

- [ ] Returns `T{i+1}({value})` form for debugging.
- [ ] Uses `FormattableString.Invariant` to avoid culture-sensitive formatting.

## Multi-Targeting

- [ ] Targets `netstandard2.0 + net10.0` (works in legacy services).
- [ ] On `netstandard2.0`, manual hash combine instead of `HashCode.Combine`.

## What MUST NOT Be Done

- [ ] No public constructor.
- [ ] No `null` allowed (constraint + factory check).
- [ ] No `OneOf<object, object>` style usage — type parameters must be specific.
- [ ] No use of `OneOf` for success/failure — use `Result<T>` instead.
- [ ] No arity beyond ~5 cases (becomes unreadable; suggests a different abstraction).

## Test Double / Assertions

- [ ] `Union.Testing` package with `ShouldBeT1`, `ShouldBeT2`, ... assertions.
- [ ] Each assertion throws a clear message naming the actual case if the assertion fails.

## Anchor Package

[`Net/FrenchExDev/Union/`](../../../Net/FrenchExDev/Union/) — implements every requirement.
