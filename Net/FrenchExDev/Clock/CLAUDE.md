# Clock — Claude Context

Thin abstraction over .NET's `TimeProvider` for testability — `IClock` (7 members), `SystemClock` for production, and a separate `.Testing` package providing `FakeClock` backed by `FakeTimeProvider`.

## Package docs
- [README](README.md)
- [Architecture](doc/ARCHITECTURE.md)
- [How-To](doc/HOW-TO.md)
- [Philosophy](doc/PHILOSOPHY.md)

## Relevant skills
- [CLOCK-ABSTRACTION](../../../Skills/Net/Programming/CLOCK-ABSTRACTION/PHILOSOPHY.md)
- [SOLID](../../../Skills/Net/Programming/SOLID/PHILOSOPHY.md)
- [HAND-WRITTEN-FAKES](../../../Skills/Net/Programming/HAND-WRITTEN-FAKES/PHILOSOPHY.md)
- [Solution Layout](../../../Skills/Net/Programming/SOLUTION-LAYOUT/ARCHITECTURE.md)
- [Central Package Management](../../../Skills/Net/Programming/CENTRAL-PACKAGE-MANAGEMENT/ARCHITECTURE.md)

## Solution
- `FrenchExDev.Net.Clock.slnx`

## Notes for Claude
- The core `Clock` package has **zero NuGet dependencies** — never add one. `Microsoft.Extensions.TimeProvider.Testing` belongs ONLY in `Clock.Testing`.
- `IClock` has 7 members because every removed member leads to a known bug pattern (see PHILOSOPHY.md). Don't trim it.
- `SystemClock` is a singleton via `SystemClock.Instance` (private constructor, no state). Register as singleton in DI.
- `FakeClock` is NOT a singleton — every test creates its own instance with its own timeline.
- Never call `DateTime.UtcNow`, `Task.Delay(delay)`, `new Timer(...)`, or `new CancellationTokenSource(timeout)` in business code — always go through `IClock`.
- 11 tests total: 3 for `SystemClock`, 8 for `FakeClock`.
