# Architecture — FrenchExDev.Net.Result

## Goals

1. **Explicit failure** — operations declare their failure mode in the return type, not through exceptions or nullable returns.
2. **Immutability** — a result value, once created, can never change state.
3. **Composability** — results chain with `Map`, `Bind`, `Then`, `Recover`, and `Combine` without nested `if`-checks.
4. **Async-first** — every combinator has a `Task<Result<T>>` overload so async code pipelines without breaking the chain.
5. **No dependency on third-party packages** — only `System.ComponentModel.DataAnnotations` for `ValidationResult`.

---

## Type Hierarchy

```
IResult
├── Result                        (no value — pass/fail only)
├── Result<TResult>               (value + validation errors)
└── Result<TResult, TError>       (value + typed error)
```

All three are `sealed record`s with a `private` constructor. The only way to obtain an instance is through the static factory methods `Success(...)` and `Failure(...)`.

### Why three types?

| Type | When to use |
|---|---|
| `Result` | Void operations (save, delete, send) — caller only needs to know if it worked |
| `Result<T>` | Validation and domain rules — multiple named field errors collected together, compatible with DataAnnotations |
| `Result<T, TError>` | Domain or infrastructure operations with a strongly-typed error discriminant (enum, exception subtype, union-like type) |

The three types are **not** a class hierarchy of each other. `Result` is not a special case of `Result<T>`; they have different error representations and different extension-method semantics. Keeping them separate avoids the `Unit` type anti-pattern and the "how do I carry errors on the no-value result" design smell.

---

## `Result` — Internal Design

```csharp
public sealed record Result : IResult
{
    public bool IsSuccess { get; init; }
    public bool IsFailure => !IsSuccess;
    private Result() { }

    public static Result Success() => new() { IsSuccess = true };
    public static Result Failure() => new() { IsSuccess = false };
}
```

### Key decisions

- **`init`-only setter** — structural record equality works correctly; the property is set once at construction.
- **Private constructor** — guarantees that no `Result` instance exists in an indeterminate state. Only `Success()` and `Failure()` produce valid values.
- **No caching of singleton instances** — avoiding premature optimization; the records are tiny value-like objects allocated on the heap but quickly collected.

### `Combine` — multi-result AND gate

`Combine<T1, T2, ...>` is a static method on `Result` (not an extension) because it operates across multiple heterogeneous `Result<T>` types and produces a `Result<(T1, T2, ...)>` tuple.

```
Result<T1> × Result<T2> → Result<(T1, T2)>
```

Semantics:
- **All succeed** → `Result<(T1, T2)>.Success((r1.Value!, r2.Value!))`
- **Any fail** → `Result<(T1, T2)>.Failure(Merge(r1.ValidationResults, r2.ValidationResults))`

The private `Merge` helper concatenates all `IReadOnlyList<ValidationResult>` inputs into a single list, so the consumer gets every error in one pass — a key property for form-level validation.

Overloads for 2–7 results are all present. Beyond 7, callers should decompose their domain into sub-combines.

### `FromTry` / `FromTryAsync`

```
factory : () → TResult   →   Result<TResult, TError>
```

Only catches `TError : Exception`. All other exception types propagate up unchanged. This is intentional: `FromTry` is not a blanket exception suppressor — it is a controlled boundary between exception-throwing APIs and the Result world.

---

## `Result<TResult>` — Internal Design

```csharp
public sealed record Result<TResult> : IResult where TResult : notnull
{
    public bool IsSuccess { get; init; }
    public TResult? Value { get; init; } = default!;
    public IReadOnlyList<ValidationResult> ValidationResults { get; init; } = [];
    public ValidationResult? ValidationResult => ValidationResults.Count > 0 ? ValidationResults[0] : null;
    private Result() { }
}
```

### `notnull` constraint

