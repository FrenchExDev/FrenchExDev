# RESULT-PATTERN — Philosophy

The Result pattern brings *expected* failure into the return type. Exceptions are reserved for *unexpected* programming errors and unrecoverable environment problems. Anything a caller is reasonably expected to handle becomes a `Result<T>` value, not a thrown `Exception`.

## Two Channels Are Not Symmetric

C# methods communicate outcomes through two channels:

- **Return values** — declared in the signature, visible at every call site, checked by the compiler.
- **Exceptions** — invisible in the signature, unchecked by the compiler, non-local in control flow.

Using exceptions for expected failures (validation, "not found", business rule rejection) produces code that:

- Hides failure modes from the caller's perspective
- Forces callers to know which exception types to catch from documentation, not the type system
- Cannot be composed the way a `Result` value can
- Silently swallows failures when callers forget to catch
- Breaks reasoning across `await` boundaries

The Result pattern moves expected failures into the return type. They become first-class, composable, and impossible to ignore.

## Three Types, Not One

Default to **three** Result variants — not a single overloaded generic:

| Type | Use case |
|---|---|
| `Result` | Void command — succeeded or failed, no payload |
| `Result<T>` | Validation-oriented — success value or one or more `ValidationResult` errors |
| `Result<T, TError>` | Domain operation — success value or a strongly-typed error |

Reasons for the split:

- A single `Result<T>` forces a `Unit` type for void commands. `Unit` adds noise everywhere it appears.
- Validation produces *lists* of errors (form-style). Domain failures usually carry a single typed error (enum, exception subtype, discriminated union).
- Reusing `System.ComponentModel.DataAnnotations.ValidationResult` for the validation case enables `ValidationProblemDetails` interop with zero adapter code.

## Immutability Is Non-Negotiable

Result values are `sealed record`s with `init`-only properties and **private constructors**. The only construction path is `Success(...)` / `Failure(...)`. There are **no implicit conversions** — `T` does not silently become `Result<T>`, and `Exception` does not silently become a typed error.

Why:

- A result value observed on one thread will never change state on another.
- Reading code is unambiguous — you always know you are looking at a Result, never an accidentally promoted value.
- Defensive copies of mutable error collections (e.g. `ValidationResult.MemberNames`) are taken at construction time so external mutation cannot leak in.

## Async Is First-Class

Every combinator (`Map`, `Bind`/`Then`, `Recover`, `Tap`, `TapError`, `Match`) ships in **three forms**:

1. **Sync** on `Result<T>`
2. **Async** on `Result<T>` (when the lambda is async)
3. **Pipeline** on `Task<Result<T>>` (when the source is async)

The pipeline overloads are the load-bearing piece. Without them, every async step needs an intermediate `await`, destroying the chained-pipeline benefit. With them, an entire async flow becomes a single expression with one terminal `await`.

```csharp
string greeting = await GetUserAsync()                    // Task<Result<User>>
    .ThenAsync(u => ValidateAsync(u))                     // Task<Result<User>>
    .TapAsync(u => auditLog.RecordAsync(u))               // side effect, passes through
    .MapAsync(u => u.Name)                                // Task<Result<string>>
    .MatchAsync(
        onSuccess: name => $"Hello, {name}!",
        onFailure: errs => $"Error: {errs[0].ErrorMessage}");
```

## The Railway Metaphor

Two parallel tracks: success and failure. Each step is a switch — if input arrives on the success track, the step runs and either stays on success or diverts to failure. If input arrives on failure, the step is skipped and the failure propagates unchanged.

- `Map` and `Bind`/`Then` are switches.
- `Tap` / `TapError` are observers that never change tracks.
- `Recover` is a crossover that can move from failure back to success.
- `Match` is the terminal station where both tracks converge to a single output.

This makes complex pipelines easy to reason about: trace the success path, then trace the failure path. They never mix until `Match`.

## When To Use a Result vs an Exception

| Scenario | Use |
|---|---|
| Validation failure (expected, recoverable, part of domain logic) | `Result<T>` |
| Business rule rejection | `Result<T>` or `Result<T, TError>` |
| Record not found in repository | `Result<T, TError>` |
| Infrastructure failure caller must handle (DB down, network timeout) | `Result<T, TError>` |
| Truly unexpected runtime error (`NullReferenceException`, programming bug) | `Exception` — let it propagate |
| Out-of-memory, stack overflow, process corruption | `Exception` — never catch |
| Third-party library that throws | `FromTry<T, TException>` at the boundary |

Rule of thumb: if the caller is expected to handle the failure as part of normal logic, put it in the return type. If the failure represents a bug or an unrecoverable environment problem, let it be an exception.

## Anchor Package

Reference implementation: [`Net/FrenchExDev/Result/`](../../../Net/FrenchExDev/Result/) — three sealed-record types, full async pipeline, `Combine` with value tuples, `FromTry` boundary helper.
