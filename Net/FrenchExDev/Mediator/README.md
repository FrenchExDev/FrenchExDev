# Mediator

Lightweight CQRS mediator contracts for .NET. Defines `IMediator`, `IRequest<TResult>` (with `ICommand<TResult>` and `IQuery<TResult>` sub-interfaces), `IRequestHandler<TRequest, TResult>`, `IBehavior<TRequest, TResult>` (pipeline middleware), `INotification`, `INotificationHandler<T>`, and `PublishStrategy` (Sequential, Parallel, FireAndForget). No implementation -- just contracts. Consumers wire their own DI-based mediator or use the `FakeMediator` for testing.

## Quick Start

```csharp
// Define a query
public sealed record GetUserQuery(int UserId) : IQuery<User>;

// Define its handler
public class GetUserHandler : IRequestHandler<GetUserQuery, User>
{
    public async Task<User> HandleAsync(GetUserQuery request, CancellationToken ct)
        => await _db.Users.FindAsync(request.UserId, ct);
}

// Define a behavior (middleware)
public class LoggingBehavior<TRequest, TResult> : IBehavior<TRequest, TResult>
    where TRequest : IRequest<TResult>
{
    public async Task<TResult> HandleAsync(TRequest request, Func<Task<TResult>> next, CancellationToken ct)
    {
        Log($"Handling {typeof(TRequest).Name}");
        var result = await next();
        Log($"Handled {typeof(TRequest).Name}");
        return result;
    }
}

// Publish a notification to multiple handlers
await mediator.PublishAsync(new OrderPlaced(orderId), strategy: PublishStrategy.Parallel);

// Test with FakeMediator
var fake = new FakeMediator();
fake.Setup<GetUserQuery, User>(q => new User(q.UserId, "Alice"));
var user = await fake.SendAsync(new GetUserQuery(42));
Assert.True(fake.WasSent<GetUserQuery>());
```

## Projects

| Project | TFM | Purpose |
|---------|-----|---------|
| `Mediator` | netstandard2.0; net10.0 | Contracts: IMediator, IRequest, ICommand, IQuery, IRequestHandler, IBehavior, INotification, INotificationHandler, PublishStrategy |
| `Mediator.Testing` | netstandard2.0; net10.0 | `FakeMediator` with canned responses, request/notification recording, WasSent/WasPublished assertions |
| `Mediator.Tests` | net10.0 | 11 xUnit tests |

## Key Design Decisions

- **Contracts only, no implementation** -- the mediator library defines interfaces, not a runtime dispatcher. Consumers choose their DI container and wiring strategy
- **CQRS markers** -- `ICommand<TResult>` and `IQuery<TResult>` extend `IRequest<TResult>` for semantic clarity without forcing separate pipelines
- **Pipeline behaviors** -- `IBehavior<TRequest, TResult>` wraps handler execution with a `next` delegate, enabling logging, validation, caching, tracing as composable middleware
- **Three publish strategies** -- Sequential (ordered, awaited), Parallel (concurrent, awaited), FireAndForget (concurrent, not awaited)
- **Depends on Result** -- enables `IRequest<Result<T>>` patterns for validated request/response flows

## Documentation

- [ARCHITECTURE.md](doc/ARCHITECTURE.md) -- type hierarchy, pipeline model, publish strategies, FakeMediator design
- [HOW-TO.md](doc/HOW-TO.md) -- defining requests/handlers, behaviors, notifications, DI wiring, testing
- [PHILOSOPHY.md](doc/PHILOSOPHY.md) -- why contracts only, why CQRS markers, why behaviors over decorators

## Building

```bash
dotnet build Mediator/FrenchExDev.Net.Mediator.slnx
dotnet test Mediator/FrenchExDev.Net.Mediator.slnx
```
