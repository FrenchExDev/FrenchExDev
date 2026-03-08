# HOW-TO — FrenchExDev.Net.Result

Practical recipes for common and advanced patterns.

---

## 1. Basic validation pipeline

Validate several independent fields, collect all errors at once, and either build the domain object or return every error to the caller.

```csharp
using System.ComponentModel.DataAnnotations;
using FrenchExDev.Net.Result;

// --- validators returning Result<T> ---

Result<string> ValidateName(string? name)
{
    if (string.IsNullOrWhiteSpace(name))
        return Result<string>.Failure(new ValidationResult("Name is required", ["Name"]));
    if (name.Length > 100)
        return Result<string>.Failure(new ValidationResult("Name must be 100 characters or fewer", ["Name"]));
    return Result<string>.Success(name.Trim());
}

Result<int> ValidateAge(int? age)
{
    if (age is null)
        return Result<int>.Failure(new ValidationResult("Age is required", ["Age"]));
    if (age < 0 || age > 150)
        return Result<int>.Failure(new ValidationResult("Age must be between 0 and 150", ["Age"]));
    return Result<int>.Success(age.Value);
}

Result<string> ValidateEmail(string? email)
{
    if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
        return Result<string>.Failure(new ValidationResult("Valid email is required", ["Email"]));
    return Result<string>.Success(email.Trim().ToLowerInvariant());
}

// --- combine + create domain object ---

Result<User> CreateUser(string? name, int? age, string? email)
    => Result.Combine(ValidateName(name), ValidateAge(age), ValidateEmail(email))
             .Then(t => Result<User>.Success(new User(t.Item1, t.Item2, t.Item3)));

// --- consume ---

var result = CreateUser("Alice", 30, "alice@example.com");

result.Match(
    onSuccess: user => Console.WriteLine($"Created user {user.Name}"),
    onFailure: errs =>
    {
        foreach (var e in errs)
            Console.WriteLine($"[{string.Join(", ", e.MemberNames)}] {e.ErrorMessage}");
    });
```

When any validator fails, `Combine` merges all `ValidationResult` entries so the caller gets a complete list of problems in a single pass.

---

## 2. Service layer — returning typed errors

Use `Result<T, TError>` when the error has semantic meaning beyond a validation message, e.g. distinguishing "not found" from "forbidden".

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

        if (user is null)
            return Result<User, UserError>.Failure(UserError.NotFound);

        return Result<User, UserError>.Success(user);
    }
}

// --- controller ---

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

---

## 3. Wrapping a throwing third-party library

`Result.FromTry` creates a clean boundary between exception-based APIs and your domain.

```csharp
// Only catches JsonException; all other exceptions propagate
Result<OrderDto, JsonException> parsed = Result.FromTry<OrderDto, JsonException>(
    () => JsonSerializer.Deserialize<OrderDto>(json)!);

// Async variant
Result<byte[], HttpRequestException> bytes =
    await Result.FromTryAsync<byte[], HttpRequestException>(
        () => httpClient.GetByteArrayAsync(url));

// Continue the pipeline
Result<Order> order = parsed
    .Map(dto => dto.ToDomain())
    .Match(
        onSuccess: o  => Result<Order>.Success(o),
        onFailure: ex => Result<Order>.Failure(
            new ValidationResult($"Could not parse order: {ex.Message}", ["Body"])));
```

---

## 4. Multi-step async pipeline without intermediate awaits

Chain async operations from start to finish using the `Task<Result<T>>` pipeline overloads.

