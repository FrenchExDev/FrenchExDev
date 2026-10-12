# Mediator -- Developer Guide (HOW-TO)

## Table of Contents

1. [Defining a Request](#1-defining-a-request)
2. [Defining a Handler](#2-defining-a-handler)
3. [Sending Requests](#3-sending-requests)
4. [Defining a Behavior](#4-defining-a-behavior)
5. [Defining Notifications](#5-defining-notifications)
6. [Publishing Notifications](#6-publishing-notifications)
7. [Publish Strategies](#7-publish-strategies)
8. [DI Wiring (Example)](#8-di-wiring-example)
9. [Using Result with Mediator](#9-using-result-with-mediator)
10. [Testing with FakeMediator](#10-testing-with-fakemediator)
11. [Running Tests](#11-running-tests)

---

## 1. Defining a Request

```csharp
// Query (read, no side effects)
public sealed record GetUserQuery(int UserId) : IQuery<User>;

// Command (write, has side effects)
public sealed record CreateUserCommand(string Name, string Email) : ICommand<Result<int>>;

// Generic request (when CQRS distinction doesn't matter)
public sealed record PingRequest() : IRequest<string>;
```

The type parameter on `IRequest<TResult>` defines the return type of the handler.

---

## 2. Defining a Handler

```csharp
public sealed class GetUserHandler : IRequestHandler<GetUserQuery, User>
{
    private readonly IUserRepository _repo;

    public GetUserHandler(IUserRepository repo) => _repo = repo;

    public async Task<User> HandleAsync(GetUserQuery request, CancellationToken ct)
        => await _repo.GetByIdAsync(request.UserId, ct);
}

public sealed class CreateUserHandler : IRequestHandler<CreateUserCommand, Result<int>>
{
    public async Task<Result<int>> HandleAsync(CreateUserCommand request, CancellationToken ct)
    {
        // Validate, persist, return Result
    }
}
```

One handler per request type. The handler is resolved from DI by the mediator implementation.

---

## 3. Sending Requests

```csharp
public class UserController(IMediator mediator)
{
    public async Task<User> GetUser(int id, CancellationToken ct)
        => await mediator.SendAsync(new GetUserQuery(id), ct);

    public async Task<Result<int>> CreateUser(string name, string email, CancellationToken ct)
        => await mediator.SendAsync(new CreateUserCommand(name, email), ct);
}
```

---

## 4. Defining a Behavior

Behaviors wrap handler execution. They receive a `next` delegate to proceed or short-circuit.

```csharp
// Logging behavior (applies to all requests)
public sealed class LoggingBehavior<TRequest, TResult> : IBehavior<TRequest, TResult>
    where TRequest : IRequest<TResult>
{
    private readonly ILogger _logger;

    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResult>> logger) => _logger = logger;

    public async Task<TResult> HandleAsync(TRequest request, Func<Task<TResult>> next, CancellationToken ct)
    {
        _logger.LogInformation("Handling {Request}", typeof(TRequest).Name);
        var sw = Stopwatch.StartNew();
        var result = await next();
        _logger.LogInformation("Handled {Request} in {Ms}ms", typeof(TRequest).Name, sw.ElapsedMilliseconds);
        return result;
    }
}

// Validation behavior (short-circuits on failure)
public sealed class ValidationBehavior<TRequest, TResult> : IBehavior<TRequest, TResult>
    where TRequest : IRequest<TResult>
{
    public async Task<TResult> HandleAsync(TRequest request, Func<Task<TResult>> next, CancellationToken ct)
    {
        var errors = Validate(request);
        if (errors.Any())
            return (TResult)(object)Result.Failure(errors); // short-circuit, never calls next
        return await next();
    }
}
```

---

## 5. Defining Notifications

```csharp
public sealed record OrderPlaced(Guid OrderId, decimal Total) : INotification;

public sealed record UserCreated(int UserId, string Name) : INotification;
```

### Notification Handlers

```csharp
public sealed class SendWelcomeEmail : INotificationHandler<UserCreated>
{
    public async Task HandleAsync(UserCreated notification, CancellationToken ct)
        => await _emailService.SendWelcomeAsync(notification.Name, ct);
}

public sealed class AuditUserCreation : INotificationHandler<UserCreated>
{
    public async Task HandleAsync(UserCreated notification, CancellationToken ct)
        => await _auditLog.RecordAsync($"User {notification.Name} created", ct);
}
```

Multiple handlers per notification. All are invoked by `PublishAsync`.

---

## 6. Publishing Notifications

```csharp
await mediator.PublishAsync(new UserCreated(userId, "Alice"));
```

---

## 7. Publish Strategies

```csharp
// Sequential (default) -- handlers run one after another
await mediator.PublishAsync(notification, strategy: PublishStrategy.Sequential);

// Parallel -- all handlers run concurrently, all awaited
await mediator.PublishAsync(notification, strategy: PublishStrategy.Parallel);

// Fire and forget -- all handlers started, not awaited, exceptions swallowed
await mediator.PublishAsync(notification, strategy: PublishStrategy.FireAndForget);
```

Choose per notification based on the use case:
- **Sequential**: when handler order matters or handlers share state
- **Parallel**: when handlers are independent and latency matters
- **FireAndForget**: for non-critical side effects (metrics, analytics)

---

## 8. DI Wiring (Example)

The library provides no mediator implementation. Here's a minimal example:

```csharp
public sealed class DependencyInjectionMediator(IServiceProvider sp) : IMediator
{
    public async Task<TResult> SendAsync<TResult>(IRequest<TResult> request, CancellationToken ct)
    {
        var handlerType = typeof(IRequestHandler<,>).MakeGenericType(request.GetType(), typeof(TResult));
        dynamic handler = sp.GetRequiredService(handlerType);
        return await handler.HandleAsync((dynamic)request, ct);
    }

    public async Task PublishAsync(INotification notification, CancellationToken ct, PublishStrategy strategy)
    {
        var handlerType = typeof(INotificationHandler<>).MakeGenericType(notification.GetType());
        var handlers = sp.GetServices(handlerType);
        // dispatch based on strategy...
    }
}
```

Or use a source-generated mediator that resolves handlers at compile time.

---

## 9. Using Result with Mediator

The library depends on `FrenchExDev.Net.Result`, enabling validated request/response:

```csharp
public sealed record CreateOrderCommand(string Product, int Qty) : ICommand<Result<Guid>>;

public sealed class CreateOrderHandler : IRequestHandler<CreateOrderCommand, Result<Guid>>
{
    public async Task<Result<Guid>> HandleAsync(CreateOrderCommand request, CancellationToken ct)
    {
        if (request.Qty <= 0)
            return Result<Guid>.Failure(new ValidationResult("Quantity must be positive"));

        var id = await _repo.CreateAsync(request, ct);
        return Result<Guid>.Success(id);
    }
}
```

---

## 10. Testing with FakeMediator

### Setup canned responses

```csharp
var mediator = new FakeMediator();
mediator.Setup<GetUserQuery, User>(q => new User(q.UserId, "Alice"));

var user = await mediator.SendAsync(new GetUserQuery(42));
Assert.Equal("Alice", user.Name);
```

### Assert requests were sent

```csharp
Assert.True(mediator.WasSent<GetUserQuery>());
Assert.False(mediator.WasSent<CreateUserCommand>());

var recorded = mediator.SentRequests.OfType<GetUserQuery>().Single();
Assert.Equal(42, recorded.UserId);
```

### Assert notifications were published

```csharp
await mediator.PublishAsync(new OrderPlaced(orderId, 99.99m));
Assert.True(mediator.WasPublished<OrderPlaced>());
```

### Reset between tests

```csharp
mediator.Reset(); // clears requests, notifications, and setups
```

---

## 11. Running Tests

```bash
dotnet test Mediator/FrenchExDev.Net.Mediator.slnx

# With quality gate
dotnet quality-gate test --config Mediator/quality-gate.yml
```

Test suite: 11 xUnit tests covering FakeMediator (send, publish, recording, assertions, reset, ordering, strategies).
