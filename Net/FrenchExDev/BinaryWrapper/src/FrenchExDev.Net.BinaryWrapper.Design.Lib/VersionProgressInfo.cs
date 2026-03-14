using System.Diagnostics;

namespace FrenchExDev.Net.BinaryWrapper.Design.Lib;

/// <summary>
/// Thread-safe progress tracker for a single version being processed.
/// Updated by middleware/scraper, read by the dashboard polling loop.
/// </summary>
public sealed class VersionProgressInfo
{
    public string Version { get; }

    private volatile string _stage = "Pending";
    private int _commandsScraped;
    private volatile string? _error;
    private readonly long _startTicks = Stopwatch.GetTimestamp();

    public VersionProgressInfo(string version) => Version = version;

    public string Stage => _stage;
    public int CommandsScraped => Volatile.Read(ref _commandsScraped);
    public string? Error => _error;
    public TimeSpan Elapsed => Stopwatch.GetElapsedTime(_startTicks);

    public void SetStage(string stage) => _stage = stage;
    public void IncrementCommandsScraped() => Interlocked.Increment(ref _commandsScraped);
    public void SetError(string error) { _error = error; _stage = "Failed"; }
    public void SetDone() => _stage = "Done";
}
