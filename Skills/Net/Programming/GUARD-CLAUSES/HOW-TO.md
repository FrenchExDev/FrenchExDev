# GUARD-CLAUSES — How-To

## 1. Guarding Constructor Parameters

Use `Guard.Against` in constructors and assign the validated value back to the field in one expression:

```csharp
public sealed class OrderService
{
    private readonly IClock _clock;
    private readonly IOrderRepository _repository;
    private readonly ILogger<OrderService> _logger;

    public OrderService(IClock clock, IOrderRepository repository, ILogger<OrderService> logger)
    {
        _clock      = Guard.Against.Null(clock);
        _repository = Guard.Against.Null(repository);
        _logger     = Guard.Against.Null(logger);
    }
}
```

`[CallerArgumentExpression]` captures the parameter name automatically — no `nameof(clock)` boilerplate.

## 2. Guarding Method Parameters

```csharp
public void Process(string customerName, int quantity, DateTimeOffset deliveryDate)
{
    var name  = Guard.Against.NullOrWhiteSpace(customerName);
    var qty   = Guard.Against.NegativeOrZero(quantity);
    var date  = Guard.Against.OutOfRange(deliveryDate,
        min: _clock.UtcNow,
        max: _clock.UtcNow.AddYears(1));

    // ... business logic uses validated values
}
```

## 3. Guarding Inside a Result Pipeline

When you're already inside a `Result`-returning method, use `Guard.ToResult` so the failure composes:

```csharp
public Result<User> CreateUser(string? name, int? age)
{
    var nameResult = Guard.ToResult.NullOrWhiteSpace(name, "Name is required");
    var ageResult  = Guard.ToResult.OutOfRange(age ?? -1, 0, 150, "Age must be 0..150");

    return Result.Combine(nameResult, ageResult)
        .Then(t => Result<User>.Success(new User(t.Item1, t.Item2)));
}
```

Throwing inside a Result-returning method breaks the pipeline contract. Always use `ToResult` here.

## 4. Custom Predicates

For one-off checks, both modes provide `InvalidInput`:

```csharp
// Throwing
var email = Guard.Against.InvalidInput(input,
    s => s.Contains('@'),
    "Email must contain '@'");

// Result-returning
Result<string> email = Guard.ToResult.InvalidInput(input,
    s => s.Contains('@'),
    "Email must contain '@'");
```

## 5. Range Checks

```csharp
// Inclusive [min, max]
var percent = Guard.Against.OutOfRange(p, 0, 100);

// Negative or zero (positive integers)
var quantity = Guard.Against.NegativeOrZero(qty);

// Just negative (allows zero)
var balance = Guard.Against.Negative(amount);
```

## 6. String Length Limits

```csharp
public void StoreNote(string note)
{
    var validated = Guard.Against.LengthExceeded(note, maxLength: 4000);
    _repo.Save(validated);
}
```

## 7. GUIDs

```csharp
public Order Find(Guid orderId)
{
    var id = Guard.Against.EmptyGuid(orderId);
    return _repo.Find(id);
}
```

## 8. Defined Enum Values

Cast-from-int can produce undefined enum values. Guard them at the boundary:

```csharp
public void Update(OrderStatus status)
{
    Guard.Against.UndefinedEnum(status);
    // safe to use status in switch expressions
}
```

## 9. Invariants With `Ensure`

Use `Guard.Ensure` (or equivalent) for postconditions and invariants — failures throw `InvalidOperationException`, not `ArgumentException`:

```csharp
public Order LoadById(Guid id)
{
    var order = _repo.Find(id);
    Guard.Ensure.NotNull(order, $"Order {id} unexpectedly missing");
    return order;
}
```

## 10. Testing Guards

For each new guard, write tests for:

- Happy path — guard returns the input unchanged.
- Each rejection mode — guard throws/returns failure with the correct exception/error type.
- Parameter name capture — `ArgumentNullException.ParamName` matches the call-site expression.

```csharp
[Fact]
public void Null_WithNonNullValue_ReturnsValue()
{
    var input = "hello";
    var result = Guard.Against.Null(input);
    Assert.Same(input, result);
}

[Fact]
public void Null_WithNullValue_ThrowsArgumentNullExceptionWithCallerName()
{
    string? input = null;
    var ex = Assert.Throws<ArgumentNullException>(() => Guard.Against.Null(input));
    Assert.Equal("input", ex.ParamName);
}
```

## What NOT To Do

- **Don't throw inside a `Result`-returning method.** Use `Guard.ToResult` instead.
- **Don't put domain rules in guards.** "Customer must be active" is a domain rule, not a structural precondition. Use a `Result<T>`-returning validator.
- **Don't catch the exceptions thrown by `Against.*`.** They indicate programming errors at boundaries; let them propagate to the global exception handler.
- **Don't re-validate inside business logic.** Once a value passed its guard, trust it.

## Anchor Package

[`Net/FrenchExDev/Guard/`](../../../Net/FrenchExDev/Guard/) — full reference implementation.
