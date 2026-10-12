# HAND-WRITTEN-FAKES — Requirements

Hard requirements for any test suite that adopts the hand-written-fake style.

## R1 — No Mocking Framework Packages

The test project must not reference any of:

- `Moq`
- `NSubstitute`
- `FakeItEasy`
- `Microsoft.QualityTools.Testing.Fakes`
- `JustMock`

**Test**: `dotnet list package` for the test assembly returns zero matches against the list above.

## R2 — A `Fakes/` Folder Exists

Each test project that needs test doubles has a top-level `Fakes/` folder containing one fake per file.

**Test**: every fake class lives at `test/<Project>.Tests/Fakes/Fake<Name>.cs`.

## R3 — Every Fake Is `internal sealed`

Fakes are not part of the public API and are not designed for inheritance.

**Test**: every class under `Fakes/` declares `internal sealed`.

## R4 — Every Fake Implements a Production Interface Directly

A fake is not a wrapper around another fake, not a subclass of a production type, and not a `Mock<T>` in disguise. It implements the production interface directly.

**Test**: every fake's base list contains exactly one production interface and no class.

## R5 — Spy Fakes Expose State as Public Get / Private Set

Captured arguments and call counts are exposed as public read-only properties:

```csharp
public QualityReport? LastReport    { get; private set; }
public int            WriteCount    { get; private set; }
```

No `Verify(...)` calls. No reflection. The test reads the property.

**Test**: every spy fake exposes capture fields as `public` properties with `private set`.

## R6 — Stub Fakes Take Canned Data via Constructor

A stub fake takes its return value as a constructor argument (with sensible default):

```csharp
public FakeCoverageParser(CoverageReport? report = null) => _report = report;
```

**Test**: every stub fake's constructor takes the canned data; no setter-based setup.

## R7 — Common Setups Are Static Factory Methods on the Fake

If two or more test classes need the same canned setup, it lives as a `public static` factory on the fake, not duplicated inline.

**Test**: a grep for `new Fake<X>(...)` shows fewer raw constructions than factory-method calls when scenarios repeat.

## R8 — Fakes Are Reused Across Test Classes

Fakes are not nested types inside test classes. They live in `Fakes/` and are referenced by all tests in the project that need them.

**Test**: no `private class Fake*` declarations inside test classes.

## R9 — Production Code Accepts Optional Constructor Dependencies

The system under test takes its dependencies as **optional** constructor parameters with concrete defaults:

```csharp
public QualityEngine(
    IReportWriter? reportWriter = null,
    ICoverageReportParser? coverageParser = null)
{
    _reportWriter   = reportWriter   ?? new DefaultReportWriter();
    _coverageParser = coverageParser ?? new DefaultCoverageReportParser();
}
```

**Test**: production callers can construct the type with no arguments; tests inject only the fakes they need.

## R10 — Interfaces Stay Narrow

If hand-writing a fake costs more than ~30 lines, the interface is too wide. Split it.

**Test**: every fake under `Fakes/` is at most ~30 lines (excluding factory methods).

## R11 — Helpers Are Separate from Fakes

Test helpers that build inputs (Roslyn project builders, fixture creators, fake clocks for unrelated time setup) live under `Helpers/`, not `Fakes/`. Only production-interface implementations live in `Fakes/`.

**Test**: every file under `Fakes/` declares a class that implements at least one production interface.

## R12 — A Fake Is Debuggable

A developer can set a breakpoint inside any fake method and observe the exact arguments and the exact return path. No proxy generation, no runtime weaving, no IL emission stands between the test and the fake.

**Test**: stepping into a fake method in the debugger lands on the fake's source line, not on a generated proxy.
