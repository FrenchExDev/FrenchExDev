# Options -- Architecture

## 1. Overview

Options provides a functional `Option<T>` type that explicitly models the presence or absence of a value. The core type is a sealed record with private constructors, created via `Some` and `None` factory methods. Five extension classes provide functor/monad operations, async pipelines, collection processing, LINQ query syntax, and bidirectional `Result<T>` integration.

---

## 2. Project Structure

```
Options/
  FrenchExDev.Net.Options.slnx
  quality-gate.yml
  coverage.runsettings
  src/
    FrenchExDev.Net.Options/                       (Core — netstandard2.0 + net10.0)
      Option.cs                                    Option<T> sealed record + Option static helpers
      Extensions.cs                                Map, Bind, Filter, Tap, Or, Zip, Contains
      AsyncExtensions.cs                           Async Map/Bind/Match/Tap/Filter/OrDefault
      CollectionExtensions.cs                      Values, FirstOrNone, SingleOrNone, Sequence, Traverse
      LinqExtensions.cs                            Select, SelectMany, Where (LINQ query syntax)
      ResultIntegration.cs                         ToResult, ToOption, OrResult, BindResult
    FrenchExDev.Net.Options.Testing/               (Test helpers — netstandard2.0 + net10.0)
      OptionAssertions.cs                          ShouldBeSome, ShouldBeNone, ShouldBeSomeAnd
  test/
    FrenchExDev.Net.Options.Tests/                 (xUnit + CsCheck)
      OptionTests.cs                               26 tests: Some/None/From/FromTry/Match/Switch/Equality
      ExtensionsTests.cs                           35 tests: Map/Bind/Filter/Tap/Or/Zip/Contains
      AsyncExtensionsTests.cs                      14 tests: async pipelines
      CollectionExtensionsTests.cs                 16 tests: Values/FirstOrNone/Sequence/Traverse
      LinqExtensionsTests.cs                       8 tests: LINQ query syntax
      ResultIntegrationTests.cs                    14 tests: Option<->Result conversions
      PropertyTests.cs                             13 tests: CsCheck property-based tests
```

---

## 3. Dependency Graph

```
FrenchExDev.Net.Result                             (external project reference)
  |
  +-- FrenchExDev.Net.Options                      (refs Result, netstandard2.0 + net10.0)
  |     |
  |     +-- FrenchExDev.Net.Options.Testing        (refs Options)
  |     |
  |     +-- FrenchExDev.Net.Options.Tests          (refs Options + Testing + xUnit + CsCheck)
```

The only project dependency is `FrenchExDev.Net.Result` for the bidirectional integration. On `netstandard2.0`, `System.ComponentModel.Annotations` is also referenced for `ValidationResult`.

---

## 4. Core Type Design

### Option<T> (sealed record, T : notnull)

```
Option<T>
  |-- private readonly T? _value
  |-- private readonly bool _isSome
  |
  |-- IsSome: bool              (property)
  |-- IsNone: bool              (property)
  |-- Value: T                  (throws if None)
  |
  |-- Some(T): Option<T>        (static factory, throws on null)
  |-- None(): Option<T>         (static factory)
  |
  |-- Match<TResult>(onSome, onNone): TResult
  |-- Switch(onSome, onNone): void
  |
  |-- implicit operator Option<T>(T value)  → Some(value)
  |-- ToString()  → "Some(42)" or "None"
```

Private constructors enforce creation via factories. `Some(null)` throws `ArgumentNullException`. The `notnull` constraint prevents `Option<string?>`.

### Option (static helper class)

| Method | Purpose |
|--------|---------|
| `Some<T>(value)` | Create Some without explicit type parameter |
| `None<T>()` | Create None without explicit type parameter |
| `From<T>(T?)` | Nullable reference → Some or None |
| `FromNullable<T>(T?)` | Nullable value type → Some or None |
| `FromTry<T>(Func<T>)` | Factory that may throw → Some or None |
| `FromTry<T, TEx>(Func<T>)` | Catch only specific exception type |

---

## 5. Extension Organization

### Functor / Monad (Extensions.cs)

