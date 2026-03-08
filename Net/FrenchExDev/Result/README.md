# FrenchExDev.Net.Result

A lightweight, immutable Result type library for .NET that replaces exceptions and null with explicit, composable success/failure values.

**Documentation:** [Architecture](doc/ARCHITECTURE.md) · [Philosophy](doc/PHILOSOPHY.md) · [How-To](doc/HOW-TO.md) · [Comparison Table](doc/COMPARISON-TABLE.md)

## Overview

The library provides three complementary types, all implementing `IResult`:

| Type | Use case |
|---|---|
| `Result` | Operation that succeeds or fails with no value |
| `Result<T>` | Operation that returns a value or fails with `ValidationResult` errors |
| `Result<T, TError>` | Operation that returns a value or fails with a typed error object |

All three types are **sealed records** — immutable, thread-safe, and structurally equatable.

---

## Installation

```xml
<PackageReference Include="FrenchExDev.Net.Result" />
```

---

## Quick Start

```csharp
using FrenchExDev.Net.Result;

// No-value result
Result result = Result.Success();
Result fail   = Result.Failure();

// Value result with validation errors
Result<User> ok  = Result<User>.Success(new User("Alice"));
Result<User> bad = Result<User>.Failure(new ValidationResult("Name is required", ["Name"]));

// Typed-error result
Result<User, NotFoundException> found    = Result<User, NotFoundException>.Success(user);
Result<User, NotFoundException> notFound = Result<User, NotFoundException>.Failure(new NotFoundException());
```

---

## Type Reference

### `IResult`

```csharp
public interface IResult
{
    bool IsSuccess { get; }
    bool IsFailure { get; }
}
```

---

### `Result`

Represents an operation with no return value.

#### Creating

```csharp
var ok  = Result.Success();
var err = Result.Failure();
```

#### Consuming — `Match` / `MatchAsync`

```csharp
string label = result.Match(
    onSuccess: () => "Done",
    onFailure: () => "Failed");

string label = await result.MatchAsync(
    onSuccess: () => Task.FromResult("Done"),
    onFailure: () => Task.FromResult("Failed"));
```

#### Combining — `Result.Combine`

Combines 2–7 `Result<T>` results. Returns success with a value-tuple when **all** succeed; returns failure merging **all** validation errors otherwise.

```csharp
Result<(User, Order)> combined = Result.Combine(userResult, orderResult);

// Use Then to continue the chain
Result<Invoice> invoice = Result.Combine(userResult, orderResult)
    .Then(t => CreateInvoice(t.Item1, t.Item2));
```

`Combine` overloads are available for 2 through 7 results.

#### Wrapping exceptions — `FromTry` / `FromTryAsync`

Catches only `TError` exceptions; all other exceptions propagate normally.

```csharp
Result<Data, IOException> r = Result.FromTry<Data, IOException>(
    () => File.ReadAllBytes(path));

Result<Data, IOException> r = await Result.FromTryAsync<Data, IOException>(
    () => File.ReadAllBytesAsync(path));
```

---

### `Result<T>`

Represents an operation that either returns a value of type `T` or fails with one or more `ValidationResult` errors.

#### Creating

```csharp
var ok  = Result<User>.Success(user);
var err = Result<User>.Failure(new ValidationResult("Name is required", ["Name"]));
```

#### Properties

| Member | Description |
|---|---|
| `IsSuccess` | `true` when the result is a success |
| `IsFailure` | `!IsSuccess` |
| `Value` | The success value (may be `null` on failure — prefer `ValueOrThrow()`) |
| `ValidationResults` | All validation errors. Empty on success; one or more on failure |
| `ValidationResult` | First validation error, or `null` on success. Convenience for single-error results |

#### Consuming — `Match` / `MatchAsync`

```csharp
IActionResult response = result.Match(
    onSuccess: user  => Ok(user),
    onFailure: errs  => BadRequest(errs));

IActionResult response = await result.MatchAsync(
    onSuccess: user  => Task.FromResult(Ok(user)),
    onFailure: errs  => Task.FromResult((IActionResult)BadRequest(errs)));
```

#### Extracting the value

```csharp
// Throws InvalidOperationException on failure
User user = result.ValueOrThrow();

// Returns fallback on failure
User user = result.ValueOrDefault(User.Anonymous);

// Lazily computes fallback from errors
User user = result.ValueOrElse(errs => BuildFallback(errs));
```

---

### `Result<T, TError>`

Represents an operation that either returns a value of type `T` or fails with a strongly-typed error of type `TError`.

#### Creating

```csharp
var ok  = Result<User, DomainError>.Success(user);
var err = Result<User, DomainError>.Failure(DomainError.NotFound);
```

#### Properties

| Member | Description |
|---|---|
| `IsSuccess` | `true` when the result is a success |
| `IsFailure` | `!IsSuccess` |
| `Value` | The success value |
| `Error` | The failure error object |

#### Consuming — `Match` / `MatchAsync`

```csharp
string message = result.Match(
    onSuccess: user  => $"Hello {user.Name}",
    onFailure: error => $"Error: {error}");
```

#### Extracting the value

```csharp
// Throws InvalidOperationException on failure (includes Error.ToString() in the message)
User user = result.ValueOrThrow();

// Returns fallback on failure
User user = result.ValueOrDefault(User.Anonymous);

// Lazily computes fallback from the error
User user = result.ValueOrElse(err => BuildFallback(err));
```

