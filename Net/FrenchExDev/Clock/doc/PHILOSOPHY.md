# Clock -- Philosophy

## IClock over raw TimeProvider

.NET 8 introduced `TimeProvider` as the official time abstraction. It's good. But it exposes 5 virtual methods, a `LocalTimeZone` property, and a `TimestampFrequency` property -- most of which are irrelevant to application code that just needs "what time is it?" and "wait N seconds."

`IClock` is a simpler contract: 4 time queries and 3 time-based operations. No `GetTimestamp()`. No `TimestampFrequency`. No `LocalTimeZone` property (use `Now(TimeZoneInfo)` explicitly instead). The interface communicates what your code actually needs from time.

If you need the full `TimeProvider` surface, `IClock.TimeProvider` is the escape hatch. But most code doesn't.

---

## Separate Testing package, not a test class in the main assembly

`FakeClock` depends on `Microsoft.Extensions.TimeProvider.Testing` -- a test-only package. If it lived in the main `Clock` assembly, every production consumer would transitively pull in a test library.

Splitting it into `Clock.Testing` means:
- Production code references only `Clock` (zero dependencies)
- Test code references `Clock.Testing` (pulls in `FakeTimeProvider`)
- The testing package is a first-class deliverable, not an afterthought hidden in the test project

This is the same split used by `Builder` / `Builder.Testing` and `Result` in the FrenchExDev ecosystem.

---

## Deterministic time over wallclock time in tests

Tests that call `DateTime.UtcNow` are non-deterministic. They pass today, fail at midnight, flake during DST transitions, and race against the system clock. Tests that depend on "wait 5 seconds" are slow and fragile.

`FakeClock` makes time a controlled input:
- Time does not advance unless the test says so
- `Advance(TimeSpan.FromHours(24))` simulates a day passing in microseconds
- Timers fire synchronously when time is advanced, not asynchronously in the background
- `Delay()` tasks complete only when the clock moves past them

The test controls time. Time does not control the test.

---

## Singleton because time has no state

`SystemClock` is a singleton (`SystemClock.Instance`). It has no fields, no mutable state, no configuration. Two instances would behave identically. A singleton eliminates allocation noise and makes the "production clock" unambiguous.

`FakeClock` is not a singleton -- each test creates its own instance with its own start time and its own timeline. This isolation prevents test interference.

---

## Seven members, not fewer

It's tempting to reduce `IClock` to just `UtcNow` and derive everything else. But:

- `Today` as `DateOnly` avoids the `UtcNow.Date` mistake (which returns `DateTime`, not `DateOnly`, and loses the offset)
- `Now(TimeZoneInfo)` prevents the common error of calling `DateTimeOffset.Now` (which uses the server's local timezone, not the user's)
- `Delay`, `CreateTimer`, and `CreateCancellationTokenSource` must go through the same `TimeProvider` as the clock -- if they're not on the interface, someone will call `Task.Delay(delay)` directly and bypass the fake in tests

Every member exists because leaving it out leads to a known bug pattern.