| Extension | Category | Signature |
|-----------|----------|-----------|
| `Map` | Functor | `Option<T> → (T → TOut) → Option<TOut>` |
| `Bind` | Monad | `Option<T> → (T → Option<TOut>) → Option<TOut>` |
| `Then` | Alias | Same as Bind |
| `Filter` | Guard | `Option<T> → (T → bool) → Option<T>` |
| `Tap` | Side effect | `Option<T> → (T → void) → Option<T>` |
| `TapNone` | Side effect | `Option<T> → (() → void) → Option<T>` |
| `OrDefault` | Unwrap | `Option<T> → T → T` |
| `OrElse` | Unwrap | `Option<T> → (() → T) → T` |
| `Or` | Alternative | `Option<T> → Option<T> → Option<T>` |
| `Or` | Alternative | `Option<T> → (() → Option<T>) → Option<T>` |
| `ToNullable` | Interop | `Option<T> → T?` (reference) |
| `ToNullableStruct` | Interop | `Option<T> → T?` (value type) |
| `Zip` | Combine | `Option<T1> → Option<T2> → Option<(T1,T2)>` |
| `Zip` | Combine | `Option<T1> → Option<T2> → (T1,T2 → TOut) → Option<TOut>` |
| `Contains` | Boolean | `Option<T> → (T → bool) → bool` |
| `Contains` | Boolean | `Option<T> → T → bool` |

### Async (AsyncExtensions.cs)

Two families:
1. **On `Option<T>`** with async lambdas: `MatchAsync`, `MapAsync`, `BindAsync`, `TapAsync`
2. **On `Task<Option<T>>`** for pipeline chaining: `MapAsync`, `BindAsync`, `TapAsync`, `WhereAsync`, `MatchAsync`, `OrDefaultAsync`, `OrElseAsync`

### Collections (CollectionExtensions.cs)

| Extension | Signature | Purpose |
|-----------|-----------|---------|
| `Values` | `IEnumerable<Option<T>> → IEnumerable<T>` | Extract all Some values |
| `FirstOrNone` | `IEnumerable<Option<T>> → Option<T>` | First Some from options |
| `FirstOrNone` | `IEnumerable<T> → (T → bool) → Option<T>` | First match as Option |
| `SingleOrNone` | `IEnumerable<T> → (T → bool) → Option<T>` | Single match or None if 0/2+ |
| `GetValueOrNone` | `IReadOnlyDictionary → Option<TValue>` | Dictionary lookup as Option |
| `Sequence` | `IEnumerable<Option<T>> → Option<IReadOnlyList<T>>` | All-or-nothing |
| `Traverse` | `IEnumerable<T> → (T → Option<TOut>) → Option<IReadOnlyList<TOut>>` | Map then sequence |

### LINQ (LinqExtensions.cs)

| Extension | Maps to |
|-----------|---------|
| `Select` | `Map` |
| `SelectMany` | `Bind` |
| `Where` | `Filter` |

### Result Integration (ResultIntegration.cs)

| Extension | Direction | Semantics |
|-----------|-----------|-----------|
| `ToResult(errorMessage)` | Option → Result | Some → Success, None → Failure |
| `ToResult(errorFactory)` | Option → Result | Lazy error |
| `ToResult<T,TError>(error)` | Option → Result<T,TError> | Typed error |
| `ToOption()` | Result → Option | Success → Some, Failure → None |
| `OrResult(factory)` | Option → Result | Some → Success, None → factory() |
| `BindResult(binder)` | Option → Option | Map through Result, discard error |

---

## 6. Algebraic Properties (verified by CsCheck)

- **Functor identity**: `option.Map(x => x) == option`
- **Functor composition**: `option.Map(f).Map(g) == option.Map(x => g(f(x)))`
- **Monad left identity**: `Option.Some(x).Bind(f) == f(x)`
- **Monad right identity**: `option.Bind(x => Option.Some(x)) == option`
- **Monad associativity**: `option.Bind(f).Bind(g) == option.Bind(x => f(x).Bind(g))`

PropertyTests.cs verifies these with random inputs via CsCheck.

---

## 7. Ecosystem Position

```
FrenchExDev.Net ecosystem:
  Options        ← optional values (this project)
  Result         ← error handling (Option depends on this)
  Builder        ← validated construction (uses Result)
  Clock          ← time abstraction
  Guard          ← argument validation
  ...

Relationship:
  Option<T>  = "value may be absent" (normal, expected)
  Result<T>  = "operation may fail" (error, needs handling)
  ToResult() = bridges absence to failure when required
  ToOption() = bridges failure to absence when errors don't matter
```
