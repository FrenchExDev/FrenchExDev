# Clock -- Developer Guide (HOW-TO)

## Table of Contents

1. [DI Registration](#1-di-registration)
2. [Basic Time Queries](#2-basic-time-queries)
3. [Time Zones](#3-time-zones)
4. [Timers](#4-timers)
5. [Delays](#5-delays)
6. [Cancellation with Timeout](#6-cancellation-with-timeout)
7. [Escape Hatch: TimeProvider](#7-escape-hatch-timeprovider)
8. [Testing with FakeClock](#8-testing-with-fakeclock)
9. [Testing Timers](#9-testing-timers)
10. [Testing Delays](#10-testing-delays)
11. [Running Tests](#11-running-tests)

---

## 1. DI Registration

```csharp
// Register as singleton — SystemClock has no mutable state
services.AddSingleton<IClock>(SystemClock.Instance);
```

Or without DI:

```csharp
IClock clock = SystemClock.Instance;
```

---

## 2. Basic Time Queries

```csharp
public class AuditService(IClock clock)
{
    public AuditEntry CreateEntry(string action) => new()
    {
        Action = action,
        Timestamp = clock.UtcNow,
        Date = clock.Today
    };
}
```

---

## 3. Time Zones

```csharp
var paris = TimeZoneInfo.FindSystemTimeZoneById("Europe/Paris");
DateTimeOffset parisNow = clock.Now(paris);

var tokyo = TimeZoneInfo.FindSystemTimeZoneById("Asia/Tokyo");
DateTimeOffset tokyoNow = clock.Now(tokyo);
```

`Now(TimeZoneInfo)` converts `UtcNow` to the specified time zone. The returned `DateTimeOffset` carries the correct offset.

---

## 4. Timers

```csharp
// Fire once after 30 seconds
var timer = clock.CreateTimer(
    callback: _ => Console.WriteLine("Fired!"),
    state: null,
    dueTime: TimeSpan.FromSeconds(30),
    period: Timeout.InfiniteTimeSpan);

// Fire every 5 minutes
var recurring = clock.CreateTimer(
    callback: _ => PollForUpdates(),
    state: null,
    dueTime: TimeSpan.FromMinutes(5),
    period: TimeSpan.FromMinutes(5));
```

The returned `ITimer` is disposable. Dispose it to stop the timer.

---

## 5. Delays

```csharp
public async Task WaitAndRetryAsync(IClock clock, CancellationToken ct)
{
    await clock.Delay(TimeSpan.FromSeconds(5), ct);
    // 5 seconds have passed (or ct was cancelled)
}
```

Uses `Task.Delay(TimeSpan, TimeProvider, CancellationToken)` internally -- FakeClock delays only complete when you call `Advance()`.

---

## 6. Cancellation with Timeout

```csharp
public async Task<string> FetchWithTimeoutAsync(IClock clock, HttpClient http, string url)
{
    using var cts = clock.CreateCancellationTokenSource(TimeSpan.FromSeconds(10));
    return await http.GetStringAsync(url, cts.Token);
}
```

The `CancellationTokenSource` respects the clock's `TimeProvider`. In tests with `FakeClock`, the timeout only fires when you advance past it.

---

## 7. Escape Hatch: TimeProvider

For APIs that take `TimeProvider` directly (e.g., `IAsyncEnumerable.WithCancellation`, `PeriodicTimer`):

```csharp
var periodicTimer = new PeriodicTimer(TimeSpan.FromSeconds(1), clock.TimeProvider);
```

This ensures the periodic timer uses the same clock as the rest of your code.

---

## 8. Testing with FakeClock

### Basic assertions

```csharp
var clock = new FakeClock(new DateTimeOffset(2025, 6, 15, 10, 0, 0, TimeSpan.Zero));

Assert.Equal(new DateTimeOffset(2025, 6, 15, 10, 0, 0, TimeSpan.Zero), clock.UtcNow);
Assert.Equal(new DateOnly(2025, 6, 15), clock.Today);

clock.Advance(TimeSpan.FromHours(2));
Assert.Equal(new DateTimeOffset(2025, 6, 15, 12, 0, 0, TimeSpan.Zero), clock.UtcNow);
```

### Default start time

```csharp
var clock = new FakeClock(); // starts at 2024-01-01T00:00:00Z
```

### Jumping to a specific time

```csharp
clock.SetUtcNow(new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero));
```

---

## 9. Testing Timers

```csharp
var clock = new FakeClock();
var fired = false;

clock.CreateTimer(_ => fired = true, null, TimeSpan.FromSeconds(10), Timeout.InfiniteTimeSpan);
Assert.False(fired);

clock.Advance(TimeSpan.FromSeconds(10));
Assert.True(fired);
```

Timers don't fire until you advance the clock. This makes timer-dependent code fully deterministic in tests.

---

## 10. Testing Delays

```csharp
var clock = new FakeClock();
var delayTask = clock.Delay(TimeSpan.FromMinutes(5));

Assert.False(delayTask.IsCompleted);

clock.Advance(TimeSpan.FromMinutes(5));
await delayTask;

Assert.True(delayTask.IsCompleted);
```

---

## 11. Running Tests

```bash
dotnet test Clock/FrenchExDev.Net.Clock.slnx

# With quality gate
dotnet quality-gate test --config Clock/quality-gate.yml
```

Test suite: 11 xUnit tests covering `SystemClock` (3 tests) and `FakeClock` (8 tests).