```csharp
public async Task<Result<InvoiceDto>> ProcessOrderAsync(Guid orderId, Guid userId)
    => await GetUserAsync(userId)                           // Task<Result<User>>
        .ThenAsync(user  => GetOrderAsync(orderId, user))   // Task<Result<Order>>
        .ThenAsync(order => ReserveStockAsync(order))       // Task<Result<Order>>
        .ThenAsync(order => ChargePaymentAsync(order))      // Task<Result<Order>>
        .MapAsync (order => BuildInvoiceDto(order))         // Task<Result<InvoiceDto>>
        .TapAsync (dto   => SendConfirmationEmailAsync(dto))// side-effect, passes through
        .TapErrorAsync(errs => LogFailureAsync(errs));      // side-effect on error, passes through

// Each step only runs if the previous one succeeded.
// Any failure short-circuits the rest and propagates errors unchanged.
```

---

## 5. Mixing sync and async steps in one pipeline

The `Task<Result<T>>` overloads accept both synchronous and asynchronous mappers/binders.

```csharp
Result<Order> order = await GetOrderAsync(id)                              // Task<Result<Order>>
    .MapAsync(o => o with { Status = OrderStatus.Confirmed })              // sync mapper
    .ThenAsync(o => SaveOrderAsync(o))                                     // async binder
    .TapAsync(o => logger.LogInformation("Saved order {Id}", o.Id));      // sync action
```

---

## 6. Validate then enrich — `Ensure` chaining

Use `Ensure` to layer additional constraints on a value already wrapped in a `Result<T>`.

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

All three guards evaluate in sequence. The first failing predicate short-circuits the remaining `Ensure` calls — subsequent predicates are not executed on an already-failed result.

---

## 7. Collecting errors across a list

Validate every item in a collection and aggregate all errors.

```csharp
Result<IReadOnlyList<LineItem>> ValidateLines(IEnumerable<LineItemDto> dtos)
{
    var results = dtos.Select((dto, i) => ValidateLine(dto, i)).ToList();

    var errors = results
        .Where(r => r.IsFailure)
        .SelectMany(r => r.ValidationResults)
        .ToList();

    if (errors.Count > 0)
        return Result<IReadOnlyList<LineItem>>.Failure(
            new ValidationResult($"{errors.Count} line item(s) are invalid", ["Items"]));

    return Result<IReadOnlyList<LineItem>>.Success(
        results.Select(r => r.ValueOrThrow()).ToList());
}

Result<LineItem> ValidateLine(LineItemDto dto, int index)
    => Result<LineItem>.Success(new LineItem(dto))
        .Ensure(li => li.Quantity > 0,
            () => new ValidationResult($"Item {index}: quantity must be positive", [$"Items[{index}].Quantity"]))
        .Ensure(li => li.UnitPrice >= 0,
            () => new ValidationResult($"Item {index}: unit price cannot be negative", [$"Items[{index}].UnitPrice"]));
```

---

## 8. Converting between `Result<T>` and `Result<T, TError>`

```csharp
// Result<T> → Result<T, ValidationException>
Result<User, ValidationException> ToTypedError(Result<User> r)
    => r.Match(
        onSuccess: u    => Result<User, ValidationException>.Success(u),
        onFailure: errs =>
        {
            var messages = string.Join("; ", errs.Select(e => e.ErrorMessage));
            return Result<User, ValidationException>.Failure(new ValidationException(messages));
        });

// Result<T, TError> → Result<T>
Result<User> ToValidationResult(Result<User, DomainException> r)
    => r.Match(
        onSuccess: u  => Result<User>.Success(u),
        onFailure: ex => Result<User>.Failure(
            new ValidationResult(ex.Message, ex.MemberNames)));
```

---

## 9. Layered application — full vertical slice

A realistic example: HTTP request → command handler → domain service → repository → response.

