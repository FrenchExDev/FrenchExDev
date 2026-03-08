# Philosophy — FrenchExDev.Net.Result

## The Core Problem

C# methods have two channels for communicating outcomes to callers.

**The return value** — declared in the signature, visible at every call site, checked by the compiler.

**Exceptions** — invisible in the signature, unchecked by the compiler, non-local in control flow.

These two channels are not symmetric. Exceptions are designed for *unexpected*, *unrecoverable* failures — out-of-memory, stack overflow, hardware faults. They are poorly suited for *expected*, *recoverable* outcomes that are part of normal domain logic: a field that fails validation, a record that does not exist, a business rule that rejects an input.

Using exceptions for expected failures produces code that:

- Hides the fact that an operation can fail from the caller's perspective
- Requires the caller to know which specific exception types to catch, often from documentation rather than the type system
- Breaks structured pipelines — a `try/catch` block cannot be composed the way a `Result` can
- Silently swallows failures when callers forget to catch
- Makes reasoning about async code harder (exception propagation across `await` boundaries has surprising behavior)

The Result pattern brings expected failures into the return type, making them first-class, composable, and impossible to ignore.

---

## Design Decisions in This Library

### Three types, not one

Most Result libraries provide a single generic `Result<T>` or `Result<T, TError>`. This library deliberately provides three:

`Result` — for void commands with no meaningful payload on either path.
`Result<T>` — for validation-oriented operations where failure is described by `ValidationResult` from `System.ComponentModel.DataAnnotations`.
`Result<T, TError>` — for operations where the error has semantic domain meaning that goes beyond a message string.

This avoids the forced choice between "use a `Unit` type everywhere" and "lose the type distinction between a validation error and a domain error". Each scenario gets the type it deserves.

### Validation errors are lists, not single values

`Result<T>` carries `IReadOnlyList<ValidationResult>`, not a single `ValidationResult`. This reflects the reality of form validation: users expect *all* their mistakes to be reported at once. The `Combine` operation exists precisely to collect errors from independent validators into a single failure result, enabling one-pass validation across multiple fields.

### `ValidationResult` is a system type, not a custom one

Rather than defining a custom error record, `Result<T>` deliberately reuses `System.ComponentModel.DataAnnotations.ValidationResult`. This means:

- It interoperates directly with ASP.NET Core model validation infrastructure
- It carries `MemberNames` for field-level attribution — standard for API error responses
- No translation layer is needed between validation results and `ValidationProblemDetails`

### Immutability is non-negotiable

All three types are `sealed record`s with `init`-only setters and private constructors. This is not a performance optimization — it is a correctness guarantee. A result value observed on one thread will never change state on another. `Result<T>.Failure(ValidationResult)` takes a defensive copy of `MemberNames` so that even if the caller mutates the original `ValidationResult` after creation, the stored value is unaffected.

### No implicit conversions

`T` does not implicitly convert to `Result<T>`. `Exception` does not implicitly convert to `Result<T, Exception>`. Every result is constructed explicitly through `Success(...)` or `Failure(...)`. This makes reading code unambiguous — you always know you are looking at a result, not an accidentally promoted value.

### Async is a first-class citizen

Every combinator (`Map`, `Bind`, `Then`, `Recover`, `Tap`, `TapError`, `Match`) has:
1. A synchronous form on `Result<T>`
2. An async form on `Result<T>` (when the mapper/binder is async)
3. A pipeline form on `Task<Result<T>>` (when the source is async)

The pipeline form is the key piece. Without it, callers would need to `await` between every step, destroying the readability advantage of chaining. With it, a complete async pipeline from input to output can be written as a single expression with one terminal `await`.

---

## Comparison with Other Libraries

The .NET ecosystem has several popular Result-type libraries. Here is an honest comparison.

### [CSharpFunctionalExtensions](https://github.com/vkhorikov/CSharpFunctionalExtensions) — `Result` / `Result<T>` / `Result<T, E>`

The most widely-used Result library for C#. Well-documented, mature, large API surface.

| | CSharpFunctionalExtensions | FrenchExDev.Net.Result |
|---|---|---|
| Typed error | `Result<T, E>` | `Result<T, TError>` |
| Validation errors (list) | No built-in multi-error | `Result<T>` with `IReadOnlyList<ValidationResult>` |
| DataAnnotations integration | Not built-in | Native — uses `ValidationResult` directly |
| `Combine` (collect all errors) | `Result.Combine(IEnumerable<Result>)` — bool only | Typed `Combine<T1,T2,...>` returns value tuple |
| Implicit conversions | ✅ — `T` → `Result<T, E>` | ❌ — explicit only |
| `Maybe<T>` / `Option<T>` | ✅ — full `Maybe<T>` type | Out of scope |
| `ValueObject` / `Entity` | ✅ — large DDD surface | Out of scope |
| Async pipeline on `Task<Result>` | Via extension methods | Via extension methods |
| Package size | Large — many features | Small — focused |
| .NET compatibility | `netstandard2.0`+ | `net10.0` |

