# MEDIATOR-PATTERN — Architecture

## Type Hierarchy

### Request Side

```
IRequest<TResult>                   (marker, defines return type)
  |-- ICommand<TResult>             (write-side CQRS)
  |-- IQuery<TResult>               (read-side CQRS)

IRequestHandler<TRequest, TResult>  (one handler per request type)
  HandleAsync(TRequest, ct) → Task<TResult>

IBehavior<TRequest, TResult>        (pipeline middleware, zero or more)
  HandleAsync(TRequest, next, ct) → Task<TResult>
```

### Notification Side

```
INotification                       (marker)

INotificationHandler<TNotification> (zero or more handlers per notification type)
  HandleAsync(TNotification, ct) → Task
```

### Dispatcher

```csharp
public interface IMediator
{
    Task<TResult> SendAsync<TResult>(IRequest<TResult> request, CancellationToken ct = default);
    Task PublishAsync(INotification notification, CancellationToken ct = default,
                      PublishStrategy strategy = PublishStrategy.Sequential);
}
```

## Pipeline Model

A request flows through behaviors before reaching its handler:

```
SendAsync(request)
  → Behavior₁.HandleAsync(request, next₁, ct)
    → Behavior₂.HandleAsync(request, next₂, ct)
      → ...
        → BehaviorN.HandleAsync(request, nextN, ct)
          → Handler.HandleAsync(request, ct) → TResult
        ← TResult (or short-circuit)
      ← TResult
    ← TResult
```

Each behavior receives a `Func<Task<TResult>> next`. Calling `next()` proceeds to the next behavior (or the handler if last). Not calling `next()` short-circuits the pipeline.

Common behaviors:

- **Logging** — log entry/exit with timing
- **Validation** — validate request, return failure without calling handler
- **Caching** — return cached result, skip handler
- **Tracing** — OpenTelemetry `Activity` span
- **Transaction** — wrap handler in a DB transaction

## Publish Strategies

```csharp
public enum PublishStrategy
{
    Sequential,   // one after another, awaited; first exception stops the chain
    Parallel,     // all concurrent via Task.WhenAll; all exceptions collected
    FireAndForget // all started concurrently, not awaited; exceptions swallowed
}
```

The strategy is passed per-call to `PublishAsync`, not configured globally. Different notifications may use different strategies.

## CQRS Marker Semantics

| Marker | Semantics | Typical TResult |
|---|---|---|
| `ICommand<TResult>` | Write operation, may have side effects | `Result`, `Result<T>`, `Unit` |
| `IQuery<TResult>` | Read operation, no side effects | `T`, `Result<T>`, `IReadOnlyList<T>` |
| `IRequest<TResult>` | Either, when distinction doesn't matter | Any |

The mediator dispatches all three identically. The markers exist so behaviors can target commands vs queries differently using generic constraints.

## Test Double Design

`FakeMediator` (in a separate `.Testing` package) provides:

1. **Recording** — `SentRequests` (`List<object>`), `PublishedNotifications` (`List<INotification>`).
2. **Canned responses** — `Setup<TRequest, TResult>(Func<TRequest, TResult>)`.
3. **Fail-fast** — `SendAsync` without a matching `Setup` throws `InvalidOperationException`.
4. **Assertion helpers** — `WasSent<T>()`, `WasPublished<T>()`.
5. **Reset()** — clears all state.

The fake does **not** run behaviors. It tests what application code sends, not the pipeline itself. Pipeline tests use integration tests with a real mediator.

## Project Decomposition

```
Mediator                  netstandard2.0 + net10.0 — refs Result
  IMediator, IRequest, ICommand, IQuery,
  IRequestHandler, IBehavior, INotification,
  INotificationHandler, PublishStrategy

Mediator.Testing          netstandard2.0 + net10.0 — refs Mediator
  FakeMediator

Mediator.Tests            net10.0 — refs Mediator + Testing + xUnit
```

## DI Wiring (Consumer's Job)

The library provides no implementation. A minimal reflection-based dispatcher:

```csharp
public sealed class DependencyInjectionMediator(IServiceProvider sp) : IMediator
{
    public async Task<TResult> SendAsync<TResult>(IRequest<TResult> request, CancellationToken ct = default)
    {
        var handlerType = typeof(IRequestHandler<,>).MakeGenericType(request.GetType(), typeof(TResult));
        dynamic handler = sp.GetRequiredService(handlerType);
        return await handler.HandleAsync((dynamic)request, ct);
    }
    // PublishAsync similar, dispatches based on strategy
}
```

Or use a source-generated mediator that resolves handlers at compile time for AOT compatibility.

## Result Coupling

The library depends on a `Result` type so handlers can return `Task<Result<T>>` without a parallel error convention. This is a deliberate coupling — it means the mediator and the result type ship together as a coordinated stack.

## Anchor Package

[`Net/FrenchExDev/Mediator/`](../../../Net/FrenchExDev/Mediator/) — 7 interfaces, 1 enum, `FakeMediator` test double.
