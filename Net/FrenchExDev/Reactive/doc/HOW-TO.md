# Reactive -- Developer Guide (HOW-TO)

## Table of Contents

1. [Creating a Stream](#1-creating-a-stream)
2. [Publishing Events](#2-publishing-events)
3. [Subscribing](#3-subscribing)
4. [Filtering Events](#4-filtering-events)
5. [Projecting Events](#5-projecting-events)
6. [Merging Streams](#6-merging-streams)
7. [Buffering](#7-buffering)
8. [Throttling](#8-throttling)
9. [Deduplication](#9-deduplication)
10. [Taking and Skipping](#10-taking-and-skipping)
11. [Filtering by Type](#11-filtering-by-type)
12. [Composing Operators](#12-composing-operators)
13. [Rx Interop](#13-rx-interop)
14. [Testing with TestEventStream](#14-testing-with-testeventstream)
15. [DI Registration](#15-di-registration)
16. [Running Tests](#16-running-tests)

---

## 1. Creating a Stream

```csharp
using var stream = new EventStream<OrderPlaced>();
```

`EventStream<T>` is `IDisposable` -- dispose it to release the underlying `Subject<T>` and signal completion to late subscribers.

---

## 2. Publishing Events

```csharp
stream.Publish(new OrderPlaced("ORD-001", total: 250.00m));
stream.Publish(new OrderPlaced("ORD-002", total: 1500.00m));

// Signal stream end
stream.Complete();

// Or signal an error
stream.Error(new InvalidOperationException("Source failed"));
```

After `Complete()` or `Error()`, further `Publish()` calls are ignored by the underlying `Subject<T>`.

---

## 3. Subscribing

```csharp
// Minimal -- just onNext
using var sub = stream.Subscribe(e => Console.WriteLine(e));

// Full -- onNext + onError + onCompleted
using var sub = stream.Subscribe(
    onNext: e => Process(e),
    onError: ex => Log.Error(ex),
    onCompleted: () => Log.Info("Stream completed"));
```

`Subscribe` returns `IDisposable`. Disposing the subscription stops event delivery to that subscriber. Other subscribers are unaffected.

### Multiple subscribers

```csharp
using var sub1 = stream.Subscribe(e => SaveToDb(e));
using var sub2 = stream.Subscribe(e => SendNotification(e));

stream.Publish(event); // both subscribers receive it
```

---

## 4. Filtering Events

```csharp
var highValue = stream.Filter(e => e.Total > 1000);
using var sub = highValue.Subscribe(e => Alert(e));

stream.Publish(new Order(total: 500));   // filtered out
stream.Publish(new Order(total: 2000));  // passes through
```

---

## 5. Projecting Events

```csharp
// Same type
var doubled = numbers.Map(x => x * 2);

// Different type
var alerts = orders.Map(o => new Alert(o.OrderId, $"Total: {o.Total}"));
using var sub = alerts.Subscribe(a => Notify(a));
```

---

## 6. Merging Streams

```csharp
var orders = new EventStream<DomainEvent>();
var payments = new EventStream<DomainEvent>();

var all = orders.Merge(payments);
using var sub = all.Subscribe(e => Audit(e));

orders.Publish(new OrderPlaced(...));       // received
payments.Publish(new PaymentReceived(...)); // also received
```

---

## 7. Buffering

### By count

```csharp
var batches = stream.Buffer(count: 10);
using var sub = batches.Subscribe(batch =>
{
    // batch is IList<T> with exactly 10 items
    BulkInsert(batch);
});
```

### By time window

```csharp
var batches = stream.Buffer(TimeSpan.FromSeconds(5));
using var sub = batches.Subscribe(batch =>
{
    // batch contains all events from the last 5 seconds
    Flush(batch);
});
```

---

## 8. Throttling

```csharp
// Only emit the last event within each 500ms window
var throttled = searchInput.Throttle(TimeSpan.FromMilliseconds(500));
using var sub = throttled.Subscribe(query => Search(query));
```

Useful for UI search-as-you-type, sensor data debouncing, or rate-limiting API calls.

---

## 9. Deduplication

```csharp
var distinct = sensorReadings.DistinctUntilChanged();
using var sub = distinct.Subscribe(v => Process(v));

// Publish: 1, 1, 2, 2, 1
// Received: 1, 2, 1 (consecutive duplicates suppressed)
```

Uses `EqualityComparer<T>.Default`. For custom equality, apply `Map` first or use Rx directly via `AsObservable()`.

---

## 10. Taking and Skipping

```csharp
// First 5 events only, then auto-completes
var first5 = stream.Take(5);

// Skip first 3 events, then deliver the rest
var afterWarmup = stream.Skip(3);
```

---

## 11. Filtering by Type

For heterogeneous event streams (`IEventStream<object>`):

```csharp
using var stream = new EventStream<object>();
var ints = stream.OfType<int>();
var strings = stream.OfType<string>();

using var sub1 = ints.Subscribe(i => Console.WriteLine($"Int: {i}"));
using var sub2 = strings.Subscribe(s => Console.WriteLine($"String: {s}"));

stream.Publish(42);       // sub1 receives
stream.Publish("hello");  // sub2 receives
stream.Publish(3.14);     // neither receives
```

---

## 12. Composing Operators

Operators chain because each returns `IEventStream<T>`:

```csharp
var pipeline = sensorStream
    .Filter(s => s.Value > threshold)
    .DistinctUntilChanged()
    .Map(s => new Alert(s.SensorId, s.Value))
    .Take(100);

using var sub = pipeline.Subscribe(alert => Notify(alert));
```

---

## 13. Rx Interop

For operators not exposed on `IEventStream<T>`, drop to Rx:

```csharp
using System.Reactive.Linq;

var rxStream = stream.AsObservable()
    .GroupBy(e => e.Category)
    .SelectMany(group => group.Sample(TimeSpan.FromSeconds(1)));

// Subscribe directly on the IObservable
rxStream.Subscribe(e => Process(e));
```

---

## 14. Testing with TestEventStream

`TestEventStream<T>` records every published event for later assertion:

```csharp
using var stream = new TestEventStream<OrderPlaced>();

// Act -- code under test publishes to the stream
var service = new OrderService(stream);
service.PlaceOrder(cart);

// Assert -- check recorded events
Assert.Single(stream.Events);
Assert.Equal("ORD-001", stream.Events[0].OrderId);

// Reset for next test phase
stream.Clear();
Assert.Empty(stream.Events);
```

### Testing operators on TestEventStream

`TestEventStream<T>` implements `IEventStream<T>`, so operators compose on it too:

```csharp
using var stream = new TestEventStream<int>();
var filtered = stream.Filter(x => x > 5);
var received = new List<int>();
using var sub = filtered.Subscribe(received.Add);

stream.Publish(3);
stream.Publish(7);
stream.Publish(10);

Assert.Equal([7, 10], received);
Assert.Equal([3, 7, 10], stream.Events); // all events recorded, even filtered ones
```

---

## 15. DI Registration

```csharp
// Register a shared event bus
services.AddSingleton<EventStream<DomainEvent>>();
services.AddSingleton<IEventStream<DomainEvent>>(sp =>
    sp.GetRequiredService<EventStream<DomainEvent>>());

// Producers inject EventStream<T> (to Publish)
// Consumers inject IEventStream<T> (to Subscribe only)
```

---

## 16. Running Tests

```bash
dotnet test Reactive/FrenchExDev.Net.Reactive.slnx

# With quality gate
dotnet quality-gate test --config Reactive/quality-gate.yml
```

Test suite: 18 xUnit tests covering `EventStream` (6 tests) and operators + `TestEventStream` (12 tests).
