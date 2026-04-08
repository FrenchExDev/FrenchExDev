# CLOCK-ABSTRACTION — Architecture

## Project Layout

Two production projects plus tests:

```
Clock/
  src/
    Clock/                  Core: IClock + SystemClock (zero NuGet deps)
      IClock.cs
      SystemClock.cs
    Clock.Testing/          Fake: FakeClock (depends on FakeTimeProvider)
      FakeClock.cs
  test/
    Clock.Tests/            xUnit tests for both
```

The core library has **zero** NuGet dependencies. Only the Testing project pulls in `Microsoft.Extensions.TimeProvider.Testing`.

## `IClock` Surface — Minimum Viable

```csharp
public interface IClock
{
    DateTimeOffset UtcNow { get; }
    DateTimeOffset Now(TimeZoneInfo timeZone);
    DateOnly Today { get; }

    Task Delay(TimeSpan delay, CancellationToken ct = default);
    ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period);
    CancellationTokenSource CreateCancellationTokenSource(TimeSpan delay);

    TimeProvider TimeProvider { get; }   // escape hatch
}
```

Each member exists because leaving it out leads to a known bug pattern:

| Member | Bug it prevents |
|---|---|
| `UtcNow` | Calling `DateTime.UtcNow` directly (non-testable) |
| `Now(TimeZoneInfo)` | Calling `DateTimeOffset.Now` (uses server timezone, not user's) |
| `Today` (`DateOnly`) | `DateTime.Today.Date` returning a `DateTime` and losing the offset |
| `Delay` | Calling `Task.Delay` (real-world delay in tests) |
| `CreateTimer` | `new Timer(...)` (real-world timer in tests) |
| `CreateCancellationTokenSource(TimeSpan)` | `new CancellationTokenSource(timeout)` (real-world timeout in tests) |
| `TimeProvider` | Code that needs the full provider for `PeriodicTimer`, etc. |

## `SystemClock` — Production Implementation

Sealed class, singleton via a static `Instance` property. All members delegate to `TimeProvider.System`:

```csharp
public sealed class SystemClock : IClock
{
    public static SystemClock Instance { get; } = new();
    private SystemClock() { }

    public DateTimeOffset UtcNow => TimeProvider.System.GetUtcNow();
    public DateTimeOffset Now(TimeZoneInfo tz) => TimeZoneInfo.ConvertTime(UtcNow, tz);
    public DateOnly Today => DateOnly.FromDateTime(UtcNow.UtcDateTime);
    public Task Delay(TimeSpan delay, CancellationToken ct = default)
        => Task.Delay(delay, TimeProvider.System, ct);
    public ITimer CreateTimer(TimerCallback cb, object? state, TimeSpan dueTime, TimeSpan period)
        => TimeProvider.System.CreateTimer(cb, state, dueTime, period);
    public CancellationTokenSource CreateCancellationTokenSource(TimeSpan delay)
        => new(delay, TimeProvider.System);
    public TimeProvider TimeProvider => TimeProvider.System;
}
```

Thread-safe (no mutable state). Singleton-safe (private constructor). Allocation-free at the call site.

## `FakeClock` — Test Implementation

Same delegation pattern, but backed by `FakeTimeProvider` from `Microsoft.Extensions.Time.Testing`:

```csharp
public sealed class FakeClock : IClock
{
    private readonly FakeTimeProvider _provider;

    public FakeClock(DateTimeOffset? startTime = null)
        => _provider = new FakeTimeProvider(startTime ?? new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero));

    public DateTimeOffset UtcNow => _provider.GetUtcNow();
    public Task Delay(TimeSpan d, CancellationToken ct = default) => Task.Delay(d, _provider, ct);
    public TimeProvider TimeProvider => _provider;
    // ... etc.

    // Test-only members
    public void Advance(TimeSpan delta) => _provider.Advance(delta);
    public void SetUtcNow(DateTimeOffset newTime) => _provider.SetUtcNow(newTime);
    public FakeTimeProvider FakeTimeProvider => _provider;
}
```

The extra members (`Advance`, `SetUtcNow`, `FakeTimeProvider`) are not on the `IClock` interface — they exist only on the concrete fake. Tests call them directly.

## Dependency Graph

```
Clock                  (no NuGet deps)
  |
  +-- Clock.Testing    (refs Clock + Microsoft.Extensions.TimeProvider.Testing)
  |
  +-- Clock.Tests      (refs Clock + Clock.Testing + xUnit)
```

The split is non-negotiable: production binaries must never transitively depend on `Microsoft.Extensions.TimeProvider.Testing`.

## DI Registration

```csharp
services.AddSingleton<IClock>(SystemClock.Instance);
```

Singleton because `SystemClock` has no state.

## Testing Pattern — Time Does Not Advance Automatically

```csharp
var clock = new FakeClock(new DateTimeOffset(2025, 6, 15, 10, 0, 0, TimeSpan.Zero));

Assert.Equal(new DateTimeOffset(2025, 6, 15, 10, 0, 0, TimeSpan.Zero), clock.UtcNow);

clock.Advance(TimeSpan.FromHours(2));
Assert.Equal(new DateTimeOffset(2025, 6, 15, 12, 0, 0, TimeSpan.Zero), clock.UtcNow);
```

Timers don't fire until `Advance` moves time past their `dueTime`. Delay tasks don't complete until time has advanced past them. Cancellation tokens with timeouts don't cancel until time advances past the timeout.

## Anchor Package

[`Net/FrenchExDev/Clock/`](../../../Net/FrenchExDev/Clock/) — full implementation with 11 tests covering both `SystemClock` and `FakeClock`.
