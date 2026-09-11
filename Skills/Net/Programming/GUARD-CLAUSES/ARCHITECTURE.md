# GUARD-CLAUSES — Architecture

How to structure a guard-clause library so the same checks are available in both throwing and Result-returning forms.

## Single Entry Point, Multiple Modes

Expose a single static `Guard` class with sub-properties for each failure mode:

```csharp
public static class Guard
{
    public static GuardAgainst Against { get; } = new();   // throws
    public static GuardToResult ToResult { get; } = new(); // returns Result<T>
    public static GuardEnsure Ensure { get; } = new();     // invariants
}
```

Why a single entry point:

- One `using` brings everything into scope.
- IntelliSense surfaces all available modes from one place.
- The mode (throw / Result) is visible at the call site: `Guard.Against.Null(x)` vs `Guard.ToResult.Null(x)`.

## Mirror Sets

Each mode is a separate class with the **same set of guard names**:

| Method | `Against` (throws) | `ToResult` (returns) |
|---|---|---|
| `Null<T>` | `ArgumentNullException` | `Result<T>.Failure(...)` |
| `NullOrEmpty(string)` | `ArgumentNullException` / `ArgumentException` | `Result<string>.Failure(...)` |
| `NullOrWhiteSpace` | `ArgumentNullException` / `ArgumentException` | `Result<string>.Failure(...)` |
| `OutOfRange<T>(value, min, max)` | `ArgumentOutOfRangeException` | `Result<T>.Failure(...)` |
| `Negative<T>` | `ArgumentOutOfRangeException` | `Result<T>.Failure(...)` |
| `NegativeOrZero<T>` | `ArgumentOutOfRangeException` | `Result<T>.Failure(...)` |
| `Default<T>` | `ArgumentException` | `Result<T>.Failure(...)` |
| `EmptyGuid` | `ArgumentException` | `Result<Guid>.Failure(...)` |
| `UndefinedEnum<T>` | `ArgumentOutOfRangeException` | (optional) |
| `LengthExceeded` | `ArgumentException` | (optional) |
| `InvalidInput<T>(predicate)` | `ArgumentException` | `Result<T>.Failure(...)` |

Both modes share the same canonical names. Switching between throw and Result is a one-word change at the call site.

## Always Return The Validated Value

Every guard returns the value it validated, even on the throwing path:

```csharp
public T Null<T>(T? value, ...) where T : class
    => value ?? throw new ArgumentNullException(paramName);
```

This enables the inline-assignment pattern (`_clock = Guard.Against.Null(clock);`), eliminating the imperative `if (x is null) throw; _x = x;` sequence.

## `[CallerArgumentExpression]` For Auto Parameter Names

```csharp
public string NullOrEmpty(
    string? value,
    [CallerArgumentExpression(nameof(value))] string? paramName = null)
{
    if (value is null) throw new ArgumentNullException(paramName);
    if (value.Length == 0) throw new ArgumentException("Value cannot be empty.", paramName);
    return value;
}
```

The compiler captures the call-site expression. `Guard.Against.NullOrEmpty(customerName)` produces `ArgumentException("...", "customerName")` automatically.

## Polyfill For `netstandard2.0`

`[CallerArgumentExpression]` requires .NET 5+ at runtime. To support `netstandard2.0`, ship an internal polyfill:

```csharp
// internal — only when targeting netstandard2.0
namespace System.Runtime.CompilerServices
{
    [AttributeUsage(AttributeTargets.Parameter)]
    internal sealed class CallerArgumentExpressionAttribute : Attribute
    {
        public CallerArgumentExpressionAttribute(string parameterName)
            => ParameterName = parameterName;
        public string ParameterName { get; }
    }
}
```

Modern compilers honor the attribute regardless of where it's defined.

## Result-Mode API Shape

The `ToResult` mode takes an optional error message instead of using `[CallerArgumentExpression]` (the result will be matched against `ValidationResult.MemberNames`, not against an exception parameter name):

```csharp
public Result<T> Null<T>(T? value, string? errorMessage = null) where T : class
    => value is not null
        ? Result<T>.Success(value)
        : Result<T>.Failure(new ValidationResult(errorMessage ?? "Value must not be null."));
```

## `Ensure` Mode — Invariants and Postconditions

A third mode (`Guard.Ensure`) is for invariants and postconditions inside a method, not parameter checks. It throws `InvalidOperationException` rather than `ArgumentException`:

```csharp
Guard.Ensure.True(_state == State.Ready, "Service must be in Ready state");
Guard.Ensure.NotNull(result, "Internal call returned null unexpectedly");
```

The semantic distinction matters: `ArgumentException` says "the caller passed bad data"; `InvalidOperationException` says "the object is in a state that cannot satisfy this call." Different exception types let callers handle them differently.

## What Guards Are Not

- **Not domain validators.** Guards check structure (not null, not negative). Domain validators check business rules (active customer, valid date range for the current month). Domain validation belongs in `Result<T>`-returning validators that produce `ValidationResult` errors with member names.
- **Not assertions.** Asserts (`Debug.Assert`) compile out in Release. Guards always run.
- **Not data validation pipelines.** Guards check one input at a time and short-circuit on the first failure. Multi-field validation belongs in `Result.Combine`.

## Anchor Package

[`Net/FrenchExDev/Guard/`](../../../Net/FrenchExDev/Guard/) — three modes, mirrored API surface, `[CallerArgumentExpression]` for auto parameter names, polyfill for `netstandard2.0`.
