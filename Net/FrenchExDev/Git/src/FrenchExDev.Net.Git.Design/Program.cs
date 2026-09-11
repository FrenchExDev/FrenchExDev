using System.Diagnostics;
using FrenchExDev.Net.BinaryWrapper.Design;
using FrenchExDev.Net.BinaryWrapper.Design.Lib;
using FrenchExDev.Net.Git.Design;
using FrenchExDev.Net.Wrapper.Versioning;
using Microsoft.Extensions.Logging;

if (args.Any(arg => arg is "--help" or "-h"))
{
    Console.WriteLine("""
        Collect git help using shared dependencies and per-version images (debian:bookworm).
          --min-version <version>       Minimum version (default: 2.30.0).
          --list                        List matching versions without building images.
          --fail-fast                   Stop scheduling after a failure; preserve failed versions for replay.
          --stop-file <path>            Share a graceful stop signal with other clients (implies --fail-fast).
          --retry-known-missing         Retry excluded versions when using --missing.
          --missing                     Select versions without an existing JSON.
          --parallel <n>                Concurrent versions (default: 4).
          --scrape-parallel <n>          Concurrent help commands per container (default: 4).
          --runtime <podman|docker>      Container runtime (default: podman).
          --output <directory>          Override scrape output directory.
          --reparse                     Reparse cached help without containers or version discovery.
          --build-base                  Prepare all dependency recipes, without version discovery.
          --build-images                Build selected version images without collecting help.
          --clean-images                Remove this wrapper's cached images, without version discovery.
          --keep-images                 Keep version images after scraping (default: remove).
          --dashboard                   Display the live progress dashboard.
          --add-known-missing <v,...>    Record unavailable versions.
          --remove-known-missing <v,...> Remove recorded unavailable versions.
          --list-known-missing          List recorded unavailable versions.
        """);
    return 0;
}

var env = DotEnvLoader.Load();
env.TryGetValue("GITHUB_TOKEN", out var githubToken);

Func<string, ILogger, IHelpParser> parser = (_, _) => new GitHelpParser();

var pipeline = new DesignPipeline()
    .UseVersionImage()
    .UseContainer()
    .Use(next => async ctx =>
    {
        // Override RunHelp to handle git's quirks:
        // 1. Root level: replace "git -h" with "git help -a" to discover ALL commands
        // 2. All levels: tolerate non-zero exit codes and capture stdout+stderr
        //    (git outputs -h help to stderr with exit 129, but podman exec may
        //     merge it into stdout — so we must capture both)
        var cid = ctx.ContainerId!;
        var runtime = ctx.RuntimeBinary;

        ctx.RunHelp = async helpArgs =>
        {
            // Root level: use "git help -a" instead of "git -h"
            var actualArgs = (helpArgs.Length == 2 && helpArgs[1] == "-h")
                ? [helpArgs[0], "help", "-a"]
                : helpArgs;

            var execArgs = new List<string> { runtime, "exec", cid };
            execArgs.AddRange(actualArgs);

            // Run process capturing stdout+stderr, tolerating non-zero exit
            return await RunGitHelp(execArgs.ToArray());
        };
        await next(ctx);
    })
    .UseScraper("git", parser, helpFlag: "-h")
    .Build();

var reparsePipeline = new DesignPipeline()
    .UseCachedHelp()
    .UseScraper("git", parser, helpFlag: "-h")
    .Build();

return await new DesignPipelineRunner
{
    ImagePlanResolver = new GitImagePlanResolver(),
    VersionCollector = new GitHubTagsVersionCollector("git", "git", token: githubToken),
    Pipeline = pipeline,
    ReparsePipeline = reparsePipeline,
    DefaultMinVersion = "2.30.0",
    OutputFilePattern = "git-{version}.json",
    OutputDir = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", "FrenchExDev.Net.Git", "scrape")),
}.RunAsync(args);

/// <summary>
/// Runs a process capturing both stdout and stderr, returning the combined output.
/// Does NOT throw on non-zero exit codes (git exits 129 for -h help).
/// </summary>
static async Task<string> RunGitHelp(string[] args)
{
    var psi = new ProcessStartInfo
    {
        FileName = args[0],
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        CreateNoWindow = true
    };
    foreach (var arg in args[1..])
        psi.ArgumentList.Add(arg);

    using var proc = Process.Start(psi);
    if (proc is null) return "";
    var stdout = await proc.StandardOutput.ReadToEndAsync();
    var stderr = await proc.StandardError.ReadToEndAsync();
    await proc.WaitForExitAsync();

    // Return whichever stream has content (git may write to either)
    if (!string.IsNullOrWhiteSpace(stdout))
        return stdout;
    return stderr;
}
