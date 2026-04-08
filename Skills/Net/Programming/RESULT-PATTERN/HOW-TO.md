# RESULT-PATTERN — How-To

Practical recipes for using a Result-pattern library in service code, validation, and async pipelines.

## 1. Validate Several Independent Fields

Validate each field independently, then merge with `Combine` so the caller gets *every* error in one pass.

```csharp
Result<string> ValidateName(string? name)
{
    if (string.IsNullOrWhiteSpace(name))
        return Result<string>.Failure(new ValidationResult("Name is required", ["Name"]));
    if (name.Length > 100)
        return Result<string>.Failure(new ValidationResult("Name must be 100 chars or fewer", ["Name"]));
    return Result<string>.Success(name.Trim());
}

Result<int> ValidateAge(int? age)
{
    if (age is null)
        return Result<int>.Failure(new ValidationResult("Age is required", ["Age"]));
    if (age < 0 || age > 150)
        return Result<int>.Failure(new ValidationResult("Age must be 0..150", ["Age"]));
    return Result<int>.Success(age.Value);
}

Result<User> CreateUser(string? name, int? age)
    => Result.Combine(ValidateName(name), ValidateAge(age))
             .Then(t => Result<User>.Success(new User(t.Item1, t.Item2)));
```

When any validator fails, `Combine` merges all `ValidationResult` entries — never short-circuit individual validators when you want a complete error report.

## 2. Service Layer With Typed Errors

Use `Result<T, TError>` when the error has semantic meaning beyond a validation message — distinguishing "not found" from "forbidden", for example.

```csharp
public enum UserError { NotFound, Forbidden, DatabaseUnavailable }

public sealed class UserService(IUserRepository repo, ICurrentUser currentUser)
{
    public async Task<Result<User, UserError>> GetAsync(Guid id)
    {
        if (!currentUser.CanRead(id))
            return Result<User, UserError>.Failure(UserError.Forbidden);

        User? user;
        try { user = await repo.FindAsync(id); }
        catch (DbException) { return Result<User, UserError>.Failure(UserError.DatabaseUnavailable); }

        return user is null
            ? Result<User, UserError>.Failure(UserError.NotFound)
            : Result<User, UserError>.Success(user);
    }
}
```

## 3. Wrapping a Throwing Third-Party Library

Use `FromTry` as a clean boundary between exception-based APIs and your domain.

```csharp
// Only catches JsonException; all other exceptions propagate
Result<OrderDto, JsonException> parsed = Result.FromTry<OrderDto, JsonException>(
    () => JsonSerializer.Deserialize<OrderDto>(json)!);

Result<byte[], HttpRequestException> bytes =
    await Result.FromTryAsync<byte[], HttpRequestException>(
        () => httpClient.GetByteArrayAsync(url));
```

Never use `FromTry<T, Exception>` to catch everything — that defeats the boundary purpose and re-introduces the silent-swallow problem.

## 4. Multi-Step Async Pipeline With No Intermediate `await`

```csharp
public async Task<Result<InvoiceDto>> ProcessOrderAsync(Guid orderId, Guid userId)
    => await GetUserAsync(userId)
        .ThenAsync(user  => GetOrderAsync(orderId, user))
        .ThenAsync(order => ReserveStockAsync(order))
        .ThenAsync(order => ChargePaymentAsync(order))
        .MapAsync (order => BuildInvoiceDto(order))
        .TapAsync (dto   => SendConfirmationEmailAsync(dto))
        .TapErrorAsync(errs => LogFailureAsync(errs));
```

Each step only runs if the previous succeeded. Any failure short-circuits the rest and propagates the error unchanged.

## 5. Layered `Ensure` Guards

`Ensure` adds a predicate-based guard to a value already wrapped in a `Result<T>`. Guards short-circuit on the first failing predicate.

```csharp
Result<Order> ValidateOrder(Order order)
    => Result<Order>.Success(order)
        .Ensure(o => o.Items.Count > 0,
            () => new ValidationResult("Order must contain at least one item", ["Items"]))
        .Ensure(o => o.TotalAmount > 0,
            () => new ValidationResult("Order total must be positive", ["TotalAmount"]))
        .Ensure(o => o.DeliveryDate > DateOnly.FromDateTime(DateTime.UtcNow),
            o => new ValidationResult(
                $"Delivery date {o.DeliveryDate} must be in the future",
                ["DeliveryDate"]));
```

## 6. Validating Every Item In a Collection

```csharp
Result<IReadOnlyList<LineItem>> ValidateLines(IEnumerable<LineItemDto> dtos)
{
    var results = dtos.Select(ValidateLine).ToList();
    var errors = results.Where(r => r.IsFailure).SelectMany(r => r.ValidationResults).ToList();

    if (errors.Count > 0)
        return Result<IReadOnlyList<LineItem>>.Failure(
            new ValidationResult($"{errors.Count} item(s) invalid", ["Items"]));

    return Result<IReadOnlyList<LineItem>>.Success(
        results.Select(r => r.ValueOrThrow()).ToList());
}
```

## 7. Mapping Result To `IActionResult`

```csharp
[HttpGet("{id:guid}")]
public async Task<IActionResult> Get(Guid id)
    => (await _userService.GetAsync(id)).Match(
        onSuccess: user  => Ok(user),
        onFailure: error => error switch
        {
            UserError.NotFound            => NotFound(),
            UserError.Forbidden           => Forbid(),
            UserError.DatabaseUnavailable => StatusCode(503),
            _                             => StatusCode(500)
        });
```

For validation results, map directly into `ValidationProblemDetails`:

```csharp
return result.Match(
    onSuccess: u    => Ok(u),
    onFailure: errs => UnprocessableEntity(new ValidationProblemDetails(
        errs.GroupBy(e => e.MemberNames.FirstOrDefault() ?? string.Empty)
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage!).ToArray()))));
```

## 8. `Recover` At Pipeline Boundaries

`Recover` is most useful at the outermost layer where you must guarantee a non-failure result.

```csharp
Task<Result<PriceList>> GetPricesAsync(Guid catalogId)
    => _priceRepository.FindAsync(catalogId)
        .RecoverAsync(_ => _cache.GetDefaultPriceList());
```

## 9. Parallel Async Operations + `Combine`

```csharp
public async Task<Result<(User, Inventory)>> LoadPageDataAsync(Guid userId, Guid productId)
{
    var userTask      = GetUserAsync(userId);
    var inventoryTask = GetInventoryAsync(productId);
    await Task.WhenAll(userTask, inventoryTask);
    return Result.Combine(await userTask, await inventoryTask);
}
```

## 10. Unit Testing — Both Branches

```csharp
[Fact]
public void ValidateName_Empty_ReturnsFailureWithMemberName()
{
    var result = ValidateName("");
    Assert.True(result.IsFailure);
    Assert.Single(result.ValidationResults);
    Assert.Equal("Name", result.ValidationResults[0].MemberNames.Single());
}

[Fact]
public async Task Pipeline_FailsAtFirstStep_DoesNotRunSubsequentSteps()
{
    bool secondStepRan = false;

    var result = await Task.FromResult(
        Result<int>.Failure(new ValidationResult("Bad input", ["Input"])))
        .ThenAsync(n =>
        {
            secondStepRan = true;
            return Task.FromResult(Result<int>.Success(n * 2));
        });

    Assert.True(result.IsFailure);
    Assert.False(secondStepRan);
}
```

Always test that failure short-circuits — the pipeline must not run subsequent steps after a failure.

## Anchor Package

[`Net/FrenchExDev/Result/`](../../../Net/FrenchExDev/Result/) — see `doc/HOW-TO.md` for additional recipes.