---

## Extension Methods

All extension methods live in `ResultExtensions` and are available for both `Result<T>` and `Result<T, TError>`. Async pipeline overloads extend `Task<Result<T>>` and `Task<Result<T, TError>>` directly so you can chain without intermediate `await`s.

### `Map` — transform the success value

```csharp
Result<string> name = userResult.Map(u => u.Name);

// Async mapper
Result<string> name = await userResult.MapAsync(async u => await u.GetNameAsync());

// Async pipeline (on Task<Result<T>>)
Result<string> name = await GetUserAsync().MapAsync(u => u.Name);
```

Propagates failure unchanged. `TError` variant preserves the error type.

### `Bind` / `Then` — chain Result-returning functions

`Then` is an alias for `Bind`; prefer `Then` for readability in sequential pipelines.

```csharp
Result<Order> order = userResult.Bind(u => GetOrder(u.Id));

// Preferred alias
Result<Order> order = userResult.Then(u => GetOrder(u.Id));

// Async binder
Result<Order> order = await userResult.BindAsync(async u => await GetOrderAsync(u.Id));

// Async pipeline
Result<Order> order = await GetUserAsync()
    .ThenAsync(u => GetOrderAsync(u.Id));
```

### `Recover` — provide a fallback on failure

```csharp
// Result<T> — recovery function receives all validation errors
Result<User> recovered = result.Recover(errs => User.Anonymous);

// Result<T, TError> — recovery function receives the typed error
Result<User, DomainError> recovered = result.Recover(err => User.Anonymous);

// Async
Result<User> recovered = await result.RecoverAsync(async errs => await GetDefaultUserAsync());
```

### `Tap` — execute a side-effect on success without transforming

```csharp
Result<User> same = result.Tap(u => logger.LogInformation("Got user {Id}", u.Id));

// Async
Result<User> same = await result.TapAsync(async u => await auditLog.RecordAsync(u));

// Async pipeline
await GetUserAsync()
    .TapAsync(u => auditLog.RecordAsync(u))
    .MapAsync(u => u.Name);
```

### `TapError` — execute a side-effect on failure without transforming

```csharp
Result<User> same = result.TapError(errs => logger.LogWarning("Validation failed"));

// Async
Result<User> same = await result.TapErrorAsync(async errs => await notifier.AlertAsync(errs));
```

### `Ensure` — add a guard check to a success value (`Result<T>` only)

```csharp
// Static error
Result<User> verified = result.Ensure(
    u => u.Age >= 18,
    new ValidationResult("Must be 18 or older", ["Age"]));

// Computed error (receives the value)
Result<User> verified = result.Ensure(
    u => u.Age >= 18,
    u => new ValidationResult($"Age {u.Age} is below minimum", ["Age"]));
```

Returns the result unchanged if it was already a failure, or if the predicate passes.

---

## Async Pipeline

All extension methods have counterparts on `Task<Result<T>>` and `Task<Result<T, TError>>`, enabling `await`-free chains:

```csharp
string greeting = await GetUserAsync()                        // Task<Result<User>>
    .ThenAsync(u => ValidateAsync(u))                         // Task<Result<User>>
    .TapAsync(u => auditLog.RecordAsync(u))                   // Task<Result<User>>
    .MapAsync(u => u.Name)                                    // Task<Result<string>>
    .MatchAsync(
        onSuccess: name => $"Hello, {name}!",
        onFailure: errs => $"Error: {errs[0].ErrorMessage}"); // Task<string>
```

---

## Combining Multiple Results

Use `Result.Combine` when you need to validate several independent operations and collect all errors:

```csharp
Result<string> nameResult  = ValidateName(input.Name);
Result<int>    ageResult   = ValidateAge(input.Age);
Result<Email>  emailResult = ValidateEmail(input.Email);

Result<(string, int, Email)> all = Result.Combine(nameResult, ageResult, emailResult);

// On success — destructure the tuple
Result<User> user = all.Then(t =>
{
    var (name, age, email) = t;
    return Result<User>.Success(new User(name, age, email));
});

// On failure — all validation errors are merged
user.Match(
    onSuccess: u    => Console.WriteLine($"Created {u.Name}"),
    onFailure: errs => errs.ToList().ForEach(e => Console.WriteLine(e.ErrorMessage)));
```

`Combine` overloads: 2, 3, 4, 5, 6, 7 results.

---

## Choosing the Right Type

| Situation | Type |
|---|---|
| Simple pass/fail with no payload | `Result` |
| Validation — single or multiple field errors | `Result<T>` |
| Domain operation that fails with a known error type | `Result<T, TError>` |
| Wrapping a method that throws a specific exception | `Result.FromTry<T, TException>` |
| Combining independent validations | `Result.Combine` → `Result<(T1, T2, ...)>` |

---

## Immutability & Thread Safety

All three types are `sealed record`s with `init`-only setters and a private constructor. Once created, a result instance cannot be modified. `Result<T>.Failure(ValidationResult)` takes a **defensive copy** of `MemberNames`, guaranteeing immutability even if the caller reuses the `ValidationResult` object after creation.

---

## Packages

| Package | Description |
|---|---|
| `FrenchExDev.Net.Result` | Core types and extensions |
| `FrenchExDev.Net.Result.Testing` | Test helper stubs (e.g., `MyClass`, `MyException`) |
