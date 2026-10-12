using System.Runtime.CompilerServices;
using CsCheck;
using FrenchExDev.Net.BinaryWrapper;
using FrenchExDev.Net.BinaryWrapper.Design;

namespace FrenchExDev.Net.BinaryWrapper.Testing;

// ── FakeProcessRunner ───────────────────────────────────────────────────────

/// <summary>
/// In-memory IProcessRunner for testing command execution without real processes.
/// </summary>
public sealed class FakeProcessRunner : IProcessRunner
{
    private readonly List<OutputLine> _lines;
    private readonly int _exitCode;
    private readonly string _stdout;
    private readonly string _stderr;

    public FakeProcessRunner(IEnumerable<OutputLine> lines, int exitCode = 0)
    {
        _lines = lines.ToList();
        _exitCode = exitCode;
        _stdout = string.Join("\n", _lines.Where(l => l.Source == OutputSource.StdOut).Select(l => l.Text));
        _stderr = string.Join("\n", _lines.Where(l => l.Source == OutputSource.StdErr).Select(l => l.Text));
    }

    public FakeProcessRunner(string stdout = "", string stderr = "", int exitCode = 0)
    {
        _stdout = stdout;
        _stderr = stderr;
        _exitCode = exitCode;
        _lines = stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => new OutputLine(l, OutputSource.StdOut))
            .Concat(stderr.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(l => new OutputLine(l, OutputSource.StdErr)))
            .ToList();
    }

    /// <summary>The last ProcessSpec passed to RunAsync or StreamAsync.</summary>
    public ProcessSpec? LastSpec { get; private set; }

    public async IAsyncEnumerable<OutputLine> StreamAsync(
        ProcessSpec spec,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        LastSpec = spec;
        foreach (var line in _lines)
        {
            await Task.Yield();
            yield return line;
        }
    }

    public Task<ProcessOutput> RunAsync(ProcessSpec spec, CancellationToken cancellationToken = default)
    {
        LastSpec = spec;
        return Task.FromResult(new ProcessOutput
        {
            ExitCode = _exitCode,
            StandardOutput = _stdout,
            StandardError = _stderr
        });
    }
}

// ── FakeCommand ─────────────────────────────────────────────────────────────

/// <summary>
/// Minimal ICliCommand implementation for testing.
/// </summary>
public sealed class FakeCommand : ICliCommand
{
    public IReadOnlyList<string> CommandPath { get; init; } = ["test"];
    public IReadOnlyList<string> Args { get; init; } = [];
    public IReadOnlyDictionary<string, string> Environment { get; init; } = new Dictionary<string, string>();
    public IReadOnlyList<string> ToArguments() => Args;
}

// ── TestEvent / TestOutputParser / TestCollector ────────────────────────────

/// <summary>Simple event type for testing output parsing pipelines.</summary>
public sealed record TestEvent(string Value);

/// <summary>
/// IOutputParser that emits TestEvent for lines starting with "EVENT:" and
/// a final TestEvent("EXIT:{exitCode}") on Complete.
/// </summary>
public sealed class TestOutputParser : IOutputParser<TestEvent>
{
    public IEnumerable<TestEvent> ParseLine(OutputLine line)
    {
        if (line.Text.StartsWith("EVENT:"))
            yield return new TestEvent(line.Text[6..]);
    }

    public IEnumerable<TestEvent> Complete(int exitCode)
    {
        yield return new TestEvent($"EXIT:{exitCode}");
    }
}

/// <summary>
/// Variant of TestOutputParser whose Complete returns no events.
/// </summary>
public sealed class EmptyCompleteParser : IOutputParser<TestEvent>
{
    public IEnumerable<TestEvent> ParseLine(OutputLine line)
    {
        if (line.Text.StartsWith("EVENT:"))
            yield return new TestEvent(line.Text[6..]);
    }

    public IEnumerable<TestEvent> Complete(int exitCode) => [];
}

/// <summary>
/// IResultCollector that joins TestEvent values with commas.
/// </summary>
public sealed class TestCollector : IResultCollector<TestEvent, string>
{
    private readonly List<string> _values = [];
    public void OnEvent(TestEvent @event) => _values.Add(@event.Value);
    public string Complete() => string.Join(",", _values);
}

// ── CsCheck Generators ─────────────────────────────────────────────────────

/// <summary>
/// Reusable CsCheck generators for BinaryWrapper types.
/// </summary>
public static class Gens
{
    /// <summary>Non-empty, non-whitespace string generator.</summary>
    public static readonly Gen<string> NonEmpty =
        Gen.String.Select(s => string.IsNullOrWhiteSpace(s) ? "a" : s);