`TResult : notnull` prevents callers from writing `Result<string?>` or `Result<int?>`. A result carries a value **or** errors — never both, never neither. The nullable annotation on `Value` (`TResult?`) is intentional: on a failure result `Value` is `default!`; accessing it without checking `IsSuccess` is a programming error surfaced by the nullable analyzer.

### Defensive copy in `Failure(ValidationResult)`

```csharp
public static Result<TResult> Failure(ValidationResult validationResult) =>
    new()
    {
        IsSuccess = false,
        ValidationResults =
        [
            new ValidationResult(
                validationResult.ErrorMessage,
                validationResult.MemberNames.ToArray())   // defensive copy
        ]
    };
```

`System.ComponentModel.DataAnnotations.ValidationResult` stores `MemberNames` as an `IEnumerable<string>` internally backed by whatever the caller passed (which could be a mutable `List<string>`). The defensive copy via `.ToArray()` ensures the stored list cannot be mutated after the result is created, satisfying the immutability guarantee.

### `ValidationResult` convenience property

Returns `ValidationResults[0]` or `null`. This allows code written for single-error results to remain readable without knowing about multi-error results:

```csharp
if (result.IsFailure)
    Console.WriteLine(result.ValidationResult?.ErrorMessage); // single-error path
```

### Internal `Failure(IReadOnlyList<ValidationResult>)` overload

Used exclusively by `Result.Combine` and `Map`/`Bind` extension propagation. It is `internal` to prevent external code from constructing multi-error failures directly — the only supported public path to multi-error failures is through `Combine`.

---

## `Result<TResult, TError>` — Internal Design

```csharp
public sealed record Result<TResult, TError> : IResult
    where TResult : notnull
    where TError  : notnull
{
    public TResult? Value { get; init; } = default!;
    public TError?  Error { get; init; } = default!;
    private Result() { }
}
```

`TError : notnull` prevents `Result<T, Exception?>` style abuse. Both type parameters are non-nullable at the constraint level.

No `ValidationResults` property — this type's error channel is fully owned by the caller. It is compatible with exception subclasses, discriminated-union-like structs, enums, and domain error records.

---

## Extension Methods — Design

All extension methods live in a single static class `ResultExtensions` in the same namespace (`FrenchExDev.Net.Result`). No import is needed beyond the base package reference.

### Two orthogonal axes

```
              sync                async (on Result)     async pipeline (on Task<Result>)
Map           Map                 MapAsync              MapAsync (×2 overloads)
Bind/Then     Bind / Then         BindAsync / ThenAsync BindAsync / ThenAsync
Recover       Recover             RecoverAsync          RecoverAsync
Tap           Tap                 TapAsync              TapAsync
TapError      TapError            TapErrorAsync         TapErrorAsync
Match         Match               MatchAsync            MatchAsync
```

The **async pipeline** overloads extend `Task<Result<T>>` rather than `Result<T>`. This lets callers chain from an async factory all the way to the terminal operation without a single intermediate `await`:

```csharp
// One await at the very end only
string result = await GetUserAsync()       // Task<Result<User>>
    .ThenAsync(ValidateAsync)              // Task<Result<User>>
    .MapAsync(u => u.Name)                 // Task<Result<string>>
    .MatchAsync(n => n, _ => "unknown");   // Task<string>
```

### `Bind` vs `Map`

| Operation | Input function | When function runs | Returns |
|---|---|---|---|
| `Map` | `T → TOut` | Only on success | `Result<TOut>` |
| `Bind` / `Then` | `T → Result<TOut>` | Only on success | `Result<TOut>` (from binder) |

`Bind` is the monadic bind (`>>=`). `Then` is an alias — prefer `Then` in sequential pipelines for readability ("do this, then that"), `Bind` in functional/point-free style.

### `Ensure` — guard predicate (`Result<T>` only)

`Ensure` converts a passing `Result<T>` to a failure if a predicate fails. It only runs on success; a pre-existing failure passes through unchanged. Two overloads:

