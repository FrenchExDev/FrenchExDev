# CLOCK-ABSTRACTION — Requirements

## Interface Surface

- [ ] Define `IClock` with `UtcNow`, `Now(TimeZoneInfo)`, `Today` (`DateOnly`), `Delay`, `CreateTimer`, `CreateCancellationTokenSource`, and a `TimeProvider` escape hatch.
- [ ] All time-dependent operations route through this interface — no `Task.Delay`, `new Timer`, or `new CancellationTokenSource(timeout)` in business code.
- [ ] `Today` returns `DateOnly`, not `DateTime`.

## Production Implementation

- [ ] `SystemClock` is a `sealed class` with a static `Instance` singleton property and a private constructor.
- [ ] All members delegate to `TimeProvider.System`.
- [ ] Zero mutable state (singleton-safe).
- [ ] Zero NuGet dependencies in the core project.

## Test Implementation

- [ ] `FakeClock` lives in a **separate** `.Testing` package, not the production assembly.
- [ ] `FakeClock` wraps `Microsoft.Extensions.Time.Testing.FakeTimeProvider`.
- [ ] `FakeClock` exposes `Advance(TimeSpan)`, `SetUtcNow(DateTimeOffset)` as public test-only members (not on `IClock`).
- [ ] `FakeClock` accepts an optional start time in its constructor; default is a fixed sentinel like `2024-01-01T00:00:00Z`.
- [ ] Each test creates its own `FakeClock` — fakes are NOT singletons.

## Behavior Requirements

- [ ] `FakeClock.Delay(...)` only completes when `Advance` moves time past the delay.
- [ ] `FakeClock.CreateTimer(...)` only fires when `Advance` moves time past the timer's `dueTime`.
- [ ] `FakeClock.CreateCancellationTokenSource(timeout)` only cancels when time advances past the timeout.
- [ ] `Now(TimeZoneInfo)` converts from `UtcNow`, never reads the server's local zone.

## Multi-Targeting

- [ ] Core targets `net10.0` (or current LTS).
- [ ] Testing package targets the same.

## DI Registration

- [ ] Documented as `services.AddSingleton<IClock>(SystemClock.Instance)`.
- [ ] Never registered as scoped or transient.

## What MUST NOT Be Done

- [ ] Production code MUST NOT call `DateTime.UtcNow`, `DateTime.Now`, `DateTimeOffset.UtcNow`, `DateTimeOffset.Now`, or `DateTime.Today` outside the `SystemClock` implementation.
- [ ] Production code MUST NOT call `Task.Delay`, `new Timer(...)`, or `new CancellationTokenSource(TimeSpan)` directly.
- [ ] The core `Clock` package MUST NOT depend on `Microsoft.Extensions.TimeProvider.Testing` or any test-only library.
- [ ] `FakeClock` MUST NOT be exposed via `IClock` — it lives only in the `.Testing` package.

## Testing

- [ ] Tests for `SystemClock`: assert it returns values in the expected ranges (no exact equality on wall-clock time).
- [ ] Tests for `FakeClock`: assert deterministic behavior of `Advance`, timer firing, delay completion, cancellation timeout.

## Anchor Package

[`Net/FrenchExDev/Clock/`](../../../Net/FrenchExDev/Clock/) — implements every requirement.
