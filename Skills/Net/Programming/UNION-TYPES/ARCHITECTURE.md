# UNION-TYPES — Architecture

## Type Family

```
OneOf<T1, T2>
OneOf<T1, T2, T3>
OneOf<T1, T2, T3, T4>
... (up to a sensible arity, typically 4–9)
```

Each is a separate `sealed class` with type parameters constrained `notnull`. They are NOT a class hierarchy — `OneOf<T1, T2>` does not inherit from `OneOf<T1, T2, T3>`. Each arity is its own closed type.

## Internal Representation

```csharp
public sealed class OneOf<T1, T2> : IEquatable<OneOf<T1, T2>>
    where T1 : notnull
    where T2 : notnull
{
    private readonly object _value;
    private readonly byte _index;

    private OneOf(object value, byte index)
    {
        _value = value;
        _index = index;
    }
}
```

- `_value` is `object` — uniform storage for any of the type parameters.
- `_index` is a `byte` (0..255 — no realistic union exceeds 9 cases, so a byte is plenty).
- The constructor is **private**. Construction goes through static factories.

## Static Factories

```csharp
public static OneOf<T1, T2> From(T1 value)
{
    if (value is null) throw new ArgumentNullException(nameof(value));
    return new OneOf<T1, T2>(value, 0);
}

public static OneOf<T1, T2> From(T2 value)
{
    if (value is null) throw new ArgumentNullException(nameof(value));
    return new OneOf<T1, T2>(value, 1);
}
```

The factories null-check at construction so a `OneOf` never holds `null`.

## Implicit Conversions

```csharp
public static implicit operator OneOf<T1, T2>(T1 value) => From(value);
public static implicit operator OneOf<T1, T2>(T2 value) => From(value);
```

Implicit conversions exist for unions because construction is otherwise verbose. The `notnull` constraint on each type parameter prevents `OneOf<int?, string?>`.

## Predicates And Accessors

```csharp
public bool IsT1 => _index == 0;
public bool IsT2 => _index == 1;

public T1 AsT1 => _index == 0
    ? (T1)_value
    : throw new InvalidOperationException($"Cannot access T1 when union holds T{_index + 1}");

public T2 AsT2 => _index == 1
    ? (T2)_value
    : throw new InvalidOperationException(...);
```

`AsT1` / `AsT2` throw on type mismatch. They are escape hatches — most code should use `Match`.

## `Match` And `Switch`

```csharp
public TResult Match<TResult>(Func<T1, TResult> withT1, Func<T2, TResult> withT2)
{
    if (withT1 is null) throw new ArgumentNullException(nameof(withT1));
    if (withT2 is null) throw new ArgumentNullException(nameof(withT2));

    return _index switch
    {
        0 => withT1((T1)_value),
        1 => withT2((T2)_value),
        _ => throw new InvalidOperationException("Invalid union state.")
    };
}

public void Switch(Action<T1> withT1, Action<T2> withT2)
{
    if (withT1 is null) throw new ArgumentNullException(nameof(withT1));
    if (withT2 is null) throw new ArgumentNullException(nameof(withT2));

    switch (_index)
    {
        case 0: withT1((T1)_value); break;
        case 1: withT2((T2)_value); break;
        default: throw new InvalidOperationException("Invalid union state.");
    }
}
```

`Match` returns a value. `Switch` returns `void`. Both require **one delegate per type parameter** — the compiler enforces this via the method signature, so forgetting a case is a compile error.

## `TryGet<T>` Escape Hatch

```csharp
public bool TryGet<T>(out T value) where T : notnull
{
    if (_value is T typed) { value = typed; return true; }
    value = default!;
    return false;
}
```

Useful when only one branch matters. Most code should prefer `Match`.

## Equality

`OneOf` implements `IEquatable<OneOf<T1, T2>>`:

```csharp
public bool Equals(OneOf<T1, T2>? other)
    => other is not null && _index == other._index && _value.Equals(other._value);

public override int GetHashCode() => HashCode.Combine(_index, _value);
```

Two unions are equal iff they hold the same case AND the underlying values are equal.

## ToString

```csharp
public override string ToString() => $"T{_index + 1}({_value})";
```

Produces `T1(Alice)` / `T2(Forbidden)` etc. — useful in test failures.

## Project Layout

```
Union              netstandard2.0 + net10.0 — zero deps
  OneOf2.cs        OneOf<T1, T2>
  OneOf3.cs        OneOf<T1, T2, T3>
  OneOf4.cs        OneOf<T1, T2, T3, T4>

Union.Testing      netstandard2.0 + net10.0 — refs Union
  UnionAssertions.cs    ShouldBeT1, ShouldBeT2, ...

Union.Tests        net10.0 — xUnit
```

## Multi-Targeting Note

`HashCode.Combine` is .NET Standard 2.1+. For `netstandard2.0`, fall back to manual hashing:

```csharp
#if NETSTANDARD2_0
unchecked { return (_index * 397) ^ _value.GetHashCode(); }
#else
return HashCode.Combine(_index, _value);
#endif
```

## Anchor Package

[`Net/FrenchExDev/Union/`](../../../Net/FrenchExDev/Union/) — implementation reference.
