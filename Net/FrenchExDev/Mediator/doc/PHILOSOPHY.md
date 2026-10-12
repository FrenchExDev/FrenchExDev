# Mediator -- Philosophy

## Contracts only, no runtime

This library contains 7 interfaces, 1 enum, and 0 classes (excluding the test double). There is no `Mediator` class. There is no DI registration. There is no reflection-based handler resolution.

This is deliberate. A mediator runtime is tightly coupled to its DI container, its behavior pipeline strategy, and its error handling policy. MediatR makes specific choices about all three. Those choices are reasonable but not universal.

By shipping only contracts, this library allows:
- A reflection-based runtime for rapid prototyping
- A source-generated runtime for AOT compatibility
- A manual wiring approach for small applications
- Integration with any DI container (Microsoft, DryIoc, Autofac)

The contracts are the stable part. The runtime is the variable part. Shipping them separately prevents the stable part from changing when the runtime evolves.

---

## CQRS markers because intent matters

`ICommand<TResult>` and `IQuery<TResult>` both extend `IRequest<TResult>`. The mediator dispatches them identically. So why have them?

Because behaviors can discriminate. A transaction behavior should wrap commands but not queries. A caching behavior should wrap queries but not commands. Without markers, the behavior must inspect the request type at runtime to decide. With markers, the DI registration handles it:

```csharp
// Register transaction behavior only for commands
services.AddScoped(typeof(IBehavior<,>), typeof(TransactionBehavior<,>));
// constrained: where TRequest : ICommand<TResult>
```

The markers cost nothing -- they're empty interfaces with no members. They carry semantic meaning that generic `IRequest<TResult>` does not.

---

## Behaviors over decorators

The decorator pattern wraps a handler with another handler of the same interface. This works, but composing multiple decorators requires nesting, and the order depends on DI registration order (which is fragile and container-specific).

`IBehavior<TRequest, TResult>` is a pipeline middleware with an explicit `next` delegate. This is the same pattern as ASP.NET Core middleware, MediatR pipeline behaviors, and the Vos pipeline in this ecosystem.

Advantages over decorators:
- **Explicit `next`** -- the behavior decides whether to proceed. A decorator always delegates (or must know the inner handler's interface to skip it).
- **Short-circuit** -- returning without calling `next()` skips the handler entirely. Common for validation and caching.
- **Composable** -- behaviors are ordered in a flat list, not nested. Adding or removing one doesn't restructure the chain.
- **Cross-cutting** -- a single `LoggingBehavior<TRequest, TResult>` applies to all requests. A decorator would need one per handler.

---

## PublishStrategy per-call, not per-configuration

The publish strategy is a parameter on `PublishAsync`, not a global setting. This is because different notifications have different dispatch requirements within the same application:

- `OrderPlaced` → `Sequential` (payment handler must run before shipping handler)
- `MetricsCollected` → `FireAndForget` (analytics doesn't need to block the request)
- `CacheInvalidated` → `Parallel` (multiple caches can flush concurrently)

A global strategy forces all notifications into the same pattern. Per-call strategy respects that notifications are diverse.

---

## FakeMediator records, not mocks

`FakeMediator` is not a mock object. It doesn't verify expectations or assert call order automatically. It records what happened and exposes the data for manual assertions:

```csharp
Assert.True(mediator.WasSent<GetUserQuery>());
var query = mediator.SentRequests.OfType<GetUserQuery>().Single();
Assert.Equal(42, query.UserId);
```

This is simpler and more readable than mock setup/verify chains. The test author writes explicit assertions about what they care about, not framework-imposed verification.

The `Setup<TRequest, TResult>` method registers canned response factories -- not expectations. If you forget to set up a request type, `SendAsync` throws with a clear message naming the missing type. This is a fail-fast guard, not a verification step.

---

## Depends on Result because mediator requests fail

A mediator request that returns `User` has no way to signal "user not found" except by throwing or returning null. Both are problems: exceptions for expected outcomes are expensive and null requires the caller to remember to check.

Depending on `FrenchExDev.Net.Result` enables `ICommand<Result<Guid>>` and `IQuery<Result<User>>`. The handler returns `Result.Success(value)` or `Result.Failure(error)`. The caller pattern-matches on the result. No exceptions for expected failures, no null for expected absence.

This is a deliberate coupling. In the FrenchExDev ecosystem, `Result<T>` is the standard error-handling primitive. A mediator that can't use it would need a parallel error convention.

---

## netstandard2.0 for the same reason as the other contracts

The mediator contracts and the fake are `netstandard2.0`. Handlers and behaviors are written in application code targeting modern .NET. But the interfaces themselves work everywhere: .NET Framework services, Xamarin, Unity, .NET 6+, .NET 10.

The contracts are leaf types with no dependencies beyond `Result`. There is no reason to restrict them to modern TFMs.