**When to prefer CSharpFunctionalExtensions:** You want a full DDD toolkit (`ValueObject`, `Entity`, `Maybe<T>`) in addition to Result types. You need `netstandard2.0` compatibility.

### [ErrorOr](https://github.com/amantinband/error-or) — `ErrorOr<T>`

Designed for the "one type for all failures" pattern. `ErrorOr<T>` is always either a value or a list of typed `Error` values.

| | ErrorOr | FrenchExDev.Net.Result |
|---|---|---|
| Error model | `IList<Error>` (always a list) | `IReadOnlyList<ValidationResult>` for `Result<T>`, single typed `TError` for `Result<T, TError>` |
| Typed errors | Via `Error` struct with type enum (`Validation`, `NotFound`, etc.) | Via `TError : notnull` — any type you choose |
| No-value result | Not natively — use `ErrorOr<Success>` | `Result` |
| DataAnnotations integration | Not built-in | Native |
| `Match` / `Switch` | ✅, incl. `MatchFirst` | ✅ |
| Async pipeline | ✅ | ✅ |
| Source generator | ✅ — `[ErrorOr]` attribute | ❌ |
| Implicit conversions | ✅ — `T` → `ErrorOr<T>` | ❌ |

**When to prefer ErrorOr:** You want a uniform error model across your entire app with a fixed set of error categories (Validation, NotFound, Conflict, …). You like the discriminated-union feel of a single error type with a `Type` property.

### [FluentResults](https://github.com/altmann/FluentResults) — `Result` / `Result<T>`

Focuses on rich, chainable, message-based error objects. Each `Result` carries a list of `IError` and `ISuccess` objects rather than typed errors.

| | FluentResults | FrenchExDev.Net.Result |
|---|---|---|
| Error model | `IList<IError>` (extensible objects) | `IReadOnlyList<ValidationResult>` or `TError` |
| Typed errors | Via `IError` implementations | Via generic type parameter |
| Success metadata | ✅ — `ISuccess` objects on success | ❌ |
| DataAnnotations integration | Manual adapter required | Native |
| Reasons / causes chain | ✅ — nested `IError.Reasons` | ❌ |
| Async pipeline | ✅ | ✅ |
| Implicit conversions | ✅ | ❌ |

**When to prefer FluentResults:** You need rich error objects with metadata, nested causes, or structured logging payloads attached to errors. You want success metadata alongside success values.

### [Ardalis.Result](https://github.com/ardalis/Result) — `Result<T>`

Combines a typed value with a status enum and a list of validation errors. Designed for use in application services and Clean Architecture.

| | Ardalis.Result | FrenchExDev.Net.Result |
|---|---|---|
| Error model | `List<string>` + `ResultStatus` enum | `IReadOnlyList<ValidationResult>` or `TError` |
| Typed errors | Via `ResultStatus` (Invalid, NotFound, Forbidden…) | Via generic `TError` |
| No-value result | Via `Result` (non-generic) | `Result` |
| DataAnnotations integration | ⚠️ (string messages) | Full (`ValidationResult` with `MemberNames`) |
| ASP.NET Core integration | ✅ — `ToActionResult()` helper | Manual `Match` → `IActionResult` |
| Async pipeline | Limited | Full |
| Immutability | Mutable (list is a `List<string>`) | Fully immutable |

**When to prefer Ardalis.Result:** You are building a Clean Architecture app and want the `ToActionResult()` mapping and the `ResultStatus` enum built in, and you do not need generic typed errors.

### [OneOf](https://github.com/mcintyre321/OneOf) — Discriminated unions

Not a Result library per se, but frequently used to build Result-like types via `OneOf<T0, T1>`.

| | OneOf | FrenchExDev.Net.Result |
|---|---|---|
| Error model | Any type — `OneOf<Success, Error1, Error2>` | Generic `TError` or `ValidationResult` list |
| Number of cases | 2–9 type parameters | 2 (Success / Failure) |
| Exhaustive match | ✅ — compiler-enforced via source generator | ✅ — `Match` covers both |
| Combinator API | None — manual pattern match only | Full `Map`, `Bind`, `Then`, `Recover`, etc. |
| Async pipeline | None built-in | Full |
| Compose across types | Hard — each `OneOf` type is different | `Combine` works across `Result<T1>`, `Result<T2>` |

**When to prefer OneOf:** You need more than two outcome branches (e.g., three distinct domain error types) and are comfortable writing manual `Switch`/`Match` without combinators.

---

## Summary Comparison Table

