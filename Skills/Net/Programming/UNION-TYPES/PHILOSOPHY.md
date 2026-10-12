# UNION-TYPES — Philosophy

A discriminated union (`OneOf<T1, T2, ...>`) holds **exactly one** of N possible values. It is the right tool when an operation has more than two distinct outcome types and the type system should enforce exhaustive handling.

## When `Result<T>` Isn't Enough

`Result<T>` has two outcomes: success or failure. That covers most cases. But sometimes an operation has **three or more** distinct outcomes that aren't naturally a success/failure split:

- A user lookup may return `Found(User)`, `NotFound`, or `Forbidden` — three semantically distinct outcomes, none of which is "the failure".
- A parser may return `Parsed(Ast)`, `SyntaxError(Diagnostic)`, or `Incomplete(Position)`.
- A workflow step may produce `Completed`, `Pending(WaitToken)`, or `Cancelled(Reason)`.

Modeling these as `Result<T>` with a typed error works for two outcomes but breaks down at three: you can't have "two errors" in a Result. You'd resort to a single `enum` with associated data — exactly what discriminated unions exist for.

## Type-Safe Either

`OneOf<T1, T2>` is the C# equivalent of F#'s discriminated union or Rust's `enum`:

```csharp
public sealed class OneOf<T1, T2> where T1 : notnull where T2 : notnull
{
    public bool IsT1 { get; }
    public bool IsT2 { get; }
    public T1 AsT1 { get; }   // throws if IsT2
    public T2 AsT2 { get; }   // throws if IsT1

    public TResult Match<TResult>(Func<T1, TResult> withT1, Func<T2, TResult> withT2);
    public void Switch(Action<T1> withT1, Action<T2> withT2);
}
```

The compiler enforces both type parameters — there is no `OneOf<object, object>` escape hatch. `Match` is exhaustive: you must provide a function for every case.

## Pattern Matching Without Overload Resolution Surprises

You could simulate a union with method overloads:

```csharp
void Handle(User user) { }
void Handle(Forbidden f) { }
void Handle(NotFound n) { }
```

But overload resolution depends on the **static** type of the argument, not its runtime type. If you have an `object` and call `Handle(obj)`, you get a compile error or the wrong overload. `OneOf<User, Forbidden, NotFound>` makes the discriminator explicit and runtime-safe.

## Implicit Conversions Because Construction Should Be Cheap

```csharp
public static implicit operator OneOf<T1, T2>(T1 value) => From(value);
public static implicit operator OneOf<T1, T2>(T2 value) => From(value);
```

This lets a method return a value directly:

```csharp
public OneOf<User, NotFound> GetUser(int id)
    => _repo.Find(id) is { } user ? user : new NotFound();   // implicit conversion
```

The `notnull` constraint on each type parameter prevents `OneOf<string?, int?>` and friends.

## `Match` Is Exhaustive, `Switch` Is Side-Effecting

- `Match<TResult>(...)` returns a value. It MUST cover every case (the compiler enforces this via overload count).
- `Switch(...)` returns `void`. It's for side effects — logging, mutation, dispatching to other systems.

Both are exhaustive in the sense that they require one delegate per type parameter. Forgetting a case is a compile error, not a runtime error.

## `TryGet<T>` For Open Pattern Matching

Sometimes you want to extract one specific case without an exhaustive match:

```csharp
if (result.TryGet<User>(out var user))
    return user.Name;
```

This is the escape hatch for code that only cares about one branch. Most code should use `Match` for type safety.

## Discriminated Unions Vs Inheritance Hierarchies

A class hierarchy (`abstract class Result; class Success : Result; class Failure : Result;`) and a discriminated union (`OneOf<Success, Failure>`) solve similar problems. Differences:

- **Hierarchies are open** — anyone can subclass `Result` and extend the union. `OneOf` is closed: the type parameters define the entire universe.
- **Hierarchies require visitor pattern** for exhaustive matching. `OneOf.Match` provides it natively.
- **Hierarchies allocate the discriminator on the heap** in the form of the class type. `OneOf` stores a single `byte _index`.
- **Hierarchies invite mutation** through subclass-specific properties. `OneOf` keeps each case as a separate type that you defined however you wanted.

Use a hierarchy when the set of outcomes is open and extension is the goal. Use `OneOf` when the set is closed and exhaustiveness is the goal.

## Anchor Package

[`Net/FrenchExDev/Union/`](../../../Net/FrenchExDev/Union/) — `OneOf<T1, T2>`, `OneOf<T1, T2, T3>`, `OneOf<T1, T2, T3, T4>` and assertion helpers.
