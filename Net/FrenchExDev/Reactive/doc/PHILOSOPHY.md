# Reactive -- Philosophy

## Domain vocabulary over Rx vocabulary

Rx is powerful, but its API is designed for general-purpose reactive programming. `IObservable<T>.Subscribe(IObserver<T>)` is a framework interface, not a domain concept. `.Where()` reads like LINQ, not like event processing.

`IEventStream<T>` reframes the same capabilities in domain terms: `Publish` and `Subscribe` instead of `OnNext` and `Subscribe(IObserver)`. `Filter` instead of `Where`. `Map` instead of `Select`. The operations are identical; the vocabulary signals that this is about domain events, not arbitrary observable sequences.

Code that reads `orderStream.Filter(o => o.Total > 1000).Map(o => new Alert(o.Id))` communicates intent more clearly than `orders.Where(o => o.Total > 1000).Select(o => new Alert(o.Id))`, even though both compile to the same Rx pipeline underneath.

---

## Wrap Rx, don't reimplement it

There is no value in reimplementing `Buffer`, `Throttle`, or `DistinctUntilChanged`. System.Reactive has 15 years of battle-tested operator implementations, edge-case handling, and scheduler support.

Every operator in `EventStreamExtensions` is a one-liner that calls the corresponding Rx operator and wraps the result. The total implementation of 10 operators is 50 lines of code. The value is not in the implementation -- it's in the interface contract that keeps domain code from depending on `System.Reactive.Linq` directly.

If you need an operator that isn't exposed (e.g., `GroupBy`, `Sample`, `Window`), call `AsObservable()` and use Rx directly. The escape hatch exists because wrapping every Rx operator would be pointless -- wrap the common ones, escape for the rest.

---

## IEventStream is read-only, EventStream is read-write

`IEventStream<T>` has no `Publish` method. It only has `Subscribe` and `AsObservable`. This is a deliberate access control: consumers subscribe, producers publish.

`EventStream<T>` adds `Publish`, `Complete`, and `Error`. Code that creates the stream holds the mutable reference. Code that receives the stream through DI or method parameters sees only `IEventStream<T>` and cannot inject events.

This is the same pattern as `IReadOnlyList<T>` vs `List<T>`: the interface restricts what consumers can do, the concrete class enables what producers need.

---

## Covariance enables polymorphic subscription

`IEventStream<out T>` is covariant. A `IEventStream<OrderPlaced>` is assignable to `IEventStream<DomainEvent>` if `OrderPlaced : DomainEvent`. This means:

```csharp
IEventStream<DomainEvent> allEvents = orderPlacedStream; // compiles
```

Without covariance, you'd need `.Map(e => (DomainEvent)e)` to bridge the types. Covariance makes the type system do the work.

This is why `IEventStream<T>` exists as an interface rather than using `EventStream<T>` directly -- concrete classes can't be covariant in C#.

---

## TestEventStream records, not replays

`TestEventStream<T>` keeps a `List<T>` of every event passed to `Publish()`. Tests assert against `Events` after the fact:

```csharp
Assert.Equal(3, stream.Events.Count);
Assert.Equal("expected-id", stream.Events[0].Id);
```

This is simpler and more readable than setting up subscribers, collecting into a list, and asserting. The stream itself is the assertion target.

`TestEventStream` also supports live subscription (it's a real `IEventStream<T>`), so it works with operator composition in tests. `Events` captures everything published, regardless of whether operators downstream filtered some of them out.

---

## Separate Testing package for the same reason as Clock

`TestEventStream<T>` is a test helper. It belongs in a `.Testing` package, not in the core library. Production code should never reference it.

This follows the same split as `Clock` / `Clock.Testing`, `Builder` / `Builder.Testing`: the core package is the contract, the testing package is the test tooling. Different consumers, different packages.

---

## No scheduler abstraction

Rx has `IScheduler` for controlling concurrency and virtual time. This library does not abstract over it because:

1. Most domain event streams are synchronous (publish on the calling thread, subscribers run inline)
2. Time-based operators (`Buffer(TimeSpan)`, `Throttle`) use the default Rx scheduler, which is correct for production
3. For tests needing scheduler control, use `AsObservable()` and Rx's `TestScheduler` directly

Adding a scheduler abstraction would be premature -- the common case doesn't need it, and the uncommon case has a clean escape hatch.
