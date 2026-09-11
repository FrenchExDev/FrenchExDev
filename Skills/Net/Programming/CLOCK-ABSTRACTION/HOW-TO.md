# CLOCK-ABSTRACTION — How-To

## 1. DI Registration

```csharp
services.AddSingleton<IClock>(SystemClock.Instance);
```

`SystemClock` has no state, so a singleton is correct. Without DI:

```csharp
IClock clock = SystemClock.Instance;
```

## 2. Inject The Clock Into Services

```csharp
public sealed class OrderService(IClock clock, IOrderRepository repo)
{
    public Order Place(Cart cart) => new()
    {
        PlacedAt  = clock.UtcNow,
        ExpiresAt = clock.UtcNow.AddHours(24)
    };

    public AuditEntry Audit(string action) => new()
    {
        Action    = action,
        Timestamp = clock.UtcNow,
        Date      = clock.Today
    };
}
```

Never call `DateTime.UtcNow`, `DateTime.Now`, `DateTimeOffset.UtcNow`, or `DateTime.Today` from a service. Always go through `IClock`.

## 3. Time Zones

```csharp
var paris = TimeZoneInfo.FindSystemTimeZoneById("Europe/Paris");
DateTimeOffset parisNow = clock.Now(paris);
```

`Now(TimeZoneInfo)` converts `UtcNow` to the specified zone. Avoid `DateTimeOffset.Now` — it uses the server's local zone, which is almost never what you want.

## 4. Async Delay

```csharp
public async Task RetryAsync(CancellationToken ct)
{
    await _clock.Delay(TimeSpan.FromSeconds(5), ct);
    // 5 seconds have elapsed (or ct was cancelled)
}
```

Never use `Task.Delay(delay)` directly. Use `clock.Delay(delay, ct)` so tests can advance the fake clock instead of waiting for real time.

## 5. Cancellation With Timeout

```csharp
public async Task<string> FetchAsync(HttpClient http, string url)
{
    using var cts = _clock.CreateCancellationTokenSource(TimeSpan.FromSeconds(10));
    return await http.GetStringAsync(url, cts.Token);
}
```

Never construct `new CancellationTokenSource(timeout)` directly — go through the clock so the timeout is fakeable.

## 6. Timers

```csharp
var timer = _clock.CreateTimer(
    callback: _ => Poll(),
    state: null,
    dueTime: TimeSpan.FromMinutes(5),
    period: TimeSpan.FromMinutes(5));
```

Dispose the returned `ITimer` to stop the timer. Never construct `new Timer(...)` directly.

## 7. Escape Hatch — Raw `TimeProvider`

For APIs that take `TimeProvider` directly (e.g. `PeriodicTimer`, third-party libraries):

```csharp
var periodicTimer = new PeriodicTimer(TimeSpan.FromSeconds(1), _clock.TimeProvider);
```

This ensures the periodic timer uses the same clock as the rest of the code — including the fake in tests.

## 8. Testing — Pin Time, Then Advance

```csharp
var clock = new FakeClock(new DateTimeOffset(2025, 6, 15, 10, 0, 0, TimeSpan.Zero));
var service = new OrderService(clock, fakeRepo);

var order = service.Place(cart);
Assert.Equal(new DateTimeOffset(2025, 6, 15, 10, 0, 0, TimeSpan.Zero), order.PlacedAt);

clock.Advance(TimeSpan.FromHours(25));
Assert.True(clock.UtcNow > order.ExpiresAt);
```

## 9. Testing Timers

```csharp
var clock = new FakeClock();
var fired = false;

clock.CreateTimer(_ => fired = true, null, TimeSpan.FromSeconds(10), Timeout.InfiniteTimeSpan);
Assert.False(fired);

clock.Advance(TimeSpan.FromSeconds(10));
Assert.True(fired);
```

## 10. Testing Delays

```csharp
var clock = new FakeClock();
var delayTask = clock.Delay(TimeSpan.FromMinutes(5));
Assert.False(delayTask.IsCompleted);

clock.Advance(TimeSpan.FromMinutes(5));
await delayTask;
Assert.True(delayTask.IsCompleted);
```

## What NOT To Do

- **Don't call `DateTime.UtcNow`, `DateTime.Now`, or `DateTimeOffset.Now` in business code.** Always inject `IClock`.
- **Don't use `Task.Delay` without the clock.** Use `clock.Delay(...)`.
- **Don't construct `new CancellationTokenSource(timeout)` directly.** Use `clock.CreateCancellationTokenSource(...)`.
- **Don't construct `new Timer(...)` directly.** Use `clock.CreateTimer(...)`.
- **Don't share `FakeClock` instances across tests.** Each test creates its own.

## Anchor Package

[`Net/FrenchExDev/Clock/`](../../../Net/FrenchExDev/Clock/) — implementation reference.