    /// <summary>Arbitrary BinaryIdentifier (colons in name replaced with underscores).</summary>
    public static readonly Gen<BinaryIdentifier> AnyBinaryId =
        Gen.Select(NonEmpty, Gen.String)
           .Select((name, ver) => new BinaryIdentifier(
               name.Replace(":", "_"),
               string.IsNullOrWhiteSpace(ver) ? null : ver));

    /// <summary>Arbitrary OutputLine with random text and source.</summary>
    public static readonly Gen<OutputLine> AnyOutputLine =
        Gen.Select(Gen.String, Gen.Bool)
           .Select((t, isErr) => new OutputLine(
               t ?? "", isErr ? OutputSource.StdErr : OutputSource.StdOut));

    /// <summary>Arbitrary SemanticVersion with values 0-100.</summary>
    public static readonly Gen<SemanticVersion> AnyVersion =
        Gen.Select(Gen.Int[0, 100], Gen.Int[0, 100], Gen.Int[0, 100])
           .Select((ma, mi, pa) => new SemanticVersion(ma, mi, pa));
}

// ── TestBindings ────────────────────────────────────────────────────────────

/// <summary>
/// Factory methods for creating test BinaryBinding instances.
/// </summary>
public static class TestBindings
{
    /// <summary>Creates a simple BinaryBinding with the given name and /usr/bin/{name} path.</summary>
    public static BinaryBinding Create(string name) => new()
    {
        Identifier = new BinaryIdentifier(name),
        ExecutablePath = $"/usr/bin/{name}"
    };

    /// <summary>Creates a BinaryBinding with a detected version.</summary>
    public static BinaryBinding Create(string name, SemanticVersion detectedVersion) => new()
    {
        Identifier = new BinaryIdentifier(name),
        ExecutablePath = $"/usr/bin/{name}",
        DetectedVersion = detectedVersion
    };

    /// <summary>Creates a DictionaryBinaryResolver from a single binding.</summary>
    public static DictionaryBinaryResolver ResolverFor(string name) =>
        new([Create(name)]);

    /// <summary>Creates a DictionaryBinaryResolver from multiple bindings.</summary>
    public static DictionaryBinaryResolver ResolverFor(params BinaryBinding[] bindings) =>
        new(bindings);
}

// ── MockContainerRuntime ────────────────────────────────────────────────────

/// <summary>
/// In-memory IContainerRuntime for testing design-time scraping without real containers.
/// </summary>
public sealed class MockContainerRuntime(
    Func<string, string, Task<string>> onBuild,
    Func<string, string[], Task<string>> onRun,
    Func<string, Task> onRemove) : IContainerRuntime
{
    public Task<string> BuildAsync(string tag, string dockerfileContent,
        CancellationToken cancellationToken = default) => onBuild(tag, dockerfileContent);
    public Task<string> RunAsync(string tag, string[] command,
        CancellationToken cancellationToken = default) => onRun(tag, command);
    public Task RemoveImageAsync(string tag,
        CancellationToken cancellationToken = default) => onRemove(tag);

    /// <summary>Creates a MockContainerRuntime that returns canned help text from RunAsync.</summary>
    public static MockContainerRuntime WithHelpText(string helpText) => new(
        onBuild: (_, _) => Task.FromResult("built"),
        onRun: (_, _) => Task.FromResult(helpText),
        onRemove: _ => Task.CompletedTask);

    /// <summary>Creates a no-op MockContainerRuntime.</summary>
    public static MockContainerRuntime NoOp => new(
        onBuild: (_, _) => Task.FromResult(""),
        onRun: (_, _) => Task.FromResult(""),
        onRemove: _ => Task.CompletedTask);
}

// ── FakeHttpHandler ─────────────────────────────────────────────────────────

/// <summary>
/// HttpMessageHandler that returns canned JSON responses, for testing HTTP-based version collectors.
/// </summary>
public sealed class FakeHttpHandler : HttpMessageHandler
{
    private readonly string _json;
    private readonly string? _linkHeader;

    public FakeHttpHandler(string json, string? linkHeader = null)
    {
        _json = json;
        _linkHeader = linkHeader;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent(_json, System.Text.Encoding.UTF8, "application/json")
        };
        if (_linkHeader is not null)
            response.Headers.TryAddWithoutValidation("Link", _linkHeader);
        return Task.FromResult(response);
    }
}
