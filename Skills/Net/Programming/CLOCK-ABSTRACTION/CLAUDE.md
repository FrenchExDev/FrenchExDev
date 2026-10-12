# CLOCK-ABSTRACTION — Claude Context

Inject time as a dependency via `IClock` interface. All time operations route through it: `UtcNow`, `Now(TimeZoneInfo)`, `Today`, `Delay`, `CreateTimer`, `CreateCancellationTokenSource`. `SystemClock` (singleton) for production; `FakeClock` (per-test, deterministic) for testing.

## Skill docs
- [Philosophy](PHILOSOPHY.md) — design rationale and trade-offs
- [Architecture](ARCHITECTURE.md) — internal structure
- [How-To](HOW-TO.md) — step-by-step tasks
- [Requirements](REQUIREMENTS.md) — formal requirements

## Related skills
- [RESULT-PATTERN](../RESULT-PATTERN/) — Result integration
- [GUARD-CLAUSES](../GUARD-CLAUSES/) — preconditions
- [RESILIENCE-CONVENTIONS](../RESILIENCE-CONVENTIONS.md) — timeout integration

## Related packages
- [`FrenchExDev.Net.Clock`](../../../../Net/FrenchExDev/Clock/)

## Notes for Claude
- NEVER call `DateTime.UtcNow`, `DateTime.Now`, `DateTimeOffset.UtcNow`, or `DateTime.Today` directly
- NEVER call `Task.Delay` directly — use `clock.Delay(delay, ct)`
- NEVER construct `new Timer(...)` directly — use `clock.CreateTimer(...)`
- NEVER construct `new CancellationTokenSource(timeout)` — use `clock.CreateCancellationTokenSource(delay)`
- `FakeClock.Delay()` only completes when `Advance()` moves time past it — no wallclock delay
- `FakeClock` is NOT singleton — each test creates its own instance with its own timeline
- DI registration: `services.AddSingleton<IClock>(SystemClock.Instance)`
