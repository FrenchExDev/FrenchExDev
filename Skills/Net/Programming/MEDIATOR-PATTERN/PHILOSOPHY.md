# MEDIATOR-PATTERN — Philosophy

A mediator decouples request senders from request handlers. CQRS markers (`ICommand` / `IQuery`) make read/write intent explicit. Pipeline behaviors compose cross-cutting concerns. The library ships **contracts only** — the runtime is whatever the consumer chooses.

## Contracts Only, No Runtime

A mediator library defines interfaces and a test double. It does **not** ship a `Mediator` class, a DI registration helper, or a reflection-based handler resolver.

This is deliberate. A mediator runtime is tightly coupled to:

- Its DI container (Microsoft DI, DryIoc, Autofac)
- Its pipeline strategy (decorator-based, middleware-based, source-generated)
- Its error handling policy (exceptions vs `Result<T>`)

MediatR makes specific choices about all three. Those choices are reasonable but not universal. By shipping only contracts, the library allows:

- A reflection-based runtime for rapid prototyping
- A source-generated runtime for AOT compatibility
- A manual wiring approach for small applications
- Integration with any DI container

The contracts are the stable part. The runtime is the variable part. Shipping them separately prevents the stable part from changing when the runtime evolves.

## CQRS Markers Because Intent Matters

```csharp
public interface IRequest<out TResult> { }
public interface ICommand<out TResult> : IRequest<TResult> { }
public interface IQuery<out TResult>   : IRequest<TResult> { }
```

`ICommand<TResult>` and `IQuery<TResult>` both extend `IRequest<TResult>`. The mediator dispatches them identically. So why have them?

**Because behaviors can discriminate.** A transaction behavior should wrap commands but not queries. A caching behavior should wrap queries but not commands. Without markers, the behavior must inspect the request type at runtime to decide. With markers, the DI registration handles it via generic constraints:

```csharp
// Register transaction behavior only for commands
services.AddScoped(typeof(IBehavior<,>), typeof(TransactionBehavior<,>));
// where TRequest : ICommand<TResult>
```

The markers cost nothing — they're empty interfaces with no members. They carry semantic meaning that generic `IRequest<TResult>` does not.

## Behaviors Over Decorators

The decorator pattern wraps a handler with another handler of the same interface. This works, but composing multiple decorators requires nesting, and the order depends on DI registration order (which is fragile and container-specific).

`IBehavior<TRequest, TResult>` is a pipeline middleware with an explicit `next` delegate. This is the same pattern as ASP.NET Core middleware:

```csharp
public interface IBehavior<TRequest, TResult> where TRequest : IRequest<TResult>
{
    Task<TResult> HandleAsync(TRequest request, Func<Task<TResult>> next, CancellationToken ct);
}
```

Advantages:

- **Explicit `next`** — the behavior decides whether to proceed.
- **Short-circuit** — returning without calling `next()` skips the handler entirely (validation, caching).
- **Composable** — behaviors are ordered in a flat list, not nested. Adding or removing one doesn't restructure the chain.
- **Cross-cutting** — a single `LoggingBehavior<TRequest, TResult>` applies to all requests. A decorator would need one per handler.

## `PublishStrategy` Per Call, Not Per Configuration

Different notifications have different dispatch needs within the same application:

- `OrderPlaced` → `Sequential` (payment handler must run before shipping handler)
- `MetricsCollected` → `FireAndForget` (analytics doesn't need to block the request)
- `CacheInvalidated` → `Parallel` (multiple caches can flush concurrently)

A global publish strategy forces all notifications into the same pattern. **Per-call strategy respects that notifications are diverse**:

```csharp
await mediator.PublishAsync(notification, strategy: PublishStrategy.Parallel);
```

## Test Double Records, Not Mocks

The test double (`FakeMediator`) is not a mock object. It doesn't verify expectations or assert call order automatically. It records what happened and exposes the data for explicit assertions:

```csharp
Assert.True(mediator.WasSent<GetUserQuery>());
var query = mediator.SentRequests.OfType<GetUserQuery>().Single();
Assert.Equal(42, query.UserId);
```

`Setup<TRequest, TResult>(factory)` registers canned responses — not expectations. If you forget to set up a request type, `SendAsync` throws with a clear message naming the missing type. Fail-fast guard, not verification step.

## Depends On Result Because Mediator Requests Fail

A mediator request returning `User` has no way to signal "user not found" except by throwing or returning null. Both are problems. Depending on a Result type enables `IRequest<Result<T>>` for validated request/response flows. The handler returns `Result.Success(value)` or `Result.Failure(error)`. The caller pattern-matches on the result. No exceptions for expected failures.

## Anchor Package

[`Net/FrenchExDev/Mediator/`](../../../Net/FrenchExDev/Mediator/) — 7 interfaces, 1 enum, 0 runtime classes (plus a `FakeMediator` for tests).
