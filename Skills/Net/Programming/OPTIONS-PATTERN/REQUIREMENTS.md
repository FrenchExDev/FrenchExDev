# OPTIONS-PATTERN — Requirements

## Type Requirements

- [ ] `Option<T>` is a `sealed record` with `T : notnull`.
- [ ] Private constructors — construction only via `Some(value)` or `None()` static factories.
- [ ] `Some(null)` throws `ArgumentNullException`.
- [ ] Implicit conversion from `T` to `Option<T>` (not from `T?`).
- [ ] `IsSome` / `IsNone` boolean accessors.
- [ ] `Value` throws `InvalidOperationException` on None — this is the explicit "I know what I'm doing" escape hatch.

## Match And Switch

- [ ] `Match<TResult>(Func<T, TResult> onSome, Func<TResult> onNone)` — exhaustive, both branches required.
- [ ] `Switch(Action<T> onSome, Action onNone)` — side-effect form.

## Combinators

- [ ] `Map(Func<T, TOut>)` — functor.
- [ ] `Bind(Func<T, Option<TOut>>)` and `Then` alias — monad.
- [ ] `Filter(Func<T, bool>)` — guard.
- [ ] `Tap(Action<T>)` and `TapNone(Action)` — side effects, return original.
- [ ] `OrDefault(T fallback)`, `OrElse(Func<T> factory)` — unwrap.
- [ ] `Or(Option<T>)` and `Or(Func<Option<T>>)` — alternative.
- [ ] `Zip` — tuple combine, both sides must be Some.
- [ ] `Contains(T)` and `Contains(predicate)` — boolean.
- [ ] `ToNullable()` for reference types, `ToNullableStruct()` for value types.

## Static Helpers

- [ ] `Option.Some<T>(value)` and `Option.None<T>()` for type inference.
- [ ] `Option.From<T>(T?)` for nullable references.
- [ ] `Option.FromNullable<T>(T?)` for nullable value types.
- [ ] `Option.FromTry<T>(Func<T>)` and `FromTry<T, TEx>(Func<T>)`.

## Async

- [ ] Async forms of `Map`, `Bind`, `Match`, `Tap`, `Filter` on `Option<T>`.
- [ ] Pipeline forms of all the above on `Task<Option<T>>`.

## Collections

- [ ] `Values()` — extract all Some values from `IEnumerable<Option<T>>`.
- [ ] `FirstOrNone(predicate)` and `SingleOrNone(predicate)`.
- [ ] `GetValueOrNone` for `IReadOnlyDictionary`.
- [ ] `Sequence()` — `IEnumerable<Option<T>> → Option<IReadOnlyList<T>>` (all-or-nothing).
- [ ] `Traverse(selector)` — map then sequence in one pass.

## LINQ

- [ ] `Select`, `SelectMany`, `Where` extensions enabling C# query syntax.

## Result Integration

- [ ] `ToResult(string errorMessage)` — convert None to validation failure.
- [ ] `ToResult(Func<ValidationResult>)` — lazy error factory.
- [ ] `ToResult<T, TError>(TError)` — convert to typed-error Result.
- [ ] `ToOption()` extension on `Result<T>` and `Result<T, TError>` — discards errors.

## Algebraic Properties (Property Tests)

- [ ] Functor identity: `option.Map(x => x) == option`.
- [ ] Functor composition: `option.Map(f).Map(g) == option.Map(x => g(f(x)))`.
- [ ] Monad left identity: `Option.Some(x).Bind(f) == f(x)`.
- [ ] Monad right identity: `option.Bind(x => Option.Some(x)) == option`.
- [ ] Monad associativity: `option.Bind(f).Bind(g) == option.Bind(x => f(x).Bind(g))`.

Verify these with a property-based testing library (CsCheck, FsCheck), not just example tests.

## What MUST NOT Be Done

- [ ] `Option<T>` MUST NOT be used to represent failures. Use `Result<T>`.
- [ ] `Option<T?>` MUST NOT exist (compile error via `T : notnull`).
- [ ] `Some(null)` MUST throw — never silently coerce to None.
- [ ] Public constructors MUST NOT be added.

## Multi-Targeting

- [ ] Targets `netstandard2.0` AND `net10.0` (so it works in legacy services).
- [ ] On `netstandard2.0`, depends on `System.ComponentModel.Annotations` for `ValidationResult`.

## Anchor Package

[`Net/FrenchExDev/Options/`](../../../Net/FrenchExDev/Options/) — full reference implementation.
