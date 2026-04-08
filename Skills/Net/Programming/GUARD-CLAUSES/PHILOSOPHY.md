# GUARD-CLAUSES — Philosophy

Guard clauses are the first line of defense at every API boundary. They make preconditions explicit, fail fast with informative messages, and reject invalid input before any business logic runs.

## Validate At The Boundary, Trust Inside

Every public method, every constructor, every controller action has a boundary. On one side: untrusted input that may be null, empty, negative, or out of range. On the other side: business logic that assumes its inputs are valid.

A guard clause sits at that boundary. It checks one precondition and either lets the value through or rejects it. Once a value passes its guard, the rest of the method can use it without re-checking.

Without guards, defensive null-checks, range checks, and "is this even a real value?" tests creep into business code, mixing what the method does with what it tolerates.

## Two Failure Modes, Two APIs

A guard can fail in two ways depending on where it sits in the system:

- **Throw an exception** at *system boundaries* — public API entry points, controller actions, constructors. Callers in this layer typically cannot recover; they want to know "you passed garbage" loudly. `ArgumentNullException`, `ArgumentException`, `ArgumentOutOfRangeException` are the standard .NET exceptions for this.

- **Return a `Result<T>`** inside *functional pipelines* — service layer, validators, anything composed with `Map`/`Bind`/`Then`. Throwing here would break the pipeline; the caller wants to compose the failure into the rest of the chain.

The same set of guard names (`Null`, `NullOrEmpty`, `OutOfRange`, `Negative`, ...) should exist in both flavors. The developer chooses the API based on whether they are at a hard boundary or inside a soft pipeline.

## Inline And Returning

Guards return the validated value. This lets them be used inline in assignments, constructors, and chained expressions:

```csharp
public OrderService(IClock clock, ILogger logger)
{
    _clock  = Guard.Against.Null(clock);
    _logger = Guard.Against.Null(logger);
}

public Order Place(string customerName, int quantity)
{
    var name = Guard.Against.NullOrWhiteSpace(customerName);
    var qty  = Guard.Against.NegativeOrZero(quantity);
    return new Order(name, qty);
}
```

Compare to the imperative form:

```csharp
if (clock is null) throw new ArgumentNullException(nameof(clock));
if (logger is null) throw new ArgumentNullException(nameof(logger));
_clock = clock;
_logger = logger;
```

The guard form is shorter, names the parameter automatically via `[CallerArgumentExpression]`, and reads as "this field must not be null."

## `[CallerArgumentExpression]` For Free Parameter Names

Modern .NET supports `[CallerArgumentExpression(nameof(value))]` on a `string? paramName = null` parameter. The compiler captures the expression text passed at the call site:

```csharp
public T Null<T>(
    T? value,
    [CallerArgumentExpression(nameof(value))] string? paramName = null)
    where T : class
    => value ?? throw new ArgumentNullException(paramName);
```

Calling `Guard.Against.Null(myService)` produces `ArgumentNullException("myService")` automatically. No `nameof()` boilerplate at every call site.

## Don't Combine Guard And Domain Validation

Guards check **structural preconditions**: not null, not empty, not negative, in range, defined enum value. They do **not** check business rules: "the customer must be active", "the cart must contain at least one item that ships internationally."

Business rules belong in domain validators that return `Result<T>` and produce rich `ValidationResult` errors. Mixing them muddies the layering — guards fail fast and brutally; domain rules fail with structured, user-facing messages.

## Anchor Package

[`Net/FrenchExDev/Guard/`](../../../Net/FrenchExDev/Guard/) — `Guard.Against` (throws), `Guard.ToResult` (returns `Result<T>`), `Guard.Ensure` (invariant assertions). Same names, two failure modes, one boundary discipline.
