# RESULT-PATTERN — Requirements

A checklist for any Result-pattern implementation in this codebase.

## Type Requirements

- [ ] Provide three Result types — `Result`, `Result<T>`, `Result<T, TError>` — not a single overload.
- [ ] All three are `sealed record`.
- [ ] All three have **private constructors**.
- [ ] Construction is only via static `Success(...)` / `Failure(...)` factories.
- [ ] All three implement a common `IResult` interface exposing `IsSuccess` / `IsFailure`.
- [ ] `Result<T>` constrains `T : notnull`.
- [ ] `Result<T, TError>` constrains both `T : notnull` and `TError : notnull`.

## Immutability Requirements

- [ ] All properties are `init`-only.
- [ ] No public mutators or builder-style modifications.
- [ ] `Result<T>.Failure(ValidationResult)` takes a **defensive copy** of `MemberNames` (the source collection is potentially mutable).
- [ ] No implicit conversions from `T` to `Result<T>`.
- [ ] No implicit conversions from `Exception` to typed errors.

## Validation Error Requirements

- [ ] `Result<T>` carries `IReadOnlyList<ValidationResult>`, not a single value.
- [ ] Reuses `System.ComponentModel.DataAnnotations.ValidationResult` (for `ValidationProblemDetails` interop).
- [ ] The multi-error `Failure(IReadOnlyList<ValidationResult>)` constructor is **`internal`**, not public.
- [ ] First-error convenience accessor returns `null` on success — never throws.

## Combinator Requirements

The library MUST provide all of:

- [ ] `Map(Func<T, TOut>)` — runs only on success.
- [ ] `Bind(Func<T, Result<TOut>>)` and an alias `Then`.
- [ ] `Recover(Func<errors, T>)` — runs only on failure.
- [ ] `Tap(Action<T>)` — observe success without changing the result.
- [ ] `TapError(Action<errors>)` — observe failure without changing the result.
- [ ] `Match(onSuccess, onFailure)` — exhaustive terminal operation.
- [ ] `Ensure(predicate, errorFactory)` on `Result<T>` only.
- [ ] `ValueOrThrow()`, `ValueOrDefault(fallback)`, `ValueOrElse(Func<errors, T>)`.

## Async Requirements

For every combinator above, provide all three forms:

- [ ] **Sync** on `Result<T>`.
- [ ] **Async** on `Result<T>` (when the lambda is async).
- [ ] **Pipeline** on `Task<Result<T>>` (so callers can chain from an async source).

Pipeline overloads on `Task<Result<T>>` accept both sync and async lambdas. A complete async chain must be expressible with **one** terminal `await`.

## `Combine` Requirements

- [ ] Static method on the non-generic `Result` type, not an extension.
- [ ] Returns a value tuple: `Result<(T1, T2, ...)>` — preserves typed success values.
- [ ] On any failure, **merges all** validation errors into the result.
- [ ] Overloads for arities 2 through 7.

## `FromTry` Requirements

- [ ] Catches **only** the specified `TError : Exception`.
- [ ] All other exception types propagate up unchanged.
- [ ] Both sync (`Func<T>`) and async (`Func<Task<T>>`) variants.

## What MUST NOT Be Provided

- [ ] No `Unit` type — `Result` exists as a first-class no-value type.
- [ ] No `Option<T>` / `Maybe<T>` blended into the same library — separate concern.
- [ ] No railway-style operator overloads (`>>`, `|>`).
- [ ] No built-in logging or serialization hooks — those are caller concerns expressed through `Tap` / `TapError`.
- [ ] No mutable error collections exposed to callers.

## Testing Requirements

Every Result-pattern consumer SHOULD test:

- [ ] The happy path returns `IsSuccess` with the expected value.
- [ ] Each failure mode returns `IsFailure` with the correct error content (including `MemberNames` for `Result<T>`).
- [ ] `Combine` merges all errors when multiple inputs fail.
- [ ] Pipelines short-circuit — assert subsequent steps did NOT run after a failure.
- [ ] `Ensure` chains: the first failing predicate wins; later predicates are not evaluated.
- [ ] `Recover` only fires on failure; pre-existing successes pass through unchanged.

## Anchor Package

[`Net/FrenchExDev/Result/`](../../../Net/FrenchExDev/Result/) — implements every requirement above with 47 tests at 100% branch coverage.
