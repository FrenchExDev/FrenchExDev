# Reactive -- Architecture

## 1. Overview

Reactive provides a domain-oriented event stream abstraction over System.Reactive (Rx). The core is `IEventStream<T>` -- a covariant interface for subscribing to typed event sequences. `EventStream<T>` is the mutable implementation backed by `Subject<T>`. Ten extension methods wrap Rx operators (`Where`, `Select`, `Merge`, `Buffer`, `Throttle`, `DistinctUntilChanged`, `Take`, `Skip`, `OfType`) under domain-friendly names. `AsObservable()` provides an escape hatch to the full Rx operator surface.

---

## 2. Project Structure

```
Reactive/
  FrenchExDev.Net.Reactive.slnx
  quality-gate.yml
  src/
    FrenchExDev.Net.Reactive/                     (Core library)
      IEventStream.cs                             Interface: Subscribe + AsObservable
      EventStream.cs                              Mutable impl: Publish + Complete + Error
      EventStreamExtensions.cs                    10 operators + ObservableEventStream adapter
    FrenchExDev.Net.Reactive.Testing/             (Test helper)
      TestEventStream.cs                          Records events + Publish + Clear
  test/
    FrenchExDev.Net.Reactive.Tests/               (xUnit tests)
      EventStreamTests.cs                         6 tests for EventStream
      EventStreamExtensionsTests.cs               12 tests for operators + TestEventStream
```

---

## 3. Dependency Graph

```
System.Reactive (NuGet)
  |
  +-- FrenchExDev.Net.Reactive         (refs System.Reactive)
  |     |
  |     +-- FrenchExDev.Net.Reactive.Testing  (refs Reactive)
  |     |
  |     +-- FrenchExDev.Net.Reactive.Tests    (refs Reactive + Testing + xUnit)
```

The core library depends only on `System.Reactive`. The Testing project has no additional NuGet dependencies beyond the core.

---

## 4. Type Architecture

### IEventStream<out T> (interface, covariant)

| Member | Return Type | Purpose |
|--------|-------------|---------|
| `Subscribe(onNext, onError?, onCompleted?)` | `IDisposable` | Subscribe with callbacks |
| `AsObservable()` | `IObservable<T>` | Escape hatch to Rx |

### EventStream<T> (sealed class, implements IEventStream<T>, IDisposable)

| Member | Purpose |
|--------|---------|
| `Publish(T)` | Push an event to all subscribers |
| `Complete()` | Signal stream completion |
| `Error(Exception)` | Signal an error |
| `Subscribe(...)` | Inherited from IEventStream<T> |
| `AsObservable()` | Inherited from IEventStream<T> |
| `Dispose()` | Dispose the underlying Subject<T> |

Backed by `Subject<T>` -- hot observable, multicast to all subscribers.

### ObservableEventStream<T> (internal sealed class)

Adapter wrapping an `IObservable<T>` as an `IEventStream<T>`. Created by operators to keep the return type as `IEventStream<T>` rather than leaking `IObservable<T>`.

### TestEventStream<T> (sealed class, implements IEventStream<T>, IDisposable)

| Member | Purpose |
|--------|---------|
| `Publish(T)` | Push an event and record it |
| `Events` | `IReadOnlyList<T>` of all published events |
| `Clear()` | Reset the recorded events list |

---

## 5. Operator Implementation

All operators follow the same pattern:

1. Null-check the stream (and predicate/mapper if applicable)
2. Call `stream.AsObservable()` to get `IObservable<T>`
3. Apply the corresponding Rx operator
4. Wrap the result in `ObservableEventStream<T>` to return `IEventStream<T>`

| Operator | Rx Delegate |
|----------|------------|
| `Filter(predicate)` | `.Where(predicate)` |
| `Map(mapper)` | `.Select(mapper)` |
| `Merge(other)` | `.Merge(other.AsObservable())` |
| `Buffer(count)` | `.Buffer(count)` |
| `Buffer(timeSpan)` | `.Buffer(timeSpan)` |
| `Throttle(dueTime)` | `.Throttle(dueTime)` |
| `DistinctUntilChanged()` | `.DistinctUntilChanged()` |
| `Take(count)` | `.Take(count)` |
| `Skip(count)` | `.Skip(count)` |
| `OfType<TTarget>()` | `.OfType<TTarget>()` |

Operators are composable -- each returns `IEventStream<T>`, so they chain:

```csharp
stream.Filter(x => x > 0).Map(x => x * 2).Take(5).DistinctUntilChanged()
```

---

## 6. Subscribe Defaults

Both `EventStream<T>` and `ObservableEventStream<T>` provide default no-op handlers for `onError` and `onCompleted` when not specified. This means subscribers that only care about `onNext` don't need to handle errors or completion explicitly:

```csharp
stream.Subscribe(x => Process(x));  // onError and onCompleted are no-ops
```

---

## 7. Ecosystem Position

```
FrenchExDev.Net ecosystem:
  Reactive       <- event stream abstraction (this project)
  Clock          <- time abstraction
  Result         <- error handling
  Builder        <- validated object construction
  ...

Usage pattern:
  Domain layer defines event types (records)
  Services publish to EventStream<T>
  Consumers subscribe via IEventStream<T>
  Operators compose pipelines (Filter, Map, Merge, Buffer...)
  Tests use TestEventStream<T> to assert published events
```

Reactive is used wherever domain events need to be observed, filtered, or composed -- the same role as `IVosEventEmitter` in Vos, but generalized and backed by Rx.
