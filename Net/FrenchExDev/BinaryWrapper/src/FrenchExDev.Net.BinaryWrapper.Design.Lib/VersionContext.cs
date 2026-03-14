using System.Collections.Concurrent;
using FrenchExDev.Net.BinaryWrapper.Design;
using Microsoft.Extensions.Logging;

namespace FrenchExDev.Net.BinaryWrapper.Design.Lib;

/// <summary>
/// Context passed to each middleware for a single version being processed.
/// </summary>
public sealed class VersionContext
{
    public required string Version { get; init; }
    public required string RuntimeBinary { get; init; }
    public required ILogger Logger { get; init; }
    public required string OutputDir { get; init; }

    /// <summary>
    /// Injectable process runner. Defaults to <see cref="ProcessRunnerContainerRuntime.RunProcessAsync"/>.
    /// </summary>
    public required Func<string[], Task<string>> RunProcess { get; init; }

    /// <summary>
    /// Max concurrency for scraping subcommands within this version.
    /// </summary>
    public int ScrapeParallelism { get; init; } = 4;

    // Mutable state shared between middleware
    public string? ImageTag { get; set; }
    public string? ContainerId { get; set; }
    public CommandTree? Result { get; set; }

    /// <summary>
    /// Callback to run a help command. Set by UseContainer (exec in container)
    /// or UseCachedHelp (read from disk).
    /// </summary>
    public Func<string[], Task<string>>? RunHelp { get; set; }

    /// <summary>
    /// Directory to dump raw help text. Set by UseContainer (to capture output).
    /// Null when reparsing (no IO needed, files already exist).
    /// </summary>
    public string? HelpDumpDir { get; set; }

    // Crash recovery tracking (shared across all versions)
    public required ConcurrentBag<string> ActiveContainers { get; init; }
    public required ConcurrentDictionary<string, byte> ActiveImages { get; init; }

    /// <summary>
    /// Optional progress tracker for dashboard display. Null when dashboard is disabled.
    /// </summary>
    public VersionProgressInfo? Progress { get; init; }
}
