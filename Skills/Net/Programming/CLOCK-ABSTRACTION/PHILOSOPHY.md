# CLOCK-ABSTRACTION — Philosophy

Time is an input. Treat it like every other input: inject it, control it in tests, never call `DateTime.UtcNow` directly from business logic.

## The `DateTime.UtcNow` Problem

A test that calls `DateTime.UtcNow` is non-deterministic. It passes today, fails at midnight, flakes during DST transitions, and races against the system clock during overnight CI runs. A test that depends on "wait 5 seconds" is slow and fragile.

The fix is to make time a controlled input. The production code receives an `IClock`. The production wiring binds it to a `SystemClock`. The test wiring binds it to a `FakeClock` whose time only advances when the test says so.

## `IClock` Over Raw `TimeProvider`

.NET 8 introduced `TimeProvider`. It's good. But it exposes 5 virtual methods plus `LocalTimeZone`, `GetTimestamp()`, and `TimestampFrequency` — most of which are irrelevant to application code that just wants "what time is it?" and "wait N seconds."

A custom `IClock` interface defines the small surface application code actually uses:

```csharp
public interface IClock
{
    DateTimeOffset UtcNow { get; }
    DateTimeOffset Now(TimeZoneInfo timeZone);
    DateOnly Today { get; }
    Task Delay(TimeSpan delay, CancellationToken ct = default);
    ITimer CreateTimer(TimerCallback cb, object? state, TimeSpan dueTime, TimeSpan period);
    CancellationTokenSource CreateCancellationTokenSource(TimeSpan delay);
    TimeProvider TimeProvider { get; }   // escape hatch
}
```

The interface communicates what the code actually needs from time. Code that needs the full `TimeProvider` surface uses the `TimeProvider` escape hatch — most code doesn't.

## Every Time Operation Goes Through The Clock

It is tempting to expose only `UtcNow` and let callers compose everything else (`Task.Delay`, `CancellationTokenSource(timeout)`, `new Timer(...)`). **Don't.**

If `Delay`, `CreateTimer`, and `CreateCancellationTokenSource` aren't on the interface, someone will eventually call `Task.Delay(delay)` directly and bypass the fake. The test that depends on "5 seconds elapsed" will hang for real-world 5 seconds — or worse, it will pass at first because the assertion fires before the delay matters, then start failing months later when a refactor changes the timing.

Every time-dependent operation must funnel through `IClock`. The fake controls *all* of them.

## Separate Testing Package

The fake clock depends on `Microsoft.Extensions.TimeProvider.Testing` (a test-only package). If the fake lived in the main `Clock` assembly, every production consumer would transitively pull in a test library.

Splitting the implementation into two packages — `Clock` (production, zero deps) and `Clock.Testing` (test, depends on `FakeTimeProvider`) — keeps production binaries clean. The testing package is a first-class deliverable, not an afterthought hidden in a test project.

## Singleton For Production, Fresh Instance For Tests

`SystemClock` is a singleton (`SystemClock.Instance`). It has no fields, no mutable state, no configuration. Two instances would behave identically. A singleton eliminates allocation noise and makes the "production clock" unambiguous.

`FakeClock` is **not** a singleton — each test creates its own instance with its own start time and its own timeline. This isolation prevents test interference: parallel tests don't share clock state.

## Deterministic Time, Not Wallclock Time

In a test, time does not advance unless the test says so:

```csharp
var clock = new FakeClock(new DateTimeOffset(2025, 6, 15, 10, 0, 0, TimeSpan.Zero));
var service = new OrderService(clock);

var order = service.Place(cart);
Assert.Equal(new DateTimeOffset(2025, 6, 15, 10, 0, 0, TimeSpan.Zero), order.PlacedAt);

clock.Advance(TimeSpan.FromHours(25));
Assert.True(order.IsExpired(clock.UtcNow));   // 24-hour TTL, deterministic
```

`Advance(TimeSpan.FromHours(24))` simulates a day passing in microseconds. Timers fire synchronously when time is advanced, not asynchronously in the background. `Delay()` tasks complete only when the clock moves past them.

The test controls time. Time does not control the test.

## Anchor Package

[`Net/FrenchExDev/Clock/`](../../../Net/FrenchExDev/Clock/) — `IClock` (7 members), `SystemClock` (singleton, zero deps), `FakeClock` (in a separate `.Testing` package).
