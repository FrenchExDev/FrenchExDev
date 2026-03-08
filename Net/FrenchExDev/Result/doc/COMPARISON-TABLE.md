# Comparison Table — C# Result-Pattern Libraries

A side-by-side feature matrix covering the most-used Result-type libraries for .NET.

Notation: ✅ = built-in, first-class. ⚠️ = partial / workaround required. ❌ = absent.

---

## 1. Package Overview

| | [CSharpFunctionalExtensions](https://github.com/vkhorikov/CSharpFunctionalExtensions) | [ErrorOr](https://github.com/amantinband/error-or) | [FluentResults](https://github.com/altmann/FluentResults) | [Ardalis.Result](https://github.com/ardalis/Result) | [OneOf](https://github.com/mcintyre321/OneOf) | **FrenchExDev.Net.Result** |
|---|---|---|---|---|---|---|
| NuGet ID | `CSharpFunctionalExtensions` | `ErrorOr` | `FluentResults` | `Ardalis.Result` | `OneOf` | `FrenchExDev.Net.Result` |
| Minimum .NET | `netstandard2.0` | `netstandard2.0` | `netstandard2.0` | `netstandard2.0` | `netstandard2.0` | `net10.0` |
| Zero third-party dependencies | ✅ | ✅ | ✅ | ✅ | ✅ | **✅** |
| Scope | Full DDD toolkit | Result only | Result only | Result + ASP.NET | DU only | Result only |

---

## 2. Result Types

| | CSharpFunctionalExtensions | ErrorOr | FluentResults | Ardalis.Result | OneOf | **FrenchExDev.Net.Result** |
|---|---|---|---|---|---|---|
| Valueless success/failure (`Result`) | ✅ | ⚠️ (`ErrorOr<Success>`) | ✅ | ✅ | Manual | **✅** |
| Value + validation errors (`Result<T>`) | ⚠️ (string errors only) | ✅ (always a list) | ✅ (IError list) | ⚠️ (string list) | ❌ | **✅ (ValidationResult list)** |
| Value + single typed error (`Result<T, TError>`) | ✅ | ⚠️ (fixed Error struct) | ⚠️ (IError impl) | ⚠️ (ResultStatus enum) | ✅ | **✅** |
| Optional / Maybe type | ✅ (`Maybe<T>`) | ❌ | ❌ | ❌ | ⚠️ (manual) | ❌ |
| Success metadata | ❌ | ❌ | ✅ (`ISuccess`) | ❌ | ❌ | ❌ |

---

## 3. Error Representation

| | CSharpFunctionalExtensions | ErrorOr | FluentResults | Ardalis.Result | OneOf | **FrenchExDev.Net.Result** |
|---|---|---|---|---|---|---|
| Single typed error (any type) | ✅ | ⚠️ (`Error` struct only) | ⚠️ (IError impl) | ⚠️ (ResultStatus enum) | ✅ | **✅** |
| Multiple errors (list) | ⚠️ (join string only) | Always | ✅ | ✅ (strings) | ❌ | **✅ (on `Result<T>`)** |
| Field-level attribution (member names) | ❌ | ❌ | ❌ | ❌ | ❌ | **✅ (`ValidationResult.MemberNames`)** |
| `System.ComponentModel.DataAnnotations.ValidationResult` | ❌ | ❌ | ❌ | Partial | ❌ | **Native** |
| Structured error metadata | ❌ | ✅ (type enum: Validation, NotFound…) | ✅ (IError.Metadata) | ⚠️ (ResultStatus) | ❌ | ❌ |
| Nested error causes chain | ❌ | ❌ | ✅ (`IError.Reasons`) | ❌ | ❌ | ❌ |
| Custom error factory | N/A | ❌ | Via IError | ❌ | N/A | **✅ (`Func<ValidationResult>`)** |

---

## 4. Combinator API

| | CSharpFunctionalExtensions | ErrorOr | FluentResults | Ardalis.Result | OneOf | **FrenchExDev.Net.Result** |
|---|---|---|---|---|---|---|
| `Map` (transform value) | ✅ | ✅ | ✅ | ❌ | ❌ | **✅** |
| `Bind` / `Then` (monadic bind) | ✅ | ✅ | ✅ | ❌ | ❌ | **✅** |
| `Match` (exhaustive pattern-match) | ✅ | ✅ (`Match` / `MatchFirst`) | ✅ | ❌ | ✅ | **✅** |
| `Recover` (failure → success fallback) | ⚠️ | ✅ | ⚠️ | ❌ | ❌ | **✅** |
| `Tap` (side-effect on success) | ✅ | ✅ | ✅ | ❌ | ❌ | **✅** |
| `TapError` (side-effect on failure) | ✅ | ✅ | ✅ | ❌ | ❌ | **✅** |
| `Ensure` (guard predicate) | ✅ | ❌ | ❌ | ❌ | ❌ | **✅** |
| `ValueOrDefault` (static fallback) | ✅ | ❌ | ❌ | ❌ | ❌ | **✅** |
| `ValueOrElse` (computed fallback) | ⚠️ | ❌ | ❌ | ❌ | ❌ | **✅** |
| `Combine` (merge independent results) | ⚠️ (bool only) | ⚠️ (already a list) | ✅ | ❌ | ❌ | **✅ (value tuple, 2–7 args)** |
| `FromTry` (catch exception as failure) | ❌ | ❌ | ❌ | ❌ | ❌ | **✅** |

---

## 5. Async Support

| | CSharpFunctionalExtensions | ErrorOr | FluentResults | Ardalis.Result | OneOf | **FrenchExDev.Net.Result** |
|---|---|---|---|---|---|---|
| Async mapper on value (`MapAsync`) | ✅ | ✅ | ✅ | ❌ | ❌ | **✅** |
| Async binder (`BindAsync` / `ThenAsync`) | ✅ | ✅ | ✅ | ❌ | ❌ | **✅** |
| `Task<Result<T>>` pipeline overloads | ✅ | ✅ | ✅ | ❌ | ❌ | **✅** |
| Async `Recover` | ✅ | ✅ | ✅ | ❌ | ❌ | **✅** |
| Async `Tap` / `TapError` | ✅ | ✅ | ✅ | ❌ | ❌ | **✅** |
| Async `Match` on `Task<Result<T>>` | ✅ | ✅ | ✅ | ❌ | ❌ | **✅** |
| Single terminal `await` for full pipeline | ✅ | ✅ | ✅ | ❌ | ❌ | **✅** |

---

## 6. Design Decisions

| | CSharpFunctionalExtensions | ErrorOr | FluentResults | Ardalis.Result | OneOf | **FrenchExDev.Net.Result** |
|---|---|---|---|---|---|---|
| Immutable result values | ✅ | ✅ | ⚠️ (list is mutable) | ❌ | ✅ | **✅** |
| No implicit conversions | ❌ | ❌ | ❌ | ❌ | ❌ | **✅** |
| `notnull` constraint on value / error | ❌ | ❌ | ❌ | ❌ | ❌ | **✅** |
| Sealed types | ⚠️ | ⚠️ | ❌ | ❌ | ❌ | **✅** |
| Private constructor | ⚠️ | ⚠️ | ❌ | ❌ | ❌ | **✅** |
| Defensive copy of error collection | ❌ | N/A | ❌ | ❌ | ❌ | **✅** |

---

## 7. Integration and Ecosystem

| | CSharpFunctionalExtensions | ErrorOr | FluentResults | Ardalis.Result | OneOf | **FrenchExDev.Net.Result** |
|---|---|---|---|---|---|---|
| DataAnnotations native (`ValidationResult`) | ❌ | ❌ | ❌ | ⚠️ (strings) | ❌ | **✅** |
| `ValidationProblemDetails` compatible (no adapter) | ❌ | ⚠️ | ❌ | ⚠️ | ❌ | **✅** |
| `ToActionResult()` for ASP.NET Core | ❌ | ❌ | ❌ | ✅ | ❌ | ❌ |
| Source generator | ❌ | ✅ (`[ErrorOr]` attribute) | ❌ | ❌ | ✅ (`[GenerateOneOf]`) | ❌ |
| DDD primitives (`ValueObject`, `Entity`) | ✅ | ❌ | ❌ | ⚠️ | ❌ | ❌ |
| FluentValidation adapter | Community | Community | Community | ✅ | ❌ | ❌ |
| `ResultStatus` / fixed error category enum | ❌ | ✅ | ❌ | ✅ | ❌ | ❌ |
| More than 2 outcome branches | ❌ | ❌ | ❌ | ⚠️ | ✅ (up to 9) | ❌ |

---

## 8. `Combine` Semantics

`Combine` is arguably the biggest API differentiator. This table details exactly what each library produces when multiple validators are merged.

| | CSharpFunctionalExtensions | ErrorOr | FluentResults | Ardalis.Result | OneOf | **FrenchExDev.Net.Result** |
|---|---|---|---|---|---|---|
| Combine result type | `Result` (bool, no value) | N/A (always a list) | `Result` | N/A | N/A | **Value tuple `Result<(T1,T2,…)>`** |
| Collects all errors on failure | ✅ | N/A | ✅ | N/A | N/A | **✅** |
| Typed success values preserved | ❌ | N/A | ❌ | N/A | N/A | **✅ — all 2–7 values returned** |
| Max arity (number of results combined) | Unlimited (`IEnumerable`) | N/A | Unlimited | N/A | N/A | **7** |

---

## 9. When to Choose Each Library

| Library | Best fit |
|---|---|
| **CSharpFunctionalExtensions** | Full DDD toolkit needed (`ValueObject`, `Entity`, `Maybe<T>`); `netstandard2.0` required; large established project. |
| **ErrorOr** | Uniform error model with fixed categories (Validation, NotFound, Conflict, …) across the whole app; source generator wanted. |
| **FluentResults** | Rich error objects with metadata, nested causes, or structured logging payloads; success metadata alongside values. |
| **Ardalis.Result** | Clean Architecture / vertical slice apps that want built-in `ToActionResult()` and `ResultStatus` enum; no generic typed errors needed. |
| **OneOf** | More than two distinct outcome branches; comfortable writing manual `Switch`/`Match` without combinators. |
| **FrenchExDev.Net.Result** | Validation returning all field-level errors at once, compatible with `ValidationProblemDetails`; async chaining without intermediate `await`s; explicit construction only; `net10.0` target; typed domain errors. |
