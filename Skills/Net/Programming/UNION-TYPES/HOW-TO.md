# UNION-TYPES — How-To

## 1. Returning A Union

```csharp
public sealed record User(int Id, string Name);
public sealed record NotFound;
public sealed record Forbidden;

public OneOf<User, NotFound, Forbidden> GetUser(int id)
{
    if (!_currentUser.CanRead(id))
        return new Forbidden();   // implicit conversion

    var user = _repo.Find(id);
    if (user is null)
        return new NotFound();

    return user;
}
```

The implicit conversion from each type parameter to the union means callers don't have to write `OneOf<User, NotFound, Forbidden>.From(...)`.

## 2. Exhaustive Match (Returns Value)

```csharp
IActionResult response = GetUser(id).Match(
    withT1: user      => Ok(user),
    withT2: notFound  => NotFound(),
    withT3: forbidden => Forbid());
```

Forgetting any of `withT1` / `withT2` / `withT3` is a compile error — the method signature requires all three delegates.

## 3. Exhaustive Switch (Side Effects)

```csharp
GetUser(id).Switch(
    withT1: user      => _logger.LogInformation("Loaded user {Id}", user.Id),
    withT2: notFound  => _logger.LogWarning("User {Id} not found", id),
    withT3: forbidden => _logger.LogWarning("Access denied for user {Id}", id));
```

## 4. Type Predicates

```csharp
var result = GetUser(id);

if (result.IsT1)
    Console.WriteLine($"Got user {result.AsT1.Name}");
else if (result.IsT2)
    Console.WriteLine("Not found");
else if (result.IsT3)
    Console.WriteLine("Forbidden");
```

This works but is brittle — adding a new union case won't break the code, it'll just silently miss the new case. Prefer `Match`.

## 5. `TryGet<T>` For One-Branch Code

When only one outcome matters:

```csharp
if (result.TryGet<User>(out var user))
{
    return user.Name;
}
return "anonymous";
```

## 6. Combining With `Result<T>`

A union and a `Result<T>` are not the same thing. Use a union when you have **three or more distinct outcomes**. Use a `Result<T>` when you have a clear success/failure split.

You can compose them:

```csharp
public Result<OneOf<User, ContractUser>> FindAnyUserType(int id)
{
    // ... returns Success holding either User or ContractUser, or a Failure
}
```

## 7. Pattern Matching With C# Switch Expressions

If you want to use C# pattern matching directly on the inner value:

```csharp
var label = result.Match(
    withT1: user      => $"User: {user.Name}",
    withT2: notFound  => "Not found",
    withT3: forbidden => "Forbidden");

// Or destructure via TryGet
return result switch
{
    { IsT1: true } when result.TryGet<User>(out var u) => u.Name,
    { IsT2: true } => "not found",
    { IsT3: true } => "forbidden",
    _ => throw new InvalidOperationException()
};
```

The `Match` form is shorter and exhaustive. Prefer it.

## 8. Equality Tests

```csharp
var a = OneOf<int, string>.From(42);
var b = OneOf<int, string>.From(42);
Assert.True(a.Equals(b));   // same case, equal values

var c = OneOf<int, string>.From("42");
Assert.False(a.Equals(c));  // different cases
```

## 9. Testing With Assertions

```csharp
using FrenchExDev.Net.Union.Testing;

result.ShouldBeT1();       // throws if not T1
result.ShouldBeT1(expectedUser);
result.ShouldBeT2();
```

## 10. Choosing The Arity

- 2 cases → consider `Result<T, TError>` first.
- 3 cases → `OneOf<T1, T2, T3>` is the natural fit.
- 4+ cases → `OneOf<T1, T2, T3, T4>` is the upper bound for readability.
- 5+ cases → consider whether your domain is actually a state machine that should be modeled differently.

## What NOT To Do

- **Don't use `OneOf<object, object>`.** The `notnull` constraint forbids this — keep it that way.
- **Don't access `AsT1` / `AsT2` without checking `IsT1` / `IsT2`.** They throw on mismatch. Use `Match` or `TryGet`.
- **Don't model success/failure as `OneOf<T, Error>`.** Use `Result<T, Error>` — it has the right combinators (`Map`, `Bind`, `Recover`, ...).
- **Don't make a class hierarchy when the cases are closed.** Use `OneOf` for closed sets; use abstract classes for open sets.
- **Don't go beyond 4–5 cases.** It's a sign your domain is more complex than a flat discriminated union.

## Anchor Package

[`Net/FrenchExDev/Union/`](../../../Net/FrenchExDev/Union/) — implementation reference.
