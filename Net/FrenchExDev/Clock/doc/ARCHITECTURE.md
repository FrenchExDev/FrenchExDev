# Clock -- Architecture

## 1. Overview

Clock provides a testable abstraction over .NET's time infrastructure. The `IClock` interface exposes time queries (`UtcNow`, `Today`, `Now`), time-based operations (`Delay`, `CreateTimer`, `CreateCancellationTokenSource`), and the underlying `TimeProvider` for advanced scenarios. Two implementations exist: `SystemClock` for production and `FakeClock` for deterministic tests.

---

## 2. Project Structure

```
Clock/
  FrenchExDev.Net.Clock.slnx
  quality-gate.yml
  src/
    FrenchExDev.Net.Clock/                  (Core library — no dependencies)
      IClock.cs                             Interface: 7 members
      SystemClock.cs                        Production impl: delegates to TimeProvider.System
    FrenchExDev.Net.Clock.Testing/          (Test helper — refs Clock + FakeTimeProvider)
      FakeClock.cs                          Test impl: deterministic time via FakeTimeProvider
  test/
    FrenchExDev.Net.Clock.Tests/            (xUnit tests)
      SystemClockTests.cs                   3 tests for SystemClock
      FakeClockTests.cs                     8 tests for FakeClock
```

---

## 3. Dependency Graph

```
FrenchExDev.Net.Clock               (no dependencies, net10.0)
  |
  +-- FrenchExDev.Net.Clock.Testing  (refs Clock + Microsoft.Extensions.TimeProvider.Testing)
  |
  +-- FrenchExDev.Net.Clock.Tests    (refs Clock + Clock.Testing + xUnit)
```

The core library has zero NuGet dependencies. Only the Testing project pulls in `Microsoft.Extensions.TimeProvider.Testing` for `FakeTimeProvider`.

---

## 4. API Surface

### IClock (7 members)

| Member | Return Type | Purpose |
|--------|-------------|---------|
| `UtcNow` | `DateTimeOffset` | Current UTC date and time |
| `Now(TimeZoneInfo)` | `DateTimeOffset` | Current time in a specific time zone |
| `Today` | `DateOnly` | Current UTC date (midnight) |
| `CreateTimer(...)` | `ITimer` | Timer that fires after delay |
| `CreateCancellationTokenSource(TimeSpan)` | `CancellationTokenSource` | Auto-cancels after delay |
| `Delay(TimeSpan, CancellationToken)` | `Task` | Async delay |
| `TimeProvider` | `TimeProvider` | Underlying provider for escape hatch |

### SystemClock

- Sealed class, singleton via `SystemClock.Instance`
- All members delegate to `TimeProvider.System`
- Thread-safe (no mutable state)

### FakeClock (IClock + 3 test members)

| Extra Member | Purpose |
|-------------|---------|
| `Advance(TimeSpan)` | Move time forward, fire pending timers |
| `SetUtcNow(DateTimeOffset)` | Jump to a specific point in time |
| `FakeTimeProvider` | Access the underlying `FakeTimeProvider` directly |

---

## 5. Implementation Details

### SystemClock

```
UtcNow              → TimeProvider.System.GetUtcNow()
Now(tz)             → TimeZoneInfo.ConvertTime(UtcNow, tz)
Today               → DateOnly.FromDateTime(UtcNow.UtcDateTime)
CreateTimer(...)    → TimeProvider.System.CreateTimer(...)
CreateCancellationTokenSource(delay) → new CancellationTokenSource(delay, TimeProvider.System)
Delay(delay, ct)    → Task.Delay(delay, TimeProvider.System, ct)
```

### FakeClock

Same delegation pattern, but backed by `FakeTimeProvider` from `Microsoft.Extensions.Time.Testing`. Time does not advance automatically -- it only moves when the test calls `Advance()` or `SetUtcNow()`.

---

## 6. Ecosystem Position

```
FrenchExDev.Net ecosystem:
  Clock          ← time abstraction (this project)
  Result         ← error handling
  Builder        ← validated object construction
  Injectable     ← DI registration
  Guard          ← argument validation
  ...

Any project needing time:
  → depends on Clock (interface only)
  → test project depends on Clock.Testing (FakeClock)
```

Clock is a leaf dependency -- it depends on nothing in the FrenchExDev ecosystem. Other projects depend on it when they need testable time.
