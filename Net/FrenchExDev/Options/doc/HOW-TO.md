# Options -- Developer Guide (HOW-TO)

## Table of Contents

1. [Creating Options](#1-creating-options)
2. [Pattern Matching](#2-pattern-matching)
3. [Transforming (Map)](#3-transforming-map)
4. [Chaining (Bind)](#4-chaining-bind)
5. [Filtering](#5-filtering)
6. [Side Effects (Tap)](#6-side-effects-tap)
7. [Unwrapping](#7-unwrapping)
8. [Alternatives (Or)](#8-alternatives-or)
9. [Combining (Zip)](#9-combining-zip)
10. [LINQ Query Syntax](#10-linq-query-syntax)
11. [Async Pipelines](#11-async-pipelines)
12. [Collection Operations](#12-collection-operations)
13. [Result Integration](#13-result-integration)
14. [Nullable Interop](#14-nullable-interop)
15. [Testing with OptionAssertions](#15-testing-with-optionassertions)
16. [Running Tests](#16-running-tests)

---

## 1. Creating Options

```csharp
// From a known value
var some = Option.Some(42);
var none = Option.None<int>();

// Implicit conversion
Option<string> opt = "hello";  // → Some("hello")

// From nullable reference
Option<string> opt = Option.From<string>(maybeNull);

// From nullable value type
Option<int> opt = Option.FromNullable<int>(nullableInt);

// From a factory that may throw
Option<int> opt = Option.FromTry(() => int.Parse(input));

// Catch only specific exceptions
Option<int> opt = Option.FromTry<int, FormatException>(() => int.Parse(input));
// OverflowException still throws
```

---

## 2. Pattern Matching

```csharp
// Exhaustive match — both branches required
string label = option.Match(
    onSome: v => $"Found: {v}",
    onNone: () => "Not found");

// Side-effect match
option.Switch(
    onSome: v => Console.WriteLine($"Got {v}"),
    onNone: () => Console.WriteLine("Empty"));
```

---

## 3. Transforming (Map)

```csharp
Option<int> length = Option.Some("hello").Map(s => s.Length);
// → Some(5)

Option<int> nothing = Option.None<string>().Map(s => s.Length);
// → None (mapper never called)
```

---

## 4. Chaining (Bind)

```csharp
Option<User> user = Option.From(userId)
    .Bind(id => FindUser(id));       // FindUser returns Option<User>

// Then is an alias for Bind
Option<Email> email = user.Then(u => u.PrimaryEmail);
```

---

## 5. Filtering

```csharp
Option<User> active = Option.From(user)
    .Filter(u => u.IsActive);
// → None if user is null or inactive
```

---

## 6. Side Effects (Tap)

```csharp
Option.From(user)
    .Tap(u => logger.LogInfo($"Found user {u.Name}"))
    .TapNone(() => logger.LogWarning("User not found"))
    .Map(u => u.Email);
```

`Tap` and `TapNone` return the option unchanged -- they're for logging, metrics, or diagnostics.

---

## 7. Unwrapping

```csharp
// With default value
string email = option.OrDefault("no-reply@example.com");

// With lazy factory
string email = option.OrElse(() => GenerateDefault());

// Direct access (throws if None)
string email = option.Value;  // InvalidOperationException if None
```

---

## 8. Alternatives (Or)

```csharp
// Try primary, fall back to secondary
Option<Config> config = LoadFromFile()
    .Or(LoadFromEnvironment())
    .Or(() => Option.Some(Config.Default));
```

---

## 9. Combining (Zip)

```csharp
// Tuple zip — Some only if both are Some
Option<(string, int)> pair = name.Zip(age);

// Selector zip
Option<string> greeting = name.Zip(age, (n, a) => $"{n} is {a} years old");
```

---

## 10. LINQ Query Syntax

```csharp
var result = from x in optionA
             from y in optionB
             where x + y > 10
             select x + y;
// → Some if both are Some and sum > 10, None otherwise

// Equivalent to:
optionA.Bind(x => optionB.Filter(y => x + y > 10).Map(y => x + y));
```

---

## 11. Async Pipelines

### On Option<T> with async lambdas

```csharp
var email = await Option.From(userId)
    .BindAsync(id => FindUserAsync(id))    // async bind
    .MapAsync(u => u.Email)                 // sync map on Task<Option<T>>
    .OrDefaultAsync("unknown");             // unwrap
```

### Chaining on Task<Option<T>>

All async extensions work on `Task<Option<T>>`, so you never need intermediate `await`:

```csharp
var result = await GetUserAsync(id)          // returns Task<Option<User>>
    .MapAsync(u => u.Name)                   // sync mapper
    .MapAsync(async name => await Enrich(name)) // async mapper
    .WhereAsync(name => name.Length > 0)     // filter
    .TapAsync(name => Log(name))             // side effect
    .MatchAsync(
        onSome: name => $"Hello, {name}",
        onNone: () => "Hello, stranger");
```

---

## 12. Collection Operations

### Extract values

```csharp
IEnumerable<int> values = options.Values();
// [Some(1), None, Some(3)] → [1, 3]
```

### Find in collections

```csharp
Option<User> user = users.FirstOrNone(u => u.Name == "Alice");
Option<User> single = users.SingleOrNone(u => u.Id == 42); // None if 0 or 2+
Option<string> value = dict.GetValueOrNone("key");
```

### All-or-nothing (Sequence / Traverse)

```csharp
// Sequence: all must be Some
Option<IReadOnlyList<int>> all = new[] { Option.Some(1), Option.Some(2) }.Sequence();
// → Some([1, 2])

Option<IReadOnlyList<int>> fail = new[] { Option.Some(1), Option.None<int>() }.Sequence();
// → None

// Traverse: map then sequence in one step
Option<IReadOnlyList<User>> users = ids.Traverse(id => FindUser(id));
// → Some([...]) only if ALL lookups succeed
```

---

## 13. Result Integration

### Option to Result

```csharp
// String error
Result<User> result = Option.From(user).ToResult("User not found");

// Typed error
Result<User, AppError> result = Option.From(user)
    .ToResult(new AppError.NotFound("User"));

// Factory
Result<User> result = option.ToResult(() => new ValidationResult("Missing", ["UserId"]));
```

### Result to Option

```csharp
Option<User> user = result.ToOption();
// Success → Some, Failure → None (error discarded)
```

### Combined

```csharp
// Bridge through Result-returning function
Option<Email> email = Option.From(user)
    .BindResult(u => ValidateEmail(u.Email)); // Result<Email> → Option<Email>
```

---

## 14. Nullable Interop

```csharp
// Option to nullable (for API boundaries)
string? email = option.ToNullable();        // reference type
int? count = intOption.ToNullableStruct();   // value type

// Nullable to Option
Option<string> opt = Option.From<string>(nullableString);
Option<int> opt = Option.FromNullable<int>(nullableInt);
```

---

## 15. Testing with OptionAssertions

```csharp
using FrenchExDev.Net.Options.Testing;

// Assert Some
option.ShouldBeSome();              // throws if None
option.ShouldBeSome(42);            // asserts value equality
option.ShouldBeSomeAnd(v => v > 0); // asserts predicate

// Assert None
option.ShouldBeNone();              // throws if Some
```

---

## 16. Running Tests

```bash
dotnet test Options/FrenchExDev.Net.Options.slnx

# With quality gate
dotnet quality-gate test --config Options/quality-gate.yml
```

Test suite: 126 xUnit tests including 13 CsCheck property-based tests verifying functor/monad laws.
