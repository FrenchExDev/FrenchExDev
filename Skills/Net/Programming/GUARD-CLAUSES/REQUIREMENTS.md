# GUARD-CLAUSES — Requirements

## Entry Point

- [ ] Single static `Guard` class as the public entry point.
- [ ] At least two modes exposed as sub-properties: `Against` (throws) and `ToResult` (returns `Result<T>`).
- [ ] Optional third mode `Ensure` for invariants — throws `InvalidOperationException`.

## Guard Set (Mirror In Both Modes)

- [ ] `Null<T>(value)` for reference types.
- [ ] `NullValue<T>(value)` for nullable value types (`T? where T : struct`).
- [ ] `NullOrEmpty(string)` and `NullOrEmpty<T>(IReadOnlyList<T>)`.
- [ ] `NullOrWhiteSpace(string)`.
- [ ] `OutOfRange<T>(value, min, max)` where `T : IComparable<T>`.
- [ ] `Negative<T>` and `NegativeOrZero<T>`.
- [ ] `Default<T>(value)` for `struct` defaults.
- [ ] `EmptyGuid(Guid)`.
- [ ] `UndefinedEnum<T>(value)` for `T : struct, Enum`.
- [ ] `LengthExceeded(string, maxLength)`.
- [ ] `InvalidInput<T>(value, predicate, message)`.

## Behavior Requirements

- [ ] Every guard returns the validated value (enables inline assignment).
- [ ] Throwing-mode methods use `[CallerArgumentExpression(nameof(value))] string? paramName = null` for auto parameter-name capture.
- [ ] Throwing mode uses standard .NET exceptions: `ArgumentNullException`, `ArgumentException`, `ArgumentOutOfRangeException`.
- [ ] `ToResult` mode wraps the failure in `Result<T>.Failure(new ValidationResult(...))`.
- [ ] `ToResult` mode accepts an optional `string? errorMessage` for caller customization.
- [ ] `Ensure` mode throws `InvalidOperationException`, never `ArgumentException`.

## Cross-Cutting Rules

- [ ] Guards perform **structural** checks only — null, range, length, defined-enum.
- [ ] Guards do NOT perform domain rule validation (those belong in `Result<T>`-returning validators).
- [ ] Guards do NOT catch exceptions — they raise them.
- [ ] Guards must run in both Debug and Release builds (never gated on `Debug.Assert`).

## Multi-Targeting

- [ ] Guards target `netstandard2.0` and `net10.0` so they work in legacy services.
- [ ] On `netstandard2.0`, an internal `CallerArgumentExpressionAttribute` polyfill is provided.

## Testing

- [ ] Each guard has a happy-path test (returns input unchanged).
- [ ] Each guard has a failure test for every rejection mode (`null`, empty, whitespace, out-of-range, etc.).
- [ ] Throwing mode tests assert `ParamName` is captured from the call-site expression.
- [ ] `ToResult` mode tests assert the failure carries the expected error message.

## What MUST NOT Be Done

- [ ] Throwing guards MUST NOT be used inside methods that return `Result<T>` — use `Guard.ToResult` instead.
- [ ] Guards MUST NOT swallow exceptions (e.g. catch and convert to a different type).
- [ ] Guards MUST NOT mutate inputs (e.g. trim a string before returning).
- [ ] Guards MUST NOT log — that's a side effect that obscures pure validation.

## Anchor Package

[`Net/FrenchExDev/Guard/`](../../../Net/FrenchExDev/Guard/) — implements every requirement above.
