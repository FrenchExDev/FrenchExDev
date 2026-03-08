using System.Diagnostics;
using System.Text.RegularExpressions;
using FrenchExDev.Net.Result;

namespace FrenchExDev.Net.BinaryWrapper;

// ── ICliCommand ──────────────────────────────────────────────────────────────

/// <summary>
/// Marker interface for generated command classes.
/// Each generated command class implements this to provide
/// its command path and serialized arguments.
/// </summary>
public interface ICliCommand
{
    /// <summary>The command path segments (e.g., ["container", "run"]).</summary>
    IReadOnlyList<string> CommandPath { get; }

    /// <summary>Serializes the command's options and arguments to CLI argument strings.</summary>
    IReadOnlyList<string> ToArguments();
}

// ── Output Parsing ───────────────────────────────────────────────────────────

/// <summary>Identifies the source of an output line.</summary>
public enum OutputSource
{
    StdOut,
    StdErr
}

/// <summary>A single line of output from a process.</summary>
public sealed record OutputLine(string Text, OutputSource Source);

/// <summary>
/// Stateful line-by-line parser that transforms process output lines into typed events.
/// Implementations may maintain internal state for context tracking
/// (e.g., which machine is currently being provisioned).
/// </summary>
public interface IOutputParser<out TEvent>
{
    /// <summary>Parse a single output line, yielding zero or more events.</summary>
    IEnumerable<TEvent> ParseLine(OutputLine line);

    /// <summary>Called when the process exits. Yields final events based on exit state.</summary>
    IEnumerable<TEvent> Complete(int exitCode);
}

/// <summary>
/// Aggregates a stream of events into a typed result.
/// </summary>
public interface IResultCollector<in TEvent, out TResult>
{
    /// <summary>Process a single event from the stream.</summary>
    void OnEvent(TEvent @event);

    /// <summary>Produce the final aggregated result after all events have been processed.</summary>
    TResult Complete();
}

// ── Binary Identity & Binding ────────────────────────────────────────────────

/// <summary>
/// Identifies a binary by its abstract name and optional version constraint.
/// Example: "container-runtime:24.0", "vagrant", "packer:1.9"
/// </summary>
public sealed record BinaryIdentifier
{
    public string Name { get; }
    public string? Version { get; }

    public BinaryIdentifier(string name, string? version = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
        Version = version;
    }

    /// <summary>Parses "name:version" or "name" strings.</summary>
    public static BinaryIdentifier Parse(string identifier)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identifier);
        var colonIndex = identifier.IndexOf(':');
        return colonIndex < 0
            ? new BinaryIdentifier(identifier)
            : new BinaryIdentifier(
                identifier[..colonIndex],
                identifier[(colonIndex + 1)..]);
    }

    public override string ToString() =>
        Version is null ? Name : $"{Name}:{Version}";
}

/// <summary>
/// Per-command overrides when the backing binary uses different flag names.
/// </summary>
public sealed record CommandOverrides
{
    /// <summary>Maps abstract option LongName to the backing binary's flag name.</summary>
    public IReadOnlyDictionary<string, string> OptionNameMappings { get; init; } =
        new Dictionary<string, string>();

    /// <summary>Options that the backing binary does not support (silently omitted).</summary>
    public IReadOnlySet<string> UnsupportedOptions { get; init; } =
        new HashSet<string>();
}

/// <summary>
/// Maps a BinaryIdentifier to an actual executable path and runtime configuration.
/// </summary>
public sealed record BinaryBinding
{
    public required BinaryIdentifier Identifier { get; init; }
    public required string ExecutablePath { get; init; }
    /// <summary>The detected version of the binary, if known.</summary>
    public SemanticVersion? DetectedVersion { get; init; }
    public IReadOnlyDictionary<string, string> EnvironmentVariables { get; init; } =
        new Dictionary<string, string>();
    /// <summary>
    /// Per-command overrides. Key = command path joined by "." (e.g., "container.run").
    /// </summary>
    public IReadOnlyDictionary<string, CommandOverrides> Overrides { get; init; } =
        new Dictionary<string, CommandOverrides>();
}

