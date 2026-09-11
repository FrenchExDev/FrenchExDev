# Mediator -- Architecture

## 1. Overview

Mediator provides CQRS mediator contracts: interfaces for requests, handlers, pipeline behaviors, notifications, and a mediator dispatcher. The library contains no implementation -- it defines the shapes that a mediator runtime must fill. A `FakeMediator` test double is provided for unit testing without DI infrastructure.

---

## 2. Project Structure

```
Mediator/
  FrenchExDev.Net.Mediator.slnx
  quality-gate.yml
  src/
    FrenchExDev.Net.Mediator/                      (Contracts -- netstandard2.0 + net10.0)
      IMediator.cs                                 SendAsync + PublishAsync
      IRequest.cs                                  IRequest<TResult>, ICommand<TResult>, IQuery<TResult>
      IRequestHandler.cs                           IRequestHandler<TRequest, TResult>
      IBehavior.cs                                 IBehavior<TRequest, TResult> (pipeline middleware)
      INotification.cs                             Marker interface
      INotificationHandler.cs                      INotificationHandler<TNotification>
      PublishStrategy.cs                            Enum: Sequential, Parallel, FireAndForget
    FrenchExDev.Net.Mediator.Testing/              (Test double -- netstandard2.0 + net10.0)
      FakeMediator.cs                              Records calls, returns canned responses
  test/
    FrenchExDev.Net.Mediator.Tests/                (xUnit tests)
      FakeMediatorTests.cs                         11 tests
```

---

## 3. Dependency Graph

```
FrenchExDev.Net.Result                             (external project reference)
  |
  +-- FrenchExDev.Net.Mediator                     (refs Result, netstandard2.0 + net10.0)
  |     |
  |     +-- FrenchExDev.Net.Mediator.Testing       (refs Mediator)
  |     |
  |     +-- FrenchExDev.Net.Mediator.Tests         (refs Mediator + Testing + xUnit + CsCheck)
```

The only dependency is `FrenchExDev.Net.Result` to support `IRequest<Result<T>>` return types.

---

## 4. Type Hierarchy

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

```
IMediator
  SendAsync<TResult>(IRequest<TResult>, ct) → Task<TResult>
  PublishAsync(INotification, ct, PublishStrategy) → Task
```

---

## 5. Pipeline Model

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
  ← TResult
```

Each behavior receives a `Func<Task<TResult>> next` delegate. Calling `next()` proceeds to the next behavior (or the handler if last). Not calling `next()` short-circuits the pipeline.

Common behaviors:
- **Logging** -- log entry/exit with timing
- **Validation** -- validate request, return failure without calling handler
- **Caching** -- return cached result, skip handler
- **Tracing** -- OpenTelemetry `Activity` span
- **Transaction** -- wrap handler in a DB transaction

---

## 6. Publish Strategies

| Strategy | Behavior |
|----------|----------|
| `Sequential` | Handlers invoked one after another, each awaited. First exception stops the chain. |
| `Parallel` | All handlers started concurrently via `Task.WhenAll`. All exceptions are collected. |
| `FireAndForget` | All handlers started concurrently, not awaited. Exceptions are swallowed. |

The strategy is passed per-call to `PublishAsync`, not configured globally. Different notifications may use different strategies.

---

## 7. FakeMediator Design

`FakeMediator` is a test double that:

1. **Records** all sent requests in `SentRequests` (ordered `List<object>`)
2. **Records** all published notifications in `PublishedNotifications` (ordered `List<INotification>`)
3. **Returns canned responses** via `Setup<TRequest, TResult>(Func<TRequest, TResult>)`
4. **Throws** `InvalidOperationException` if `SendAsync` is called without a matching setup
5. **Ignores** notifications (records but does nothing)
6. Provides **assertion helpers**: `WasSent<T>()`, `WasPublished<T>()`
7. **Reset()** clears all state

The fake does not run behaviors -- it tests what the application sends, not the pipeline itself. Pipeline behavior tests should use integration tests with a real mediator.

---

## 8. CQRS Marker Semantics

| Marker | Semantics | Typical TResult |
|--------|-----------|-----------------|
| `ICommand<TResult>` | Write operation, may have side effects | `Result`, `Result<T>`, `Unit` |
| `IQuery<TResult>` | Read operation, no side effects | `T`, `Result<T>`, `IReadOnlyList<T>` |
| `IRequest<TResult>` | Either, when the distinction doesn't matter | Any |

The markers are purely semantic -- the mediator dispatches them identically. They exist so behaviors can target commands vs queries differently (e.g., only wrap commands in transactions).
