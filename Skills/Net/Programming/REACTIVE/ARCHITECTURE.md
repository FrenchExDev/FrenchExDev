# REACTIVE — Architecture

## Core Types

### `IEventStream<out T>` — Consumer Interface

```csharp
public interface IEventStream<out T>
{
    IDisposable Subscribe(Action<T> onNext, Action<Exception>? onError = null, Action? onCompleted = null);
    IObservable<T> AsObservable();
}
```

Covariant. Read-only. Provides Rx escape hatch.

### `EventStream<T>` — Mutable Producer

```csharp
public sealed class EventStream<T> : IEventStream<T>, IDisposable
{
    private readonly Subject<T> _subject = new();

    public void Publish(T value) => _subject.OnNext(value);
    public void Complete() => _subject.OnCompleted();
    public void Error(Exception error) => _subject.OnError(error);

    public IDisposable Subscribe(Action<T> onNext, Action<Exception>? onError = null, Action? onCompleted = null)
        => _subject.Subscribe(onNext, onError ?? (_ => { }), onCompleted ?? (() => { }));

    public IObservable<T> AsObservable() => _subject.AsObservable();

    public void Dispose() => _subject.Dispose();
}
```

The producer methods (`Publish`, `Complete`, `Error`) are NOT on `IEventStream<T>` — separation of concerns enforces "who can read vs who can write".

### `ObservableEventStream<T>` — Internal Adapter

Operators return new streams. They do this via an internal adapter that wraps an `IObservable<T>`:

```csharp
internal sealed class ObservableEventStream<T> : IEventStream<T>
{
    private readonly IObservable<T> _observable;
    internal ObservableEventStream(IObservable<T> observable) => _observable = observable;

    public IDisposable Subscribe(Action<T> onNext, Action<Exception>? onError = null, Action? onCompleted = null)
        => _observable.Subscribe(onNext, onError ?? (_ => { }), onCompleted ?? (() => { }));

    public IObservable<T> AsObservable() => _observable;
}
```

The adapter is `internal` — application code never references it directly.

## Operator Pattern

Every operator follows the same shape:

```csharp
public static IEventStream<T> Filter<T>(this IEventStream<T> stream, Func<T, bool> predicate)
{
    ArgumentNullException.ThrowIfNull(stream);
    ArgumentNullException.ThrowIfNull(predicate);
    return new ObservableEventStream<T>(stream.AsObservable().Where(predicate));
}
```

1. Null-check the inputs.
2. Call `stream.AsObservable()` to drop into Rx.
3. Apply the underlying Rx operator (`Where`, `Select`, `Merge`, `Buffer`, ...).
4. Wrap the result in `ObservableEventStream<T>`.

The wrapping is what hides Rx from the consumer.

## Operator Catalog

| Domain Name | Rx Underlying | Purpose |
|---|---|---|
| `Filter(predicate)` | `Where` | Keep matching events |
| `Map(mapper)` | `Select` | Project events to a new shape |
| `Merge(other)` | `Merge` | Combine two streams |
| `Buffer(int count)` | `Buffer(count)` | Group N events into a list |
| `Buffer(TimeSpan window)` | `Buffer(timeSpan)` | Group events in a time window |
| `Throttle(TimeSpan window)` | `Throttle(window)` | Emit only the last event in a window |
| `DistinctUntilChanged()` | `DistinctUntilChanged` | Suppress consecutive duplicates |
| `Take(count)` | `Take` | First N events only |
| `Skip(count)` | `Skip` | Skip first N events |
| `OfType<TTarget>()` | `OfType` | Filter by runtime type |

Operators return new `IEventStream<T>` instances (the internal adapter). They do NOT mutate the source stream.

## Project Layout

```
Reactive             net10.0 — refs System.Reactive
  IEventStream.cs
  EventStream.cs
  EventStreamExtensions.cs   (operators + ObservableEventStream adapter)

Reactive.Testing     net10.0 — refs Reactive
  TestEventStream.cs

Reactive.Tests       net10.0 — xUnit
```

## Subject Choice

`EventStream<T>` uses `Subject<T>` — a **hot, multicast, no-replay** observable. Subscribers only see events published after they subscribe. No buffering, no caching.

For replay scenarios, use `ReplaySubject<T>` via the escape hatch:

```csharp
public sealed class ReplayingStream<T> : IEventStream<T>
{
    private readonly ReplaySubject<T> _subject;
    public ReplayingStream(int bufferSize) => _subject = new ReplaySubject<T>(bufferSize);
    // ... same Subscribe / AsObservable / Publish pattern
}
```

## Disposal

`EventStream<T>` implements `IDisposable`. Disposing it disposes the underlying `Subject<T>`, which signals completion to all subscribers and releases resources.

Subscribers receive an `IDisposable` from `Subscribe(...)`. Disposing it removes that subscriber but doesn't affect the stream itself.

## Anchor Package

[`Net/FrenchExDev/Reactive/`](../../../Net/FrenchExDev/Reactive/) — implementation reference.
