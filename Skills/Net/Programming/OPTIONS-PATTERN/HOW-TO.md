# OPTIONS-PATTERN — How-To

## 1. Creating Options

```csharp
var some  = Option.Some(42);
var none  = Option.None<int>();

Option<string> opt = "hello";                              // implicit
Option<string> fromRef = Option.From<string>(maybeNull);
Option<int> fromVal = Option.FromNullable<int>(nullableInt);
Option<int> safe = Option.FromTry(() => int.Parse(input));
Option<int> safer = Option.FromTry<int, FormatException>(() => int.Parse(input));
```

## 2. Pattern Matching

```csharp
string label = option.Match(
    onSome: v => $"Found: {v}",
    onNone: () => "Not found");

option.Switch(
    onSome: v => Console.WriteLine($"Got {v}"),
    onNone: () => Console.WriteLine("Empty"));
```

## 3. Transforming And Chaining

```csharp
Option<int> length = Option.Some("hello").Map(s => s.Length);
Option<User> user  = Option.From(userId).Bind(id => FindUser(id));
Option<User> active = Option.From(user).Filter(u => u.IsActive);
```

## 4. Side Effects And Unwrapping

```csharp
Option.From(user)
    .Tap(u => logger.LogInfo($"Found user {u.Name}"))
    .TapNone(() => logger.LogWarning("User not found"))
    .Map(u => u.Email);

string email = option.OrDefault("no-reply@example.com");
string email2 = option.OrElse(() => GenerateDefault());
```

## 5. Alternatives And Combination

```csharp
Option<Config> config = LoadFromFile()
    .Or(LoadFromEnvironment())
    .Or(() => Option.Some(Config.Default));

Option<(string, int)> pair = name.Zip(age);
Option<string> greeting = name.Zip(age, (n, a) => $"{n} is {a}");
```

## 6. LINQ Query Syntax

```csharp
var result = from x in optionA
             from y in optionB
             where x + y > 10
             select x + y;
```

Equivalent to `optionA.Bind(x => optionB.Filter(y => x + y > 10).Map(y => x + y))`.

## 7. Async Pipelines

```csharp
var email = await Option.From(userId)
    .BindAsync(id => FindUserAsync(id))
    .MapAsync(u => u.Email)
    .OrDefaultAsync("unknown");
```

All async extensions also work on `Task<Option<T>>` so no intermediate `await` is required:

```csharp
var result = await GetUserAsync(id)
    .MapAsync(u => u.Name)
    .WhereAsync(name => name.Length > 0)
    .TapAsync(name => Log(name))
    .MatchAsync(
        onSome: name => $"Hello, {name}",
        onNone: () => "Hello, stranger");
```

## 8. Collections — Extract And Find

```csharp
IEnumerable<int> values = options.Values();                 // [Some(1), None, Some(3)] → [1, 3]
Option<User> user = users.FirstOrNone(u => u.Name == "Alice");
Option<User> single = users.SingleOrNone(u => u.Id == 42);  // None if 0 or 2+
Option<string> value = dict.GetValueOrNone("key");
```

## 9. Sequence And Traverse — All-Or-Nothing

```csharp
Option<IReadOnlyList<int>> all = new[] { Option.Some(1), Option.Some(2) }.Sequence();
// → Some([1, 2])

Option<IReadOnlyList<int>> fail = new[] { Option.Some(1), Option.None<int>() }.Sequence();
// → None

Option<IReadOnlyList<User>> users = ids.Traverse(id => FindUser(id));
// → Some([...]) only if EVERY lookup succeeds
```

## 10. Result Integration

```csharp
// Option → Result
Result<User> result = Option.From(user).ToResult("User not found");
Result<User, AppError> typed = Option.From(user)
    .ToResult(new AppError.NotFound("User"));

// Result → Option
Option<User> user = result.ToOption();   // success → Some, failure → None (error discarded)
```

## 11. Nullable Interop At API Boundaries

```csharp
// Option to nullable
string? email = option.ToNullable();
int? count = intOption.ToNullableStruct();

// Nullable to Option
Option<string> opt = Option.From<string>(nullableString);
Option<int> opt2 = Option.FromNullable<int>(nullableInt);
```

## 12. Testing With Assertions

```csharp
using FrenchExDev.Net.Options.Testing;

option.ShouldBeSome();
option.ShouldBeSome(42);
option.ShouldBeSomeAnd(v => v > 0);
option.ShouldBeNone();
```

## What NOT To Do

- **Don't use `Option<T>` for failure modeling.** That's `Result<T>`'s job. Option means "value may be absent (and that's fine)".
- **Don't access `Option.Value` without checking `IsSome` first.** It throws on None. Use `Match`, `OrDefault`, or `OrElse`.
- **Don't create `Option<string?>`.** The `notnull` constraint forbids this — keep it that way.
- **Don't write `Option<T>` constructors.** Use `Some` / `None` factories only.

## Anchor Package

[`Net/FrenchExDev/Options/`](../../../Net/FrenchExDev/Options/) — implementation reference.
