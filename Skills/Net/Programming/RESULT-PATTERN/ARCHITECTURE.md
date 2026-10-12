# RESULT-PATTERN — Architecture

How to structure a Result-pattern library so it composes correctly, stays immutable, and supports async pipelines.

## Type Hierarchy

```
IResult                       (interface — IsSuccess / IsFailure)
├── Result                    (no value — pass/fail only)
├── Result<TResult>           (value + validation errors list)
└── Result<TResult, TError>   (value + typed error)
```

All three are `sealed record`s with **private constructors**. The only way to obtain an instance is through static factory methods (`Success`, `Failure`).

The three types are **not** a class hierarchy of each other. `Result` is not a special case of `Result<T>`; they have different error representations and different combinator semantics. Keeping them distinct avoids the `Unit` type anti-pattern.

## Constraints

```csharp
public sealed record Result<TResult> where TResult : notnull
public sealed record Result<TResult, TError>
    where TResult : notnull
    where TError  : notnull
```

The `notnull` constraint prevents `Result<string?>` and `Result<int?>` — a result carries a value **or** errors, never both, never neither. The nullable annotation on the internal `Value` field (`TResult?`) is intentional: on a failure, `Value` is `default!`; accessing it without checking `IsSuccess` is a programming error surfaced by the nullable analyzer.

## Validation Errors Are a List

`Result<T>` carries `IReadOnlyList<ValidationResult>`, not a single `ValidationResult`. This reflects the reality of form validation: users expect *all* their mistakes reported at once. The `Combine` operation merges errors from independent validators.

The first-error convenience property is acceptable but should never be the primary access path:

```csharp
public ValidationResult? ValidationResult =>
    ValidationResults.Count > 0 ? ValidationResults[0] : null;
```

## Defensive Copy Of External Mutable Collections

`Failure(ValidationResult)` must defensively copy any mutable inputs:

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

`System.ComponentModel.DataAnnotations.ValidationResult.MemberNames` is backed by whatever the caller passed (often a mutable `List<string>`). Without the `.ToArray()` copy, mutating the original after construction would change the stored result.

## Internal Multi-Error Constructor

The multi-error `Failure(IReadOnlyList<ValidationResult>)` overload should be **`internal`**, not public. The only supported public path to multi-error failures is through `Combine` and through propagation by combinators (`Map`, `Bind`). External code constructing multi-error failures directly is almost always a smell.

## Combinator Set

The minimal set every Result library should provide:

| Combinator | Input function | When function runs | Returns |
|---|---|---|---|
| `Map` | `T → TOut` | On success only | `Result<TOut>` |
| `Bind` / `Then` | `T → Result<TOut>` | On success only | `Result<TOut>` (from binder) |
| `Recover` | `errs → T` | On failure only | Wraps in `Success` |
| `Ensure` | `T → bool` + error factory | On success only | May convert to failure |
| `Tap` | `T → void` | On success only | Original result, unchanged |
| `TapError` | `errs → void` | On failure only | Original result, unchanged |
| `Match` | both `onSuccess`/`onFailure` | Always | Caller's chosen output type |

`Then` is an alias for `Bind`. Prefer `Then` in sequential pipelines for readability ("do this, then that"), `Bind` in functional/point-free style.

`Ensure` is only meaningful on `Result<T>` (validation list semantics). Do not provide it on `Result<T, TError>` — there is no merge semantics for typed errors.

## Async Pipeline — Two Orthogonal Axes

```
              sync                async (on Result)     async pipeline (on Task<Result>)
Map           Map                 MapAsync              MapAsync (×2 overloads)
Bind/Then     Bind / Then         BindAsync / ThenAsync BindAsync / ThenAsync
Recover       Recover             RecoverAsync          RecoverAsync
Tap           Tap                 TapAsync              TapAsync
TapError      TapError            TapErrorAsync         TapErrorAsync
Match         Match               MatchAsync            MatchAsync
```

The third column — extensions on `Task<Result<T>>` rather than on `Result<T>` itself — is the load-bearing piece. It lets callers chain from an async source all the way to the terminal operation without a single intermediate `await`.

## `Combine` — Multi-Result AND Gate

`Combine<T1, T2, ...>` is a **static** method on the non-generic `Result` type, not an extension method. It operates across multiple heterogeneous `Result<T>` types and produces a `Result<(T1, T2, ...)>` value tuple.

Semantics:
- **All succeed** → `Result<(T1, T2)>.Success((r1.Value!, r2.Value!))`
- **Any fail** → `Result<(T1, T2)>.Failure(Merge(r1.ValidationResults, r2.ValidationResults))`

Provide overloads for 2 through 7 results. Beyond 7, callers should decompose their domain into sub-combines.

## `FromTry` — Controlled Exception Boundary

```csharp
public static Result<T, TError> FromTry<T, TError>(Func<T> factory)
    where TError : Exception
```

`FromTry` only catches `TError`. **All other exception types propagate up unchanged.** It is not a blanket exception suppressor — it is a controlled boundary between exception-throwing third-party APIs and the Result world.

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

## Namespace Layout

Single namespace, no sub-namespaces. The full surface area is available with one `using`. All extensions live in a single static `ResultExtensions` class in the same namespace as the types.

## What This Library Deliberately Does Not Do

- **No implicit conversions** — `T → Result<T>` is omitted to keep construction explicit and avoid surprises.
- **No `Unit` type** — `Result` exists as a first-class type rather than `Result<Unit>`.
- **No `Option<T>` / `Maybe<T>`** — absent values are out of scope.
- **No railway-operator overloads** — `>>` / `|>` are not idiomatic C#; method chaining is.
- **No logging / serialization hooks** — side effects belong to `Tap` / `TapError` callbacks the caller provides.

## Anchor Package

[`Net/FrenchExDev/Result/`](../../../Net/FrenchExDev/Result/) — see `doc/ARCHITECTURE.md` for the full reference implementation.
