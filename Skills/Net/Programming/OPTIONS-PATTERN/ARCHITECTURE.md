# OPTIONS-PATTERN — Architecture

## Core Type

```csharp
public sealed record Option<T> where T : notnull
{
    private readonly T? _value;
    private readonly bool _isSome;

    private Option(T value) { _value = value; _isSome = true; }
    private Option() { _value = default; _isSome = false; }

    public bool IsSome => _isSome;
    public bool IsNone => !_isSome;
    public T Value => _isSome ? _value! : throw new InvalidOperationException("None");

    public static Option<T> Some(T value) =>
        value is null ? throw new ArgumentNullException(nameof(value)) : new Option<T>(value);
    public static Option<T> None() => new();

    public TResult Match<TResult>(Func<T, TResult> onSome, Func<TResult> onNone)
        => _isSome ? onSome(_value!) : onNone();

    public void Switch(Action<T> onSome, Action onNone)
    {
        if (_isSome) onSome(_value!); else onNone();
    }

    public static implicit operator Option<T>(T value) => Some(value);
    public override string ToString() => _isSome ? $"Some({_value})" : "None";
}
```

`T : notnull` prevents `Option<string?>`. Private constructors enforce factory usage. Implicit conversion from `T` allows `Option<string> opt = "hello"`.

## Static Helper Class

```csharp
public static class Option
{
    public static Option<T> Some<T>(T value) where T : notnull => Option<T>.Some(value);
    public static Option<T> None<T>() where T : notnull => Option<T>.None();
    public static Option<T> From<T>(T? value) where T : class
        => value is null ? None<T>() : Some(value);
    public static Option<T> FromNullable<T>(T? value) where T : struct
        => value.HasValue ? Some(value.Value) : None<T>();
    public static Option<T> FromTry<T>(Func<T> factory) where T : notnull
    {
        try { return Some(factory()); }
        catch { return None<T>(); }
    }
    public static Option<T> FromTry<T, TEx>(Func<T> factory)
        where T : notnull where TEx : Exception
    {
        try { return Some(factory()); }
        catch (TEx) { return None<T>(); }
    }
}
```

## Extension Categories

Five extension classes, each in its own file:

| File | Methods |
|---|---|
| `Extensions.cs` | `Map`, `Bind`/`Then`, `Filter`, `Tap`, `TapNone`, `OrDefault`, `OrElse`, `Or`, `ToNullable`, `ToNullableStruct`, `Zip`, `Contains` |
| `AsyncExtensions.cs` | `MatchAsync`, `MapAsync`, `BindAsync`, `TapAsync`, `WhereAsync`, `OrDefaultAsync`, `OrElseAsync` (on both `Option<T>` and `Task<Option<T>>`) |
| `CollectionExtensions.cs` | `Values`, `FirstOrNone`, `SingleOrNone`, `GetValueOrNone`, `Sequence`, `Traverse` |
| `LinqExtensions.cs` | `Select` (Map), `SelectMany` (Bind), `Where` (Filter) — enables LINQ query syntax |
| `ResultIntegration.cs` | `ToResult`, `ToOption`, `OrResult`, `BindResult` |

## Functor/Monad Surface

| Extension | Category | Signature |
|---|---|---|
| `Map` | Functor | `Option<T> → (T → TOut) → Option<TOut>` |
| `Bind` / `Then` | Monad | `Option<T> → (T → Option<TOut>) → Option<TOut>` |
| `Filter` | Guard | `Option<T> → (T → bool) → Option<T>` |
| `Tap` / `TapNone` | Side effect | `Option<T> → (T/() → void) → Option<T>` |
| `OrDefault(fallback)` | Unwrap | `Option<T> → T → T` |
| `OrElse(factory)` | Unwrap | `Option<T> → (() → T) → T` |
| `Or(other)` | Alternative | `Option<T> → Option<T> → Option<T>` |
| `Zip` | Combine | `Option<T1> → Option<T2> → Option<(T1,T2)>` |
| `Contains(value)` | Boolean | `Option<T> → T → bool` |

## Async — Two Families

1. **On `Option<T>`** with async lambdas: `MapAsync`, `BindAsync`, `MatchAsync`, `TapAsync`.
2. **On `Task<Option<T>>`** for pipeline chaining: same names, plus `WhereAsync`, `OrDefaultAsync`, `OrElseAsync`.

The pipeline form on `Task<Option<T>>` lets callers chain from an async source without intermediate `await`s.

## Collection Operations

| Extension | Signature | Purpose |
|---|---|---|
| `Values` | `IEnumerable<Option<T>> → IEnumerable<T>` | Extract all Some values |
| `FirstOrNone` | `IEnumerable<T> → (T → bool) → Option<T>` | First match as Option |
| `SingleOrNone` | `IEnumerable<T> → (T → bool) → Option<T>` | Single match or None if 0/2+ |
| `GetValueOrNone` | `IReadOnlyDictionary → Option<TValue>` | Dictionary lookup as Option |
| `Sequence` | `IEnumerable<Option<T>> → Option<IReadOnlyList<T>>` | All-or-nothing |
| `Traverse` | `IEnumerable<T> → (T → Option<TOut>) → Option<IReadOnlyList<TOut>>` | Map then sequence |

## LINQ Integration

Three one-liner extensions enable C# query syntax:

```csharp
public static Option<TOut> Select<T, TOut>(this Option<T> opt, Func<T, TOut> selector)
    where T : notnull where TOut : notnull => opt.Map(selector);

public static Option<TOut> SelectMany<T, TOut>(this Option<T> opt, Func<T, Option<TOut>> binder)
    where T : notnull where TOut : notnull => opt.Bind(binder);

public static Option<T> Where<T>(this Option<T> opt, Func<T, bool> predicate)
    where T : notnull => opt.Filter(predicate);
```

## Result Integration

```csharp
// Option → Result
opt.ToResult("not found");                       // string error
opt.ToResult(() => new ValidationResult(...));   // lazy factory
opt.ToResult<T, TError>(error);                  // typed error

// Result → Option
result.ToOption();   // discards error
```

## Algebraic Properties (Verify With Property Tests)

- **Functor identity**: `option.Map(x => x) == option`
- **Functor composition**: `option.Map(f).Map(g) == option.Map(x => g(f(x)))`
- **Monad left identity**: `Option.Some(x).Bind(f) == f(x)`
- **Monad right identity**: `option.Bind(x => Option.Some(x)) == option`
- **Monad associativity**: `option.Bind(f).Bind(g) == option.Bind(x => f(x).Bind(g))`

## Anchor Package

[`Net/FrenchExDev/Options/`](../../../Net/FrenchExDev/Options/) — full implementation with 126 tests, 13 of them CsCheck property tests.