```csharp
// No-arg factory — error object allocated only when the predicate fails
.Ensure(u => u.Age >= 18, () => new ValidationResult("Too young", ["Age"]))

// Value factory — error depends on the current value
.Ensure(u => u.IsVerified, u => new ValidationResult($"User {u.Id} not verified", ["IsVerified"]))
```

`Ensure` is not available on `Result<T, TError>` because the typed error channel does not have a meaningful "merge" semantic for multiple `Ensure` calls. Use `Bind` to a function returning `Result<T, TError>` instead.

### `Recover`

Turns a failure back into a success by computing a fallback value. Useful at the edge of a pipeline when you need to guarantee a non-failure result:

```csharp
// Result<T> — receives all validation errors
.Recover(errs => ComputeDefault(errs))

// Result<T, TError> — receives the typed error
.Recover(err  => ComputeDefault(err))
```

`Recover` only fires if `IsFailure && ValidationResults.Count > 0`. A structurally empty failure (no errors) is passed through unchanged.

### `Tap` / `TapError`

Side-effect observation that does not alter the result. The return value is always the original result, making them safe to insert anywhere in a pipeline:

```csharp
.Tap(u      => logger.Info("User loaded: {Id}", u.Id))     // fires on success
.TapError(e => logger.Warn("Failed: {Msg}", e))            // fires on failure
```

### `ValueOrDefault` vs `ValueOrElse`

| Method | When to use |
|---|---|
| `ValueOrDefault(fallback)` | Fallback is a constant or already-constructed value |
| `ValueOrElse(func)` | Fallback is expensive or depends on the error payload |

---

## Error Propagation Rules

For `Result<T>`:

| Combinator | On success | On failure |
|---|---|---|
| `Map` | Applies mapper, wraps in `Success` | Propagates `ValidationResults` as-is |
| `Bind` / `Then` | Calls binder (may return failure) | Propagates `ValidationResults` as-is |
| `Recover` | Passes through | Calls recovery, wraps in `Success` |
| `Ensure` | Runs predicate; may convert to failure | Passes through |
| `Tap` | Runs action, passes through | Passes through |
| `TapError` | Passes through | Runs action, passes through |

For `Result<T, TError>`:

| Combinator | On success | On failure |
|---|---|---|
| `Map` | Applies mapper | Propagates `Error` as-is |
| `Bind` / `Then` | Calls binder | Propagates `Error` as-is |
| `Recover` | Passes through | Calls recovery, wraps in `Success` |
| `Tap` | Runs action, passes through | Passes through |
| `TapError` | Passes through | Runs action, passes through |

---

## Namespace Layout

```
FrenchExDev.Net.Result
├── IResult                 (interface)
├── Result                  (sealed record)
├── Result<TResult>         (sealed record)
├── Result<TResult, TError> (sealed record)
└── ResultExtensions        (static class — all extension methods)
```

Single namespace, no sub-namespaces. The full surface area is available with one `using`.

---

## Thread Safety

All instances are immutable after construction. The defensive copy of `MemberNames` in `Result<T>.Failure(ValidationResult)` ensures external mutable state cannot reach into a stored result. Concurrent reads from the same instance are safe without synchronization.

---

## What This Library Deliberately Does Not Do

- **No implicit conversions** — `T → Result<T>` or `Exception → Result<T, Exception>` implicit operators are omitted to keep construction explicit and avoid surprises.
- **No `Unit` type** — `Result` exists as a first-class type rather than `Result<Unit>`. This avoids noise when the caller does not need to distinguish the returned value type.
- **No `Option<T>` / `Maybe<T>`** — absent values are out of scope. `Result` is about success/failure, not presence/absence.
- **No railway-operator syntax** — `>>` or `|>` style operator overloads are not provided; method chaining is idiomatic C#.
- **No logging / serialization hooks** — side effects belong to `Tap`/`TapError` callbacks provided by the caller.
