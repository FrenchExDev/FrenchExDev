# REACTIVE — How-To

## 1. Creating A Stream

```csharp
var stream = new EventStream<OrderPlaced>();
```

The class implements `IEventStream<OrderPlaced>`. It is `IDisposable` — call `Dispose` when shutting down.

## 2. Publishing Events

```csharp
stream.Publish(new OrderPlaced(orderId, DateTimeOffset.UtcNow));
```

Publishers receive a concrete `EventStream<T>` (which has `Publish`). Consumers receive `IEventStream<T>` (which doesn't).

## 3. Subscribing

```csharp
IDisposable subscription = stream.Subscribe(
    onNext:     order => _orderHandler.Handle(order),
    onError:    ex    => _logger.LogError(ex, "Stream error"),
    onCompleted: ()    => _logger.LogInformation("Stream completed"));

// Later — to stop receiving events
subscription.Dispose();
```

Both `onError` and `onCompleted` are optional.

## 4. Filtering

```csharp
IEventStream<OrderPlaced> highValue = stream.Filter(o => o.Total > 1000);

highValue.Subscribe(o => _vipHandler.Notify(o));
```

`Filter` returns a NEW stream. The original is unchanged.

## 5. Mapping

```csharp
IEventStream<OrderSummary> summaries = stream.Map(o => new OrderSummary(o.Id, o.Total));

summaries.Subscribe(s => _summaryView.Add(s));
```

## 6. Throttling

```csharp
IEventStream<MouseMoveEvent> throttled = mouseEvents.Throttle(TimeSpan.FromMilliseconds(100));
```

Only the last event in each 100ms window is emitted.

## 7. Buffering

```csharp
// Buffer by count
IEventStream<IList<Tick>> batched = ticks.Buffer(100);

// Buffer by time
IEventStream<IList<Tick>> timed = ticks.Buffer(TimeSpan.FromSeconds(1));
```

## 8. Distinct Until Changed

```csharp
IEventStream<int> uniqueValues = sensor.DistinctUntilChanged();
// Suppresses consecutive duplicates: 1,1,1,2,2,3 → 1,2,3
```

## 9. Merge

```csharp
IEventStream<Order> all = onlineOrders.Merge(inStoreOrders);
```

## 10. Type Filter

When the stream carries `object` (e.g. an event bus):

```csharp
IEventStream<object> bus = ...;
IEventStream<UserCreated> userEvents = bus.OfType<UserCreated>();
```

## 11. Composing Operators

```csharp
var pipeline = mouseEvents
    .Filter(e => e.Button == MouseButton.Left)
    .Throttle(TimeSpan.FromMilliseconds(50))
    .Map(e => new ClickEvent(e.X, e.Y))
    .DistinctUntilChanged();

pipeline.Subscribe(click => _ui.Render(click));
```

Operators chain naturally.

## 12. Escape Hatch — Using Rx Directly

For operators not in the domain catalog:

```csharp
var advanced = stream.AsObservable()
    .GroupBy(e => e.Category)
    .SelectMany(g => g.Sample(TimeSpan.FromMinutes(1)));

advanced.Subscribe(e => _categoryHandler.Handle(e));
```

`AsObservable()` returns the underlying `IObservable<T>`. From there, the full `System.Reactive.Linq` surface is available.

## 13. DI Registration

```csharp
// Register the producer-side stream as singleton
services.AddSingleton<EventStream<OrderPlaced>>();

// Register the consumer-side interface as the same instance
services.AddSingleton<IEventStream<OrderPlaced>>(sp => sp.GetRequiredService<EventStream<OrderPlaced>>());
```

Or use `[Injectable]`:

```csharp
[Injectable(Scope = Scope.Singleton)]
public sealed class OrderEventStream : EventStream<OrderPlaced> { }
```

## 14. Testing With `TestEventStream`

```csharp
var stream = new TestEventStream<OrderPlaced>();
var received = new List<OrderPlaced>();

stream.Subscribe(o => received.Add(o));

stream.Publish(new OrderPlaced(orderId, DateTimeOffset.UtcNow));

Assert.Single(received);
Assert.Equal(orderId, received[0].OrderId);
```

For time-sensitive operators (`Throttle`, `Buffer(timeSpan)`), use Rx's `TestScheduler` via the escape hatch.

## What NOT To Do

- **Don't expose `EventStream<T>` to consumers — expose `IEventStream<T>`.** Otherwise consumers can publish.
- **Don't mutate the source stream from inside an operator.** Operators return new streams.
- **Don't subscribe in a loop without disposing.** Subscriptions accumulate; always dispose them when no longer needed.
- **Don't use `EventStream<T>` for cold/replay semantics.** It's hot — late subscribers miss earlier events. Use a `ReplaySubject<T>`-backed stream for replay.
- **Don't forget to dispose `EventStream<T>` itself** when the producer shuts down.

## Anchor Package

[`Net/FrenchExDev/Reactive/`](../../../Net/FrenchExDev/Reactive/) — implementation reference.
