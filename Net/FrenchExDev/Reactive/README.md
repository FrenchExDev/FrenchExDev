# Reactive

Domain-oriented event stream abstraction over System.Reactive (Rx). Wraps `Subject<T>` and Rx operators behind a simpler `IEventStream<T>` interface with `Publish`/`Subscribe` semantics and composable operators (`Filter`, `Map`, `Merge`, `Buffer`, `Throttle`, `DistinctUntilChanged`, `Take`, `Skip`, `OfType`).

## Quick Start

```csharp
// Create a stream and subscribe
using var stream = new EventStream<OrderPlaced>();
using var sub = stream.Subscribe(
    onNext: e => Console.WriteLine($"Order {e.OrderId} placed at {e.Timestamp}"),
    onError: ex => Console.Error.WriteLine(ex));

// Publish events
stream.Publish(new OrderPlaced("ORD-001", DateTimeOffset.UtcNow));
stream.Publish(new OrderPlaced("ORD-002", DateTimeOffset.UtcNow));

// Compose with operators
var highValue = stream
    .Filter(e => e.Total > 1000)
    .Map(e => new Alert(e.OrderId, "High-value order"));

// Merge multiple streams
var allEvents = orderStream.Merge(paymentStream);

// Buffer into batches
var batches = stream.Buffer(count: 10);
```

## Projects

| Project | TFM | Purpose |
|---------|-----|---------|
| `Reactive` | net10.0 | `IEventStream<T>`, `EventStream<T>`, 10 operators |
| `Reactive.Testing` | net10.0 | `TestEventStream<T>` with event recording |
| `Reactive.Tests` | net10.0 | 18 xUnit tests |

## Operators

| Operator | Description |
|----------|-------------|
| `Filter(predicate)` | Keep events matching condition |
| `Map(selector)` | Project events to a new type |
| `Merge(other)` | Combine two streams into one |
| `Buffer(count)` | Group events into fixed-size lists |
| `Buffer(timeSpan)` | Group events into time windows |
| `Throttle(dueTime)` | Emit last event per time window |
| `DistinctUntilChanged()` | Suppress consecutive duplicates |
| `Take(count)` | Take first N events then complete |
| `Skip(count)` | Skip first N events |
| `OfType<T>()` | Filter by runtime type |

## Key Design Decisions

- **Wraps Rx, does not replace it** -- `AsObservable()` exposes the underlying `IObservable<T>` for full Rx interop
- **`IEventStream<T>` is covariant** (`out T`) -- a stream of `OrderPlaced` is a stream of `DomainEvent`
- **Operators return `IEventStream<T>`** -- not `IObservable<T>`, keeping the domain vocabulary through the pipeline
- **`TestEventStream<T>` records events** -- `Events` property gives assertion access without subscribing

## Documentation

- [ARCHITECTURE.md](doc/ARCHITECTURE.md) -- project structure, API surface, operator implementation, Rx interop
- [HOW-TO.md](doc/HOW-TO.md) -- publishing, subscribing, composing operators, testing patterns
- [PHILOSOPHY.md](doc/PHILOSOPHY.md) -- why wrap Rx, why domain vocabulary, why a separate Testing package

## Building

```bash
dotnet build Reactive/FrenchExDev.Net.Reactive.slnx
dotnet test Reactive/FrenchExDev.Net.Reactive.slnx
```
