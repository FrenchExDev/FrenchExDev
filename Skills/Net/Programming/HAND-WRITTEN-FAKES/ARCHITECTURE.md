# HAND-WRITTEN-FAKES — Architecture

How a hand-written-fake test suite is organized.

## Folder Layout

Every test project has a `Fakes/` folder at its root:

```
test/
  YourLib.Tests/
    Fakes/
      FakeReportWriter.cs
      FakeCoverageParser.cs
      FakeMutationParser.cs
      FakeSolutionLoader.cs
      FakeClock.cs
    Helpers/
      RoslynTestHelper.cs
    SomeFeatureTests.cs
    AnotherFeatureTests.cs
```

The `Fakes/` folder is a peer to your test classes, not buried inside a `Common/` or `Infrastructure/` directory. Discoverability matters: a new contributor opening the test project should see all available fakes immediately.

## Visibility

Fakes are `internal sealed`:

- **internal** — they are test-only; nothing outside the test assembly should reference them.
- **sealed** — fakes are not designed for inheritance; subclassing them defeats the "small explicit class" goal.

```csharp
internal sealed class FakeCoverageParser : ICoverageReportParser { ... }
```

If you find yourself wanting to inherit a fake, you actually want a *factory method* on the existing fake (see below).

## The Three Fake Patterns

Hand-written fakes follow one of three shapes depending on what the test needs.

### Pattern 1 — The Stub (returns canned data)

The simplest form. Constructor takes the data to return. The interface method returns it.

```csharp
internal sealed class FakeCoverageParser : ICoverageReportParser
{
    private readonly CoverageReport? _report;

    public FakeCoverageParser(CoverageReport? report = null) => _report = report;

    public CoverageReport? TryParseGlobs(string baseDir, List<string>? globs) => _report;
}
```

Use when: the test only cares what the dependency *returns*.

### Pattern 2 — The Spy (captures arguments)

Implements the interface and exposes captured state via observable properties.

```csharp
internal sealed class FakeReportWriter : IReportWriter
{
    public QualityReport? LastReport    { get; private set; }
    public string?        LastOutputDir { get; private set; }
    public int            WriteCount    { get; private set; }

    public Task<string> WriteAsync(QualityReport report, string outputDir, CancellationToken ct = default)
    {
        LastReport    = report;
        LastOutputDir = outputDir;
        WriteCount++;
        return Task.FromResult(Path.Combine(outputDir, "fake-run"));
    }
}
```

Use when: the test cares what the dependency was *called with* (or how many times).

### Pattern 3 — The Programmable Fake (per-input behavior)

When different inputs need different outputs, expose a dictionary or a delegate.

```csharp
internal sealed class FakeRepository : IRepository
{
    private readonly Dictionary<Guid, Entity> _store = new();

    public FakeRepository Seed(Entity entity)
    {
        _store[entity.Id] = entity;
        return this;
    }

    public Task<Entity?> FindAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(_store.TryGetValue(id, out var e) ? e : null);

    public Task SaveAsync(Entity entity, CancellationToken ct = default)
    {
        _store[entity.Id] = entity;
        return Task.CompletedTask;
    }
}
```

Use when: the test exercises multiple paths through the dependency and needs realistic behavior.

## Factory Methods Over Constructors

When a fake has multiple "common" setups, expose them as static factory methods. This eliminates the per-test boilerplate of constructing canned data:

```csharp
internal sealed class FakeSolutionLoader : ISolutionLoader
{
    private readonly Solution _solution;

    public FakeSolutionLoader(Solution solution) => _solution = solution;

    public Task<Solution> LoadAsync(string solutionPath) => Task.FromResult(_solution);

    public static FakeSolutionLoader Empty()
    {
        var workspace = new AdhocWorkspace();
        return new FakeSolutionLoader(workspace.CurrentSolution);
    }

    public static FakeSolutionLoader WithSource(string source)
    {
        var project = RoslynTestHelper.CreateProject(source);
        return new FakeSolutionLoader(project.Solution);
    }
}
```

Tests then read clearly:

```csharp
var loader = FakeSolutionLoader.WithSource("public class Foo { }");
```

## Naming Conventions

- Class name: `Fake<InterfaceWithoutI>` (e.g. `IReportWriter` -> `FakeReportWriter`).
- Capture properties: `Last<Argument>` for the most recent value, `<Verb>Count` for invocation counts.
- Factory methods: short, declarative — `Empty()`, `WithSource(...)`, `Returning(...)`, `ThatThrows(...)`.

Avoid `Stub`, `Mock`, `Dummy` prefixes. Everything is a fake.

## Optional-Constructor Injection

Hand-written fakes pair naturally with constructor injection where dependencies are *optional with defaults*:

```csharp
public sealed class QualityEngine
{
    private readonly ISolutionLoader _solutionLoader;
    private readonly ICoverageReportParser _coverageParser;

    public QualityEngine(
        ISolutionLoader? solutionLoader = null,
        ICoverageReportParser? coverageParser = null)
    {
        _solutionLoader = solutionLoader ?? new MsBuildSolutionLoader();
        _coverageParser = coverageParser ?? new DefaultCoverageReportParser();
    }
}
```

Production callers pass nothing. Tests inject only the fakes they care about. No DI container required.

## The Fakes Are Reusable; The Tests Are Specific

A common mistake is to hand-write a fake inside the test class itself, then duplicate it across other test classes. Don't. Put every fake in `Fakes/` and reuse it. The test class instantiates the fake with its specific data; the fake itself is generic.

Bad:

```csharp
public class FeatureATests
{
    private class TestRepo : IRepository { /* ... */ }
}

public class FeatureBTests
{
    private class TestRepo : IRepository { /* ... */ }   // duplicate
}
```

Good:

```csharp
// Fakes/FakeRepository.cs — single source
internal sealed class FakeRepository : IRepository { /* ... */ }

// FeatureATests.cs
var repo = new FakeRepository().Seed(...);

// FeatureBTests.cs
var repo = new FakeRepository().Seed(...);
```

## Test Helpers vs Fakes

A `Helpers/` folder is fine for things that are not test doubles: Roslyn project builders, file-system fixture creators, cancellation token sources, etc. Keep these separate from `Fakes/` so the distinction stays clear.

```
test/
  YourLib.Tests/
    Fakes/      # implements production interfaces
    Helpers/    # builds test inputs / fixtures
```
