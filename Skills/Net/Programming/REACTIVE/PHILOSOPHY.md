# REACTIVE — Philosophy

A domain-oriented event stream wraps Rx (`System.Reactive`) behind a small interface that uses **domain language** (`Publish`, `Subscribe`, `Filter`, `Map`, `Throttle`) instead of Rx jargon (`OnNext`, `Subject<T>`, `Where`, `Select`). The escape hatch (`AsObservable()`) is always available for code that needs the full Rx surface.

## Wrap Rx, Don't Replace It

Rx is a phenomenally powerful library, but its surface is enormous and its naming comes from a different tradition (`Observable.Where`, `OnNext`, `Subject<T>`, `IObserver<T>`). Application code that wants to publish a domain event and let other parts of the system react doesn't need 80% of that.

The fix is a thin domain interface that:

- Speaks application language (`Publish(value)`, `Subscribe(onNext)`).
- Provides the operators developers actually use (`Filter`, `Map`, `Throttle`, `Buffer`, `DistinctUntilChanged`, `Merge`).
- Delegates internally to Rx so the heavy lifting is battle-tested.
- Exposes `AsObservable()` for code that needs full Rx.

This is the same pattern as `IClock` over `TimeProvider`: a smaller, domain-aligned surface with an escape hatch for the rare cases that need more.

## `IEventStream<out T>` — The Minimal Read Surface

```csharp
public interface IEventStream<out T>
{
    IDisposable Subscribe(Action<T> onNext, Action<Exception>? onError = null, Action? onCompleted = null);
    IObservable<T> AsObservable();
}
```

- **Covariant** (`out T`) — a stream of `Order` can be passed where a stream of `object` is expected.
- **Read-only from the consumer's perspective** — the interface provides no way to push events. Only `Subscribe` and the Rx escape hatch.
- **Optional error/completed callbacks** — callers who don't care don't have to provide them.

## `EventStream<T>` — The Mutable Producer

```csharp
public sealed class EventStream<T> : IEventStream<T>, IDisposable
{
    private readonly Subject<T> _subject = new();

    public void Publish(T value);
    public void Complete();
    public void Error(Exception error);
}
```

The producer side (`Publish`, `Complete`, `Error`) is on the **concrete class**, not on the interface. This separates "who can read" from "who can write":

- Methods that publish events accept `EventStream<T>` (or own one privately).
- Methods that consume events accept `IEventStream<T>`.
- A consumer that receives `IEventStream<T>` cannot accidentally publish.

## Domain-Named Operators, Not Rx Jargon

```csharp
public static IEventStream<T> Filter<T>(this IEventStream<T> stream, Func<T, bool> predicate);
public static IEventStream<TResult> Map<T, TResult>(this IEventStream<T> stream, Func<T, TResult> mapper);
public static IEventStream<T> Merge<T>(this IEventStream<T> stream, IEventStream<T> other);
public static IEventStream<IList<T>> Buffer<T>(this IEventStream<T> stream, int count);
public static IEventStream<IList<T>> Buffer<T>(this IEventStream<T> stream, TimeSpan window);
public static IEventStream<T> Throttle<T>(this IEventStream<T> stream, TimeSpan window);
public static IEventStream<T> DistinctUntilChanged<T>(this IEventStream<T> stream);
public static IEventStream<T> Take<T>(this IEventStream<T> stream, int count);
public static IEventStream<T> Skip<T>(this IEventStream<T> stream, int count);
public static IEventStream<TTarget> OfType<TTarget>(this IEventStream<object> stream);
```

Every operator delegates to Rx via `stream.AsObservable().{RxOperator}(...)` and wraps the result in a small internal `ObservableEventStream<T>` adapter. The application code never sees `Where`/`Select`/`Throttle` from `System.Reactive.Linq` — only the domain names.

## Escape Hatch When Needed

For genuinely advanced Rx scenarios (custom schedulers, cold-vs-hot, time-based testing with `TestScheduler`), `AsObservable()` returns the underlying `IObservable<T>`:

```csharp
var advanced = stream.AsObservable()
    .GroupBy(e => e.UserId)
    .SelectMany(g => g.Sample(TimeSpan.FromMinutes(1)));
```

Drop into Rx for the 5% of cases that need it. Stay in domain operators for the 95%.

## Subjects Are Hot, Not Cold

`EventStream<T>` is backed by `Subject<T>` — a **hot** observable. Subscribers only receive events published **after** they subscribe. There is no replay, no caching, no deferred execution.

If you need replay or caching, use `ReplaySubject<T>` directly via the escape hatch, or build a domain-specific stream type that exposes a buffer.

## Anchor Package

[`Net/FrenchExDev/Reactive/`](../../../Net/FrenchExDev/Reactive/) — `IEventStream<T>`, `EventStream<T>` (Subject-backed), domain operators, and a `TestEventStream` for tests.
