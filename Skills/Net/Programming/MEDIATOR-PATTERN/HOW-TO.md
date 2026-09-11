# MEDIATOR-PATTERN — How-To

## 1. Defining Requests

```csharp
// Query (read, no side effects)
public sealed record GetUserQuery(int UserId) : IQuery<User>;

// Command (write, has side effects)
public sealed record CreateUserCommand(string Name, string Email) : ICommand<Result<int>>;

// Generic request when CQRS distinction doesn't matter
public sealed record PingRequest() : IRequest<string>;
```

## 2. Defining Handlers

```csharp
public sealed class GetUserHandler(IUserRepository repo) : IRequestHandler<GetUserQuery, User>
{
    public Task<User> HandleAsync(GetUserQuery request, CancellationToken ct)
        => repo.GetByIdAsync(request.UserId, ct);
}

public sealed class CreateUserHandler(IUserRepository repo) : IRequestHandler<CreateUserCommand, Result<int>>
{
    public async Task<Result<int>> HandleAsync(CreateUserCommand request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return Result<int>.Failure(new ValidationResult("Name required", ["Name"]));
        var id = await repo.CreateAsync(request, ct);
        return Result<int>.Success(id);
    }
}
```

One handler per request type.

## 3. Sending Requests

```csharp
public sealed class UserController(IMediator mediator)
{
    public Task<User> Get(int id, CancellationToken ct)
        => mediator.SendAsync(new GetUserQuery(id), ct);

    public Task<Result<int>> Create(string name, string email, CancellationToken ct)
        => mediator.SendAsync(new CreateUserCommand(name, email), ct);
}
```

## 4. Defining Behaviors

A logging behavior that applies to every request:

```csharp
public sealed class LoggingBehavior<TRequest, TResult>(ILogger<LoggingBehavior<TRequest, TResult>> logger)
    : IBehavior<TRequest, TResult> where TRequest : IRequest<TResult>
{
    public async Task<TResult> HandleAsync(TRequest request, Func<Task<TResult>> next, CancellationToken ct)
    {
        logger.LogInformation("Handling {Request}", typeof(TRequest).Name);
        var sw = Stopwatch.StartNew();
        var result = await next();
        logger.LogInformation("Handled {Request} in {Ms}ms", typeof(TRequest).Name, sw.ElapsedMilliseconds);
        return result;
    }
}
```

A validation behavior that short-circuits on failure:

```csharp
public sealed class ValidationBehavior<TRequest, TResult> : IBehavior<TRequest, TResult>
    where TRequest : IRequest<TResult>
{
    public async Task<TResult> HandleAsync(TRequest request, Func<Task<TResult>> next, CancellationToken ct)
    {
        var errors = Validate(request);
        if (errors.Any())
            return (TResult)(object)Result.Failure(errors);   // short-circuit, never calls next
        return await next();
    }
}
```

## 5. Defining Notifications

```csharp
public sealed record OrderPlaced(Guid OrderId, decimal Total) : INotification;
public sealed record UserCreated(int UserId, string Name) : INotification;
```

Multiple handlers per notification:

```csharp
public sealed class SendWelcomeEmail(IEmailService email) : INotificationHandler<UserCreated>
{
    public Task HandleAsync(UserCreated notification, CancellationToken ct)
        => email.SendWelcomeAsync(notification.Name, ct);
}

public sealed class AuditUserCreation(IAuditLog log) : INotificationHandler<UserCreated>
{
    public Task HandleAsync(UserCreated notification, CancellationToken ct)
        => log.RecordAsync($"User {notification.Name} created", ct);
}
```

## 6. Publishing With Strategy

```csharp
// Sequential — handlers in order, awaited
await mediator.PublishAsync(notification, strategy: PublishStrategy.Sequential);

// Parallel — all handlers concurrent, all awaited
await mediator.PublishAsync(notification, strategy: PublishStrategy.Parallel);

// Fire and forget — concurrent, not awaited, exceptions swallowed
await mediator.PublishAsync(notification, strategy: PublishStrategy.FireAndForget);
```

Choose per notification: ordered for handlers that share state, parallel for independent handlers, fire-and-forget for non-critical side effects (metrics, analytics).

## 7. Using `Result` With The Mediator

```csharp
public sealed record CreateOrderCommand(string Product, int Qty) : ICommand<Result<Guid>>;

public sealed class CreateOrderHandler(IOrderRepository repo) : IRequestHandler<CreateOrderCommand, Result<Guid>>
{
    public async Task<Result<Guid>> HandleAsync(CreateOrderCommand request, CancellationToken ct)
    {
        if (request.Qty <= 0)
            return Result<Guid>.Failure(new ValidationResult("Quantity must be positive"));

        var id = await repo.CreateAsync(request, ct);
        return Result<Guid>.Success(id);
    }
}
```

The caller pattern-matches on the returned `Result<Guid>` instead of catching exceptions.

## 8. Testing With FakeMediator

```csharp
var mediator = new FakeMediator();
mediator.Setup<GetUserQuery, User>(q => new User(q.UserId, "Alice"));

var user = await mediator.SendAsync(new GetUserQuery(42));
Assert.Equal("Alice", user.Name);

Assert.True(mediator.WasSent<GetUserQuery>());
var recorded = mediator.SentRequests.OfType<GetUserQuery>().Single();
Assert.Equal(42, recorded.UserId);

await mediator.PublishAsync(new OrderPlaced(orderId, 99.99m));
Assert.True(mediator.WasPublished<OrderPlaced>());

mediator.Reset();   // clear between test phases
```

Calling `SendAsync` without a matching `Setup` throws — this is a fail-fast guard, not a verification step.

## What NOT To Do

- **Don't ship the library with a built-in mediator runtime.** The runtime is consumer-specific.
- **Don't make handlers throw for expected failures.** Use `Result<T>` return types.
- **Don't put cross-cutting concerns in handlers.** Move logging, validation, caching, transactions into behaviors.
- **Don't configure publish strategy globally.** It's per-call for a reason.
- **Don't mock with frameworks.** Use `FakeMediator`.

## Anchor Package

[`Net/FrenchExDev/Mediator/`](../../../Net/FrenchExDev/Mediator/) — implementation reference.
