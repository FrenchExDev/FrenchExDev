# HAND-WRITTEN-FAKES — How-To

Step-by-step recipes for replacing mocking frameworks with hand-written fakes.

## 1. Create a Stub Fake (returns canned data)

For an interface that just returns a value:

```csharp
public interface ICoverageReportParser
{
    CoverageReport? TryParseGlobs(string baseDir, List<string>? globs);
}
```

Create `Fakes/FakeCoverageParser.cs`:

```csharp
internal sealed class FakeCoverageParser : ICoverageReportParser
{
    private readonly CoverageReport? _report;
    public FakeCoverageParser(CoverageReport? report = null) => _report = report;
    public CoverageReport? TryParseGlobs(string baseDir, List<string>? globs) => _report;
}
```

Use it:

```csharp
var parser = new FakeCoverageParser(new CoverageReport(85.0, 12, 100));
var engine = new QualityEngine(coverageParser: parser);
```

## 2. Create a Spy Fake (captures arguments)

For an interface where the test needs to know *how* it was called:

```csharp
public interface IReportWriter
{
    Task<string> WriteAsync(QualityReport report, string outputDir, CancellationToken ct = default);
}
```

Create `Fakes/FakeReportWriter.cs`:

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

Assert against captured state:

```csharp
[Fact]
public async Task Run_writes_report_once()
{
    var writer = new FakeReportWriter();
    var engine = new QualityEngine(reportWriter: writer);

    await engine.RunAsync();

    Assert.Equal(1, writer.WriteCount);
    Assert.NotNull(writer.LastReport);
    Assert.Equal("./output", writer.LastOutputDir);
}
```

## 3. Create a Programmable Fake (per-input behavior)

When the dependency must behave like a small in-memory state store:

```csharp
internal sealed class FakeRepository<T> : IRepository<T> where T : IHasId
{
    private readonly Dictionary<Guid, T> _store = new();

    public FakeRepository<T> Seed(params T[] entities)
    {
        foreach (var e in entities) _store[e.Id] = e;
        return this;
    }

    public Task<T?> FindAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(_store.TryGetValue(id, out var e) ? e : default);

    public Task SaveAsync(T entity, CancellationToken ct = default)
    {
        _store[entity.Id] = entity;
        return Task.CompletedTask;
    }

    public IReadOnlyCollection<T> All => _store.Values;
}
```

Use the fluent `Seed`:

```csharp
var repo = new FakeRepository<Order>().Seed(
    new Order { Id = Guid.NewGuid(), Total = 10m },
    new Order { Id = Guid.NewGuid(), Total = 20m });
```

## 4. Add Static Factory Methods

When the same canned setup appears in multiple tests, lift it onto the fake:

```csharp
internal sealed class FakeSolutionLoader : ISolutionLoader
{
    private readonly Solution _solution;
    public FakeSolutionLoader(Solution solution) => _solution = solution;
    public Task<Solution> LoadAsync(string path) => Task.FromResult(_solution);

    public static FakeSolutionLoader Empty()
    {
        var ws = new AdhocWorkspace();
        return new FakeSolutionLoader(ws.CurrentSolution);
    }

    public static FakeSolutionLoader WithSource(string source)
    {
        var project = RoslynTestHelper.CreateProject(source);
        return new FakeSolutionLoader(project.Solution);
    }
}
```

Then tests read like prose:

```csharp
var loader = FakeSolutionLoader.WithSource("public class Foo { }");
```

## 5. Make a Fake That Throws

For testing error paths:

```csharp
internal sealed class FakeReportWriter : IReportWriter
{
    private readonly Exception? _throwOnWrite;

    public FakeReportWriter(Exception? throwOnWrite = null) => _throwOnWrite = throwOnWrite;

    public Task<string> WriteAsync(QualityReport report, string outputDir, CancellationToken ct = default)
    {
        if (_throwOnWrite is not null) throw _throwOnWrite;
        return Task.FromResult(Path.Combine(outputDir, "fake-run"));
    }

    public static FakeReportWriter ThatThrows(Exception ex) => new(ex);
}
```

## 6. Wire the Fake via Optional Constructor Param

Production code accepts dependencies as optional with defaults:

```csharp
public sealed class QualityEngine
{
    private readonly IReportWriter _reportWriter;

    public QualityEngine(IReportWriter? reportWriter = null)
    {
        _reportWriter = reportWriter ?? new DefaultReportWriter();
    }
}
```

Test injects exactly the fakes it needs:

```csharp
var writer = new FakeReportWriter();
var engine = new QualityEngine(reportWriter: writer);
```

## 7. Migrate from Moq

Replace this:

```csharp
var writerMock = new Mock<IReportWriter>();
writerMock
    .Setup(w => w.WriteAsync(It.IsAny<QualityReport>(), It.IsAny<string>(), default))
    .ReturnsAsync("fake-run");

var engine = new QualityEngine(reportWriter: writerMock.Object);
await engine.RunAsync();

writerMock.Verify(
    w => w.WriteAsync(It.IsAny<QualityReport>(), "./output", default),
    Times.Once());
```

With this:

```csharp
var writer = new FakeReportWriter();
var engine = new QualityEngine(reportWriter: writer);
await engine.RunAsync();

Assert.Equal(1, writer.WriteCount);
Assert.Equal("./output", writer.LastOutputDir);
```

Lines saved: 6. Frameworks deleted: 1.

## 8. Migrate from NSubstitute

Replace:

```csharp
var parser = Substitute.For<ICoverageReportParser>();
parser.TryParseGlobs(Arg.Any<string>(), Arg.Any<List<string>?>())
      .Returns(new CoverageReport(85.0, 12, 100));
```

With:

```csharp
var parser = new FakeCoverageParser(new CoverageReport(85.0, 12, 100));
```

## 9. When the Interface Is Too Wide for a Cheap Fake

If implementing the interface produces a 50-line fake, the interface is the problem. Split it.

Before:

```csharp
public interface IUserService
{
    Task<User> GetByIdAsync(Guid id);
    Task<User> GetByEmailAsync(string email);
    Task CreateAsync(User user);
    Task UpdateAsync(User user);
    Task DeleteAsync(Guid id);
    Task<IReadOnlyList<User>> SearchAsync(string query);
    Task SendEmailAsync(Guid id, string subject, string body);
    Task ResetPasswordAsync(Guid id);
    // ... 7 more methods
}
```

After:

```csharp
public interface IUserReader { Task<User?> GetByIdAsync(Guid id); }
public interface IUserWriter { Task SaveAsync(User user); }
public interface IUserSearch { Task<IReadOnlyList<User>> SearchAsync(string query); }
public interface IUserMailer { Task SendEmailAsync(Guid id, string subject, string body); }
```

Each gets a 5-line fake. Each test depends only on the seam it cares about.

## 10. Common Anti-Patterns to Avoid

- **Inheriting one fake from another** — duplicate the class instead.
- **A "configurable" fake with 12 boolean knobs** — split it into per-scenario factory methods.
- **Putting fakes inside test classes as nested types** — move them to `Fakes/`.
- **Using `Mock<T>` "just for this one test"** — there is no "just one." Add the package, and the next person adds five more.
- **Asserting against `Substitute.Received().Method(...)`** — you have a fake; assert against captured fields.