```csharp
// --- Command ---
public record CreateProductCommand(string Name, decimal Price, int Stock);

// --- Validation ---
public static class CreateProductValidator
{
    public static Result<CreateProductCommand> Validate(CreateProductCommand cmd)
        => Result.Combine(
               ValidateName(cmd.Name),
               ValidatePrice(cmd.Price),
               ValidateStock(cmd.Stock))
           .Then(t => Result<CreateProductCommand>.Success(
               cmd with { Name = t.Item1 }));

    static Result<string>  ValidateName(string n)  =>
        string.IsNullOrWhiteSpace(n)
            ? Result<string>.Failure(new ValidationResult("Name required", ["Name"]))
            : Result<string>.Success(n.Trim());

    static Result<decimal> ValidatePrice(decimal p) =>
        p <= 0
            ? Result<decimal>.Failure(new ValidationResult("Price must be positive", ["Price"]))
            : Result<decimal>.Success(p);

    static Result<int>     ValidateStock(int s)     =>
        s < 0
            ? Result<int>.Failure(new ValidationResult("Stock cannot be negative", ["Stock"]))
            : Result<int>.Success(s);
}

// --- Domain service ---
public sealed class ProductService(IProductRepository repo, IEventBus bus)
{
    public async Task<Result<Product>> CreateAsync(CreateProductCommand cmd)
        => await CreateProductValidator.Validate(cmd)    // Result<CreateProductCommand>
            .ThenAsync(c => PersistAsync(c))             // Task<Result<Product>>
            .TapAsync(p  => bus.PublishAsync(new ProductCreated(p.Id)));

    async Task<Result<Product>> PersistAsync(CreateProductCommand cmd)
    {
        try
        {
            var product = await repo.CreateAsync(
                new Product(cmd.Name, cmd.Price, cmd.Stock));
            return Result<Product>.Success(product);
        }
        catch (DuplicateKeyException)
        {
            return Result<Product>.Failure(
                new ValidationResult(
                    $"A product named '{cmd.Name}' already exists", ["Name"]));
        }
    }
}

// --- API controller ---
[ApiController, Route("products")]
public class ProductsController(ProductService service) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(CreateProductRequest request)
    {
        var cmd = new CreateProductCommand(request.Name, request.Price, request.Stock);

        return (await service.CreateAsync(cmd)).Match(
            onSuccess: product => CreatedAtAction(nameof(Get), new { id = product.Id }, product),
            onFailure: errs    => UnprocessableEntity(new ValidationProblemDetails(
                errs.GroupBy(e => e.MemberNames.FirstOrDefault() ?? string.Empty)
                    .ToDictionary(
                        g => g.Key,
                        g => g.Select(e => e.ErrorMessage!).ToArray()))));
    }
}
```

---

## 10. `Recover` — providing defaults at pipeline boundaries

`Recover` is most useful at the outermost layer of a pipeline where you must guarantee a non-failure result.

```csharp
// Return a cached fallback when the live lookup fails
Task<Result<PriceList>> GetPricesAsync(Guid catalogId)
    => _priceRepository.FindAsync(catalogId)           // Task<Result<PriceList>>
        .RecoverAsync(_ => _cache.GetDefaultPriceList());

// Log the error and return an empty list so the UI always has something to render
IReadOnlyList<Notification> notifications =
    await _notificationService.GetAsync(userId)
        .TapErrorAsync(errs => _logger.LogWarning(
            "Could not load notifications: {Errors}",
            string.Join("; ", errs.Select(e => e.ErrorMessage))))
        .RecoverAsync(_ => Task.FromResult(Array.Empty<Notification>()))
        .MatchAsync(arr => (IReadOnlyList<Notification>)arr, _ => []);
```

---

## 11. `Result` (no value) — void commands

Use the non-generic `Result` for commands that have no meaningful return value.

```csharp
public async Task<Result> SendEmailAsync(EmailMessage message)
{
    try
    {
        await _smtp.SendAsync(message);
        return Result.Success();
    }
    catch (SmtpException ex)
    {
        _logger.LogError(ex, "Failed to send email to {To}", message.To);
        return Result.Failure();
    }
}

// Guard a subsequent step on the void result
public async Task<Result<Order>> PlaceOrderAsync(Cart cart)
{
    var notified = await SendEmailAsync(BuildConfirmationEmail(cart));

    return notified.Match(
        onSuccess: () => BuildOrder(cart),
        onFailure: ()  => Result<Order>.Failure(
            new ValidationResult("Could not send confirmation email", ["Email"])));
}
```

