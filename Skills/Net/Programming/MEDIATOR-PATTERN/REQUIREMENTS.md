# MEDIATOR-PATTERN — Requirements

## Contracts (Required)

- [ ] `IRequest<out TResult>` — marker with type-level return type.
- [ ] `ICommand<out TResult> : IRequest<TResult>` — write-side marker.
- [ ] `IQuery<out TResult> : IRequest<TResult>` — read-side marker.
- [ ] `IRequestHandler<in TRequest, TResult> where TRequest : IRequest<TResult>`.
- [ ] `IBehavior<in TRequest, TResult> where TRequest : IRequest<TResult>` with `HandleAsync(request, next, ct)`.
- [ ] `INotification` — marker.
- [ ] `INotificationHandler<in TNotification> where TNotification : INotification`.
- [ ] `IMediator` with `SendAsync<TResult>(IRequest<TResult>, ct)` and `PublishAsync(INotification, ct, PublishStrategy)`.
- [ ] `PublishStrategy` enum: `Sequential`, `Parallel`, `FireAndForget`.

## What MUST NOT Ship

- [ ] No `Mediator` concrete class.
- [ ] No DI registration extension methods.
- [ ] No reflection-based handler resolver in the core library.
- [ ] No global mediator configuration.

## Behavior Pipeline Requirements

- [ ] Each behavior receives an explicit `Func<Task<TResult>> next` delegate.
- [ ] A behavior may short-circuit by NOT calling `next()`.
- [ ] Behaviors compose in flat order (not nested decorators).
- [ ] One generic `LoggingBehavior<TRequest, TResult>` applies to every request type — no per-handler decorator boilerplate.

## Publish Strategy Requirements

- [ ] Strategy is a parameter on `PublishAsync`, not a global setting.
- [ ] Sequential: handlers in order, awaited; first exception stops the chain.
- [ ] Parallel: `Task.WhenAll`; all exceptions collected.
- [ ] FireAndForget: handlers started concurrently, NOT awaited; exceptions swallowed.

## Test Double Requirements

- [ ] Lives in a separate `.Testing` package.
- [ ] Records all sent requests in an ordered `List<object>`.
- [ ] Records all published notifications in an ordered `List<INotification>`.
- [ ] Provides `Setup<TRequest, TResult>(Func<TRequest, TResult>)` for canned responses.
- [ ] `SendAsync` without a matching `Setup` throws `InvalidOperationException` naming the missing type.
- [ ] Provides `WasSent<T>()` and `WasPublished<T>()` assertion helpers.
- [ ] Provides `Reset()` to clear all state.
- [ ] Does NOT run behaviors — pipeline tests use a real mediator.

## Result Coupling

- [ ] The library depends on a Result type so handlers can return `Task<Result<T>>`.

## Multi-Targeting

- [ ] Contracts target `netstandard2.0 + net10.0` so they work everywhere.
- [ ] Test double targets the same.

## What Handlers MUST NOT Do

- [ ] Handlers MUST NOT throw for expected failures — use `Result<T>` return types.
- [ ] Handlers MUST NOT contain cross-cutting concerns (logging, validation, caching) — those belong in behaviors.

## Anchor Package

[`Net/FrenchExDev/Mediator/`](../../../Net/FrenchExDev/Mediator/) — implements every requirement.
