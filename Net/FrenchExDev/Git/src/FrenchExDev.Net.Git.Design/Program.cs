using System.Diagnostics;
using FrenchExDev.Net.BinaryWrapper.Design;
using FrenchExDev.Net.BinaryWrapper.Design.Lib;
using FrenchExDev.Net.Git.Design;
using FrenchExDev.Net.Wrapper.Versioning;
using Microsoft.Extensions.Logging;

Func<string, ILogger, IHelpParser> parser = (_, _) => new GitHelpParser();

var pipeline = new DesignPipeline()
    .UseImageBuild(
        imageTagPrefix: "git-scrape",
        baseImage: "debian:bookworm",
        installScript: v =>
            "apt-get update -qq > /dev/null 2>&1 && " +
            "apt-get install -y -qq make gcc libz-dev libcurl4-openssl-dev " +
            "libssl-dev libexpat1-dev gettext curl > /dev/null 2>&1 && " +
            $"curl -fsSL https://github.com/git/git/archive/refs/tags/v{v}.tar.gz " +
            "-o /tmp/git.tar.gz && " +
            "tar xzf /tmp/git.tar.gz -C /tmp && " +
            $"cd /tmp/git-{v} && " +
            "make prefix=/usr/local -j$(nproc) all > /dev/null 2>&1 && " +
            "make prefix=/usr/local install > /dev/null 2>&1 && " +
            "rm -rf /tmp/git* && apt-get clean > /dev/null 2>&1",
        shell: "bash")
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
    VersionCollector = new GitHubTagsVersionCollector("git", "git"),
    Pipeline = pipeline,
    ReparsePipeline = reparsePipeline,
    DefaultMinVersion = "2.30.0",
    OutputFilePattern = "git-{version}.json",
    OutputDir = Path.GetFullPath(Path.Combine("..", "FrenchExDev.Net.Git", "scrape")),
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
