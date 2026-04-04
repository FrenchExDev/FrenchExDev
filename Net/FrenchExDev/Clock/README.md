# Clock

Thin abstraction over .NET's `TimeProvider` for testability. Wraps time-related operations (`UtcNow`, `Today`, `Delay`, `CreateTimer`, `CreateCancellationTokenSource`) behind `IClock`, with a `SystemClock` for production and a `FakeClock` for deterministic time control in tests.

## Quick Start

```csharp
// Production — inject via DI
services.AddSingleton<IClock>(SystemClock.Instance);

// In your service
public class OrderService(IClock clock)
{
    public Order Place(Cart cart) => new()
    {
        PlacedAt = clock.UtcNow,
        ExpiresAt = clock.UtcNow.AddHours(24)
    };
}

// Tests — deterministic time
var clock = new FakeClock(new DateTimeOffset(2025, 6, 15, 10, 0, 0, TimeSpan.Zero));
var svc = new OrderService(clock);
var order = svc.Place(cart);
Assert.Equal(new DateTimeOffset(2025, 6, 15, 10, 0, 0, TimeSpan.Zero), order.PlacedAt);

clock.Advance(TimeSpan.FromHours(25));
Assert.True(clock.UtcNow > order.ExpiresAt); // deterministic expiration check
```

## Projects

| Project | TFM | Purpose |
|---------|-----|---------|
| `Clock` | net10.0 | `IClock` interface + `SystemClock` production implementation |
| `Clock.Testing` | net10.0 | `FakeClock` backed by `FakeTimeProvider` for deterministic tests |
| `Clock.Tests` | net10.0 | xUnit tests for both implementations |

## Key Design Decisions

- **Wraps `TimeProvider`, does not replace it** -- `IClock.TimeProvider` exposes the underlying provider for code that needs it directly
- **No external dependencies** -- the core library has zero NuGet references
- **`FakeClock` in a separate package** -- production code never references `Microsoft.Extensions.TimeProvider.Testing`
- **Singleton-safe** -- `SystemClock.Instance` is a static readonly field, safe to share across threads

## Documentation

- [ARCHITECTURE.md](doc/ARCHITECTURE.md) -- project structure, dependency graph, API surface
- [HOW-TO.md](doc/HOW-TO.md) -- DI registration, time zones, timers, delays, testing patterns
- [PHILOSOPHY.md](doc/PHILOSOPHY.md) -- why IClock over raw TimeProvider, why a separate Testing package

## Building

```bash
dotnet build Clock/FrenchExDev.Net.Clock.slnx
dotnet test Clock/FrenchExDev.Net.Clock.slnx
```