| Feature | CSharpFunctionalExtensions | ErrorOr | FluentResults | Ardalis.Result | OneOf | **FrenchExDev.Net.Result** |
|---|---|---|---|---|---|---|
| Typed error channel | ✅ | ⚠️ (Error struct) | Via IError | Via enum | ✅ | ✅ |
| Multi-error collection | ❌ | Always | ✅ | ✅ (strings) | ❌ | ✅ (on `Result<T>`) |
| DataAnnotations native | ❌ | ❌ | ❌ | ⚠️ | ❌ | **✅** |
| Immutable | ✅ | ✅ | ⚠️ | ❌ | ✅ | **✅** |
| No implicit conversions | ❌ | ❌ | ❌ | ❌ | ❌ | **✅** |
| Full async pipeline | ⚠️ | ⚠️ | ⚠️ | ❌ | ❌ | **✅** |
| Separate no-value type | `Result` | ❌ | `Result` | `Result` | Manual | **`Result`** |
| `Combine` with value tuple | ❌ | ❌ | ❌ | ❌ | ❌ | **✅** |
| Small / focused | ❌ | ✅ | ✅ | ✅ | ✅ | **✅** |
| DDD extras | ✅ | ❌ | ❌ | ⚠️ | ❌ | ❌ |
| .NET target | netstandard2.0 | netstandard2.0 | netstandard2.0 | netstandard2.0 | netstandard2.0 | net10.0 |

---

## Why Use This Library

### Use it when

- **You validate user input** and need to return all field-level errors at once, attributed to specific member names, compatible with `ValidationProblemDetails`.
- **You build service layers** with typed domain errors (`NotFound`, `Forbidden`, enum cases, domain exception subtypes) and want the error type enforced by the compiler.
- **You chain async operations** and want to write the entire pipeline as a single expression without littering intermediate `await`s and `if (result.IsFailure) return` guards.
- **You value explicitness** — no implicit conversions, no magic, construction is always `Success(...)` or `Failure(...)`.
- **You target net10+** and want something lean with zero third-party dependencies (only `System.ComponentModel.DataAnnotations`).
- **You combine independent validators** and want all errors merged automatically via `Combine`.

### Do not use it when

- **You need `netstandard2.0` compatibility** — this library targets `net10.0` and is not backported.
- **You need a `Maybe<T>` / `Option<T>` type** — absent values are out of scope; use `CSharpFunctionalExtensions` or `Optional`.
- **You need more than two outcome branches** — if an operation can return three distinct typed results, use `OneOf<T0, T1, T2>` instead.
- **You want ASP.NET Core mapping built in** — this library returns plain `Result<T>` values; you map them to `IActionResult` yourself via `Match`. Use `Ardalis.Result` if you want `ToActionResult()` out of the box.
- **You need DDD primitives** — no `ValueObject`, `Entity`, or `AggregateRoot`. Use `CSharpFunctionalExtensions` for those.
- **Your team strongly prefers exceptions** — a Result type requires discipline across the whole codebase. Partial adoption (some methods return `Result`, others throw) is workable but produces friction at boundaries.

---

## The Railway Metaphor

The most common mental model for Result pipelines is "railway-oriented programming" (coined by Scott Wlaschin). Imagine a two-track railway:

```
 ──────────────────────────────────────────── success track ──
  input → [validate] → [enrich] → [persist] → [notify] → output
 ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─
  any failure ──────────────────────────────────────────────── failure track ──
```

Each step is a switch: if the input arrives on the success track, it runs and either stays on success or diverts to failure. If it arrives on the failure track, it is skipped entirely and the failure is propagated unchanged.

`Map` and `Bind`/`Then` are the switches. `Tap`/`TapError` are observers that do not change tracks. `Recover` is a crossover that can move a train from failure back to success. `Match` is the terminal station where both tracks converge to a single output.

This mental model makes it easy to reason about complex pipelines: trace the success path, then trace the failure path. They never mix until `Match`.

---

## On Exceptions vs. Results — When to Use Which

Results and exceptions are not mutually exclusive. Use them for what they are designed for:

| Scenario | Use |
|---|---|
| Validation failure (expected, recoverable, part of domain logic) | `Result<T>` |
| Business rule rejection | `Result<T>` or `Result<T, TError>` |
| Record not found in repository | `Result<T, TError>` |
| Infrastructure failure (DB down, network timeout) you want the caller to handle explicitly | `Result<T, TError>` |
| Truly unexpected runtime error (`NullReferenceException`, programming error) | `Exception` — let it propagate |
| Out-of-memory, stack overflow, process corruption | `Exception` — never catch |
| Third-party library that throws | `Result.FromTry<T, TException>` at the boundary |

The rule of thumb: if the caller is expected to handle a failure as part of normal logic, put it in the return type. If the failure represents a bug or an unrecoverable environment problem, let it be an exception.