// ── Binary Resolution ────────────────────────────────────────────────────────

/// <summary>
/// Resolves a BinaryIdentifier to a concrete BinaryBinding.
/// </summary>
public interface IBinaryResolver
{
    Task<Result<BinaryBinding, BinaryResolutionError>> ResolveAsync(
        BinaryIdentifier identifier,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Simple in-memory resolver backed by a dictionary of bindings.
/// </summary>
public sealed class DictionaryBinaryResolver : IBinaryResolver
{
    private readonly IReadOnlyDictionary<string, BinaryBinding> _bindings;

    public DictionaryBinaryResolver(IReadOnlyDictionary<string, BinaryBinding> bindings)
    {
        _bindings = bindings;
    }

    public DictionaryBinaryResolver(params IEnumerable<BinaryBinding> bindings)
    {
        _bindings = bindings.ToDictionary(b => b.Identifier.Name);
    }

    public Task<Result<BinaryBinding, BinaryResolutionError>> ResolveAsync(
        BinaryIdentifier identifier,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(
            _bindings.TryGetValue(identifier.Name, out var binding)
                ? Result<BinaryBinding, BinaryResolutionError>.Success(binding)
                : Result<BinaryBinding, BinaryResolutionError>.Failure(
                    new BinaryResolutionError(identifier, "No binding found for identifier.")));
    }
}

// ── Version Detection ────────────────────────────────────────────────────────

/// <summary>
/// A parsed semantic version (major.minor.patch with optional prerelease/metadata).
/// </summary>
public sealed record SemanticVersion : IComparable<SemanticVersion>
{
    public int Major { get; }
    public int Minor { get; }
    public int Patch { get; }
    public string? PreRelease { get; }
    public string? BuildMetadata { get; }

    public SemanticVersion(int major, int minor = 0, int patch = 0,
        string? preRelease = null, string? buildMetadata = null)
    {
        if (major < 0) throw new ArgumentOutOfRangeException(nameof(major));
        if (minor < 0) throw new ArgumentOutOfRangeException(nameof(minor));
        if (patch < 0) throw new ArgumentOutOfRangeException(nameof(patch));
        Major = major;
        Minor = minor;
        Patch = patch;
        PreRelease = preRelease;
        BuildMetadata = buildMetadata;
    }

    private static readonly Regex SemverPattern = new(
        @"(\d+)\.(\d+)(?:\.(\d+))?(?:-([\w.]+))?(?:\+([\w.]+))?",
        RegexOptions.Compiled);

    public static SemanticVersion Parse(string version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        var match = SemverPattern.Match(version);
        if (!match.Success)
            throw new FormatException($"Invalid semantic version: '{version}'");
        return new SemanticVersion(
            int.Parse(match.Groups[1].Value),
            int.Parse(match.Groups[2].Value),
            match.Groups[3].Success ? int.Parse(match.Groups[3].Value) : 0,
            match.Groups[4].Success ? match.Groups[4].Value : null,
            match.Groups[5].Success ? match.Groups[5].Value : null);
    }

    public static bool TryParse(string? version, out SemanticVersion? result)
    {
        result = null;
        if (string.IsNullOrWhiteSpace(version)) return false;
        var match = SemverPattern.Match(version);
        if (!match.Success) return false;
        result = new SemanticVersion(
            int.Parse(match.Groups[1].Value),
            int.Parse(match.Groups[2].Value),
            match.Groups[3].Success ? int.Parse(match.Groups[3].Value) : 0,
            match.Groups[4].Success ? match.Groups[4].Value : null,
            match.Groups[5].Success ? match.Groups[5].Value : null);
        return true;
    }

    public int CompareTo(SemanticVersion? other)
    {
        if (other is null) return 1;
        var c = Major.CompareTo(other.Major);
        if (c != 0) return c;
        c = Minor.CompareTo(other.Minor);
        if (c != 0) return c;
        c = Patch.CompareTo(other.Patch);
        if (c != 0) return c;
        if (PreRelease is null && other.PreRelease is not null) return 1;
        if (PreRelease is not null && other.PreRelease is null) return -1;
        if (PreRelease is not null && other.PreRelease is not null)
            return string.Compare(PreRelease, other.PreRelease, StringComparison.Ordinal);
        return 0;
    }

    public static bool operator <(SemanticVersion left, SemanticVersion right) =>
        left.CompareTo(right) < 0;
    public static bool operator >(SemanticVersion left, SemanticVersion right) =>
        left.CompareTo(right) > 0;
    public static bool operator <=(SemanticVersion left, SemanticVersion right) =>
        left.CompareTo(right) <= 0;
    public static bool operator >=(SemanticVersion left, SemanticVersion right) =>
        left.CompareTo(right) >= 0;

    public override string ToString()
    {
        var s = $"{Major}.{Minor}.{Patch}";
        if (PreRelease is not null) s += $"-{PreRelease}";
        if (BuildMetadata is not null) s += $"+{BuildMetadata}";
        return s;
    }
}

/// <summary>
/// Detects the installed version of a binary by running it with a version flag.
/// </summary>
public interface IVersionDetector
{
    Task<Result<SemanticVersion, CommandError>> DetectAsync(
        string executablePath,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Default version detector that runs "{binary} --version" and parses the output.
/// </summary>
public sealed class StandardVersionDetector : IVersionDetector
{
    private readonly IProcessRunner _processRunner;

    public StandardVersionDetector(IProcessRunner processRunner)
    {
        _processRunner = processRunner;
    }

    public async Task<Result<SemanticVersion, CommandError>> DetectAsync(
        string executablePath,
        CancellationToken cancellationToken = default)
    {
        var spec = new ProcessSpec
        {
            ExecutablePath = executablePath,
            Arguments = ["--version"]
        };

        var output = await _processRunner.RunAsync(spec, cancellationToken);

        if (output.ExitCode != 0)
            return Result<SemanticVersion, CommandError>.Failure(
                new CommandError(output.ExitCode, output.StandardError,
                    "Version detection failed."));

        var text = output.StandardOutput.Trim();
        if (SemanticVersion.TryParse(text, out var version) && version is not null)
            return Result<SemanticVersion, CommandError>.Success(version);

        return Result<SemanticVersion, CommandError>.Failure(
            new CommandError(0, text, $"Could not parse version from: '{text}'"));
    }
}

// ── Process Execution ────────────────────────────────────────────────────────

/// <summary>Specification for launching a process.</summary>
public sealed record ProcessSpec
{
    public required string ExecutablePath { get; init; }
    public IReadOnlyList<string> Arguments { get; init; } = [];
    public string? WorkingDirectory { get; init; }
    public IReadOnlyDictionary<string, string> EnvironmentVariables { get; init; } =
        new Dictionary<string, string>();
    public TimeSpan? Timeout { get; init; }
}

/// <summary>The captured output from a completed process.</summary>
public sealed record ProcessOutput
{
    public required int ExitCode { get; init; }
    public required string StandardOutput { get; init; }
    public required string StandardError { get; init; }
}

/// <summary>Abstraction over System.Diagnostics.Process for testability.</summary>
public interface IProcessRunner
{
    /// <summary>Streams output lines as they arrive from the process.</summary>
    IAsyncEnumerable<OutputLine> StreamAsync(
        ProcessSpec spec,
        CancellationToken cancellationToken = default);

    /// <summary>Runs the process to completion and returns captured output.</summary>
    Task<ProcessOutput> RunAsync(
        ProcessSpec spec,
        CancellationToken cancellationToken = default);
}

/// <summary>Real process runner using System.Diagnostics.Process.</summary>
public sealed class SystemProcessRunner : IProcessRunner
{
    public async IAsyncEnumerable<OutputLine> StreamAsync(
        ProcessSpec spec,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var process = CreateProcess(spec);
        var channel = System.Threading.Channels.Channel.CreateUnbounded<OutputLine>();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null)
                channel.Writer.TryWrite(new OutputLine(e.Data, OutputSource.StdOut));
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
                channel.Writer.TryWrite(new OutputLine(e.Data, OutputSource.StdErr));
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        _ = Task.Run(async () =>
        {
            await process.WaitForExitAsync(cancellationToken);
            channel.Writer.Complete();
        }, cancellationToken);

        await foreach (var line in channel.Reader.ReadAllAsync(cancellationToken))
        {
            yield return line;
        }
    }

    public async Task<ProcessOutput> RunAsync(
        ProcessSpec spec,
        CancellationToken cancellationToken = default)
    {
        using var process = CreateProcess(spec);
        process.Start();

        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

        if (spec.Timeout.HasValue)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(spec.Timeout.Value);
            await process.WaitForExitAsync(cts.Token);
        }
        else
        {
            await process.WaitForExitAsync(cancellationToken);
        }

        return new ProcessOutput
        {
            ExitCode = process.ExitCode,
            StandardOutput = await stdoutTask,
            StandardError = await stderrTask
        };
    }

    private static Process CreateProcess(ProcessSpec spec)
    {
        var psi = new ProcessStartInfo
        {
            FileName = spec.ExecutablePath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var arg in spec.Arguments)
            psi.ArgumentList.Add(arg);

        if (spec.WorkingDirectory is not null)
            psi.WorkingDirectory = spec.WorkingDirectory;

        foreach (var (key, value) in spec.EnvironmentVariables)
            psi.Environment[key] = value;

        return new Process { StartInfo = psi };
    }
}

// ── Command Executor ─────────────────────────────────────────────────────────

/// <summary>
/// Orchestrates the command execution pipeline:
/// resolve binary -> serialize args -> apply overrides -> execute process.
/// </summary>
public sealed class CommandExecutor
{
    private readonly IBinaryResolver _resolver;
    private readonly IProcessRunner _processRunner;

    public CommandExecutor(IBinaryResolver resolver, IProcessRunner? processRunner = null)
    {
        _resolver = resolver;
        _processRunner = processRunner ?? new SystemProcessRunner();
    }

    /// <summary>Execute a command and return raw process output.</summary>
    public async Task<Result<ProcessOutput, CommandError>> ExecuteAsync(
        BinaryIdentifier binaryId,
        ICliCommand command,
        CancellationToken cancellationToken = default)
    {
        var resolveResult = await _resolver.ResolveAsync(binaryId, cancellationToken);
        if (resolveResult.IsFailure)
            return Result<ProcessOutput, CommandError>.Failure(
                new CommandError(-1, "", resolveResult.Error!.Message));

        var binding = resolveResult.Value!;
        var spec = BuildProcessSpec(binding, command);
        var output = await _processRunner.RunAsync(spec, cancellationToken);

        return output.ExitCode == 0
            ? Result<ProcessOutput, CommandError>.Success(output)
            : Result<ProcessOutput, CommandError>.Failure(
                new CommandError(output.ExitCode, output.StandardError, "Command failed."));
    }

    /// <summary>Execute a command and stream parsed events.</summary>
    public async IAsyncEnumerable<TEvent> StreamAsync<TEvent>(
        BinaryIdentifier binaryId,
        ICliCommand command,
        IOutputParser<TEvent> parser,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var resolveResult = await _resolver.ResolveAsync(binaryId, cancellationToken);
        if (resolveResult.IsFailure)
            yield break;

        var binding = resolveResult.Value!;
        var spec = BuildProcessSpec(binding, command);

        await foreach (var line in _processRunner.StreamAsync(spec, cancellationToken))
        {
            foreach (var @event in parser.ParseLine(line))
                yield return @event;
        }

        foreach (var @event in parser.Complete(0))
            yield return @event;
    }

    /// <summary>Execute a command, parse events, and collect into a typed result.</summary>
    public async Task<Result<TResult, CommandError>> ExecuteAsync<TEvent, TResult>(
        BinaryIdentifier binaryId,
        ICliCommand command,
        IOutputParser<TEvent> parser,
        IResultCollector<TEvent, TResult> collector,
        CancellationToken cancellationToken = default)
        where TResult : notnull
    {
        var resolveResult = await _resolver.ResolveAsync(binaryId, cancellationToken);
        if (resolveResult.IsFailure)
            return Result<TResult, CommandError>.Failure(
                new CommandError(-1, "", resolveResult.Error!.Message));

        var binding = resolveResult.Value!;
        var spec = BuildProcessSpec(binding, command);

        await foreach (var line in _processRunner.StreamAsync(spec, cancellationToken))
        {
            foreach (var @event in parser.ParseLine(line))
                collector.OnEvent(@event);
        }

        foreach (var @event in parser.Complete(0))
            collector.OnEvent(@event);

        return Result<TResult, CommandError>.Success(collector.Complete());
    }

    internal ProcessSpec BuildProcessSpec(BinaryBinding binding, ICliCommand command)
    {
        var commandPath = string.Join(".", command.CommandPath);
        var hasOverrides = binding.Overrides.TryGetValue(commandPath, out var overrides);

        var arguments = new List<string>();
        arguments.AddRange(command.CommandPath);

        foreach (var arg in command.ToArguments())
        {
            if (hasOverrides && overrides is not null)
            {
                if (arg.StartsWith("--"))
                {
                    var optionName = arg[2..];
                    if (overrides.UnsupportedOptions.Contains(optionName))
                        continue;
                    if (overrides.OptionNameMappings.TryGetValue(optionName, out var mapped))
                    {
                        arguments.Add($"--{mapped}");
                        continue;
                    }
                }
            }
            arguments.Add(arg);
        }

        return new ProcessSpec
        {
            ExecutablePath = binding.ExecutablePath,
            Arguments = arguments,
            EnvironmentVariables = binding.EnvironmentVariables
        };
    }
}

// ── Command Execution Handle ─────────────────────────────────────────────────

/// <summary>
/// Base execution handle that supports dual consumption:
/// IAsyncEnumerable for streaming, ExecuteAsync for result collection.
/// </summary>
public class CommandExecution<TEvent> : IAsyncEnumerable<TEvent>
{
    private readonly CommandExecutor _executor;
    private readonly BinaryIdentifier _binaryId;
    private readonly ICliCommand _command;
    private readonly IOutputParser<TEvent> _parser;

    public CommandExecution(
        CommandExecutor executor,
        BinaryIdentifier binaryId,
        ICliCommand command,
        IOutputParser<TEvent> parser)
    {
        _executor = executor;
        _binaryId = binaryId;
        _command = command;
        _parser = parser;
    }

    /// <summary>Stream events as they arrive.</summary>
    public IAsyncEnumerator<TEvent> GetAsyncEnumerator(
        CancellationToken cancellationToken = default) =>
        _executor.StreamAsync(_binaryId, _command, _parser, cancellationToken)
            .GetAsyncEnumerator(cancellationToken);

    /// <summary>Execute and return raw process output.</summary>
    public Task<Result<ProcessOutput, CommandError>> ExecuteAsync(
        CancellationToken cancellationToken = default) =>
        _executor.ExecuteAsync(_binaryId, _command, cancellationToken);

    /// <summary>Execute, parse events, and collect into a typed result.</summary>
    public Task<Result<TResult, CommandError>> ExecuteAsync<TResult>(
        IResultCollector<TEvent, TResult> collector,
        CancellationToken cancellationToken = default)
        where TResult : notnull =>
        _executor.ExecuteAsync(_binaryId, _command, _parser, collector, cancellationToken);
}

// ── Versioning Attributes ────────────────────────────────────────────────────

/// <summary>Marks a command or option as available since a specific binary version.</summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Property, AllowMultiple = false)]
public sealed class SinceVersionAttribute : Attribute
{
    public string Version { get; }
    public SinceVersionAttribute(string version) => Version = version;
}

/// <summary>Marks a command or option as removed/unavailable starting from a specific binary version.</summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Property, AllowMultiple = false)]
public sealed class UntilVersionAttribute : Attribute
{
    public string Version { get; }
    public UntilVersionAttribute(string version) => Version = version;
}

// ── Version Guard ─────────────────────────────────────────────────────────────

/// <summary>
/// Runtime version enforcement. Throws when a command or option
/// is called on a binary version that does not support it.
/// </summary>
public static class VersionGuard
{
    /// <summary>Throws if the command is outside the supported version range.</summary>
    public static void EnsureCommandSupported(
        SemanticVersion? detectedVersion,
        string commandPath,
        SemanticVersion? since,
        SemanticVersion? until)
    {
        if (detectedVersion is null) return;

        if (since is not null && detectedVersion < since)
            throw new CommandNotSupportedException(commandPath, detectedVersion, since, until);

        if (until is not null && detectedVersion >= until)
            throw new CommandNotSupportedException(commandPath, detectedVersion, since, until);
    }

    /// <summary>Throws if the option is outside the supported version range.</summary>
    public static void EnsureOptionSupported(
        SemanticVersion? detectedVersion,
        string commandPath,
        string optionName,
        SemanticVersion? since,
        SemanticVersion? until)
    {
        if (detectedVersion is null) return;

        if (since is not null && detectedVersion < since)
            throw new OptionNotSupportedException(commandPath, optionName, detectedVersion, since, until);

        if (until is not null && detectedVersion >= until)
            throw new OptionNotSupportedException(commandPath, optionName, detectedVersion, since, until);
    }
}

/// <summary>Thrown when a command is not available in the detected binary version.</summary>
public sealed class CommandNotSupportedException : InvalidOperationException
{
    public string CommandPath { get; }
    public SemanticVersion DetectedVersion { get; }
    public SemanticVersion? Since { get; }
    public SemanticVersion? Until { get; }

    public CommandNotSupportedException(
        string commandPath, SemanticVersion detectedVersion,
        SemanticVersion? since, SemanticVersion? until)
        : base(FormatMessage(commandPath, detectedVersion, since, until))
    {
        CommandPath = commandPath;
        DetectedVersion = detectedVersion;
        Since = since;
        Until = until;
    }

    private static string FormatMessage(
        string commandPath, SemanticVersion detected,
        SemanticVersion? since, SemanticVersion? until)
        => until is not null && detected >= until
            ? $"Command '{commandPath}' was removed in version {until} (detected: {detected})."
            : $"Command '{commandPath}' requires version {since} or later (detected: {detected}).";
}

/// <summary>Thrown when an option is not available in the detected binary version.</summary>
public sealed class OptionNotSupportedException : InvalidOperationException
{
    public string CommandPath { get; }
    public string OptionName { get; }
    public SemanticVersion DetectedVersion { get; }
    public SemanticVersion? Since { get; }
    public SemanticVersion? Until { get; }

    public OptionNotSupportedException(
        string commandPath, string optionName, SemanticVersion detectedVersion,
        SemanticVersion? since, SemanticVersion? until)
        : base(FormatMessage(commandPath, optionName, detectedVersion, since, until))
    {
        CommandPath = commandPath;
        OptionName = optionName;
        DetectedVersion = detectedVersion;
        Since = since;
        Until = until;
    }

    private static string FormatMessage(
        string commandPath, string optionName, SemanticVersion detected,
        SemanticVersion? since, SemanticVersion? until)
        => until is not null && detected >= until
            ? $"Option '{optionName}' on command '{commandPath}' was removed in version {until} (detected: {detected})."
            : $"Option '{optionName}' on command '{commandPath}' requires version {since} or later (detected: {detected}).";
}

// ── Error Types ──────────────────────────────────────────────────────────────

/// <summary>Error when a binary cannot be resolved from its identifier.</summary>
public sealed record BinaryResolutionError(BinaryIdentifier Identifier, string Message);

/// <summary>Error from command execution (non-zero exit, parse failure, etc.).</summary>
public sealed record CommandError(int ExitCode, string StandardError, string Message);