---

## 12. Unit testing results

Assert on both the happy path and all failure branches.

```csharp
// Happy path
[Fact]
public void ValidateName_Valid_ReturnsSuccess()
{
    var result = ValidateName("Alice");
    Assert.True(result.IsSuccess);
    Assert.Equal("Alice", result.ValueOrThrow());
}

// Failure — check error content
[Fact]
public void ValidateName_Empty_ReturnsFailureWithMemberName()
{
    var result = ValidateName("");
    Assert.True(result.IsFailure);
    Assert.Single(result.ValidationResults);
    Assert.Equal("Name", result.ValidationResults[0].MemberNames.Single());
}

// Combine — all errors collected
[Fact]
public void Combine_BothFail_MergesErrors()
{
    var r1 = Result<string>.Failure(new ValidationResult("Error A", ["FieldA"]));
    var r2 = Result<int>.Failure(new ValidationResult("Error B", ["FieldB"]));

    var combined = Result.Combine(r1, r2);

    Assert.True(combined.IsFailure);
    Assert.Equal(2, combined.ValidationResults.Count);
    Assert.Contains(combined.ValidationResults, e => e.MemberNames.Contains("FieldA"));
    Assert.Contains(combined.ValidationResults, e => e.MemberNames.Contains("FieldB"));
}

// Async pipeline — failure short-circuits subsequent steps
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

// Ensure — first failing predicate wins
[Fact]
public void Ensure_FirstPredicateFails_SecondPredicateNotEvaluated()
{
    bool secondRan = false;

    var result = Result<int>.Success(-5)
        .Ensure(n => n > 0, () => new ValidationResult("Must be positive", ["Value"]))
        .Ensure(n => { secondRan = true; return n < 100; },
            () => new ValidationResult("Must be less than 100", ["Value"]));

    Assert.True(result.IsFailure);
    Assert.False(secondRan);
}
```

---

## 13. Implicit unwrap at API boundaries — `ValueOrElse`

When you must produce a plain value at the system edge, `ValueOrElse` is concise and avoids a `Match` call.

```csharp
// Returns the DTO or a sentinel "not found" DTO — no branching at call site
UserDto dto = (await _userService.GetAsync(id))
    .ValueOrElse(err => err switch
    {
        UserError.NotFound  => UserDto.NotFound,
        UserError.Forbidden => UserDto.Forbidden,
        _                   => UserDto.Error
    });
```

---

## 14. Branching on a specific error without exiting the pipeline

Use `TapError` + `Recover` together to handle one specific failure case while letting others propagate.

```csharp
Result<Config> config = await _configService.LoadAsync(tenantId)
    .TapErrorAsync(errs =>
        _logger.LogWarning("Config load failed: {Msg}", errs[0].ErrorMessage))
    .RecoverAsync(errs =>
        // Only recover from "not found"; re-surface other errors
        errs.Any(e => e.ErrorMessage == "Config not found")
            ? Task.FromResult(Config.Default)
            : Task.FromException<Config>(
                new InvalidOperationException(errs[0].ErrorMessage)));
```

---

## 15. Parallel async operations with `Task.WhenAll` + `Combine`

Run independent async operations in parallel, then combine their results.

```csharp
public async Task<Result<(User, Inventory)>> LoadPageDataAsync(Guid userId, Guid productId)
{
    // Run both requests in parallel
    var userTask      = GetUserAsync(userId);
    var inventoryTask = GetInventoryAsync(productId);

    await Task.WhenAll(userTask, inventoryTask);

    // Combine after both have completed
    return Result.Combine(await userTask, await inventoryTask);
}

// Consume
var result = await LoadPageDataAsync(userId, productId);

return result.Match(
    onSuccess: t    => RenderPage(t.Item1, t.Item2),
    onFailure: errs => RenderError(errs));
```
