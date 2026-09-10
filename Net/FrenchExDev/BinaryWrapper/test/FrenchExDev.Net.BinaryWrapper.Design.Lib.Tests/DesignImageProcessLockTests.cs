using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using Shouldly;
using ImageBuildProbeProgram = FrenchExDev.Net.BinaryWrapper.Design.ImageBuildProbe.Program;

namespace FrenchExDev.Net.BinaryWrapper.Design.Lib.Tests;

public sealed class DesignImageProcessLockTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "bw-process-lock-" + Guid.NewGuid().ToString("N"));
    private readonly string _imageName = "lock-test-" + Guid.NewGuid().ToString("N");
    private readonly List<Probe> _probes = [];
    private string Store => Path.Combine(_root, "engine");

    [Theory]
    [InlineData("base", false)]
    [InlineData("base", true)]
    [InlineData("version", false)]
    [InlineData("version", true)]
    public async Task ConcurrentProcesses_BuildEachImageOnce(string stage, bool shareOutput)
    {
        var first = Start("first", blockedStage: stage);
        await WaitForBuildAsync("first", stage);
        var second = Start("second", output: shareOutput ? "first" : "second");
        await second.WaitForLockAsync();
        await ReleaseAsync("first");

        await first.ShouldExitAsync(0);
        await second.ShouldExitAsync(0);
        BuildStages().Order().ShouldBe(new[] { "base", "version" });
        ImageIds("first").ShouldBe(ImageIds(shareOutput ? "first" : "second"));
    }

    [Theory]
    [InlineData("base")]
    [InlineData("version")]
    public async Task KilledOwner_ReleasesLockAndWaiterCanBuildAndReuse(string stage)
    {
        var first = Start("first", blockedStage: stage);
        await WaitForBuildAsync("first", stage);
        var second = Start("second");
        await second.WaitForLockAsync();
        await first.KillAsync();

        await second.ShouldExitAsync(0);
        BuildStages().Count(s => s == stage).ShouldBe(2); // One interrupted attempt and one successful build.
        BuildStages().Count.ShouldBe(3);
        var third = Start("third");
        await third.ShouldExitAsync(0);
        BuildStages().Count.ShouldBe(3);
        ImageIds("second").ShouldBe(ImageIds("third"));
    }

    [Theory]
    [InlineData("base")]
    [InlineData("version")]
    public async Task FailedOwner_ReleasesLockAndWaiterRetries(string stage)
    {
        var first = Start("first", blockedStage: stage, failedStage: stage);
        await WaitForBuildAsync("first", stage);
        var second = Start("second");
        await second.WaitForLockAsync();
        await ReleaseAsync("first");

        await first.ShouldExitAsync(1);
        await second.ShouldExitAsync(0);
        BuildStages().Count(s => s == stage).ShouldBe(2);
        BuildStages().Count.ShouldBe(3);
        ImageIds("second").Length.ShouldBe(2);
    }

    [Fact]
    public async Task DifferentVersions_CanBuildConcurrently()
    {
        var first = Start("first", blockedStage: "version");
        await WaitForBuildAsync("first", "version");
        var second = Start("second", version: "2.0");
        await second.ShouldExitAsync(0);
        first.Process.HasExited.ShouldBeFalse();
        await ReleaseAsync("first");
        await first.ShouldExitAsync(0);
        BuildStages().Count(s => s == "base").ShouldBe(1);
        BuildStages().Count(s => s == "version").ShouldBe(2);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConcurrentScrapes_LastUserRemovesImage_AfterFirstReturnsOrDies(bool killFirst)
    {
        var first = Start("first", blockedStage: "scrape", mode: "scrape");
        await WaitForScrapeAsync("first");
        var second = Start("second", blockedStage: "scrape", mode: "scrape");
        await WaitForScrapeAsync("second");
        BuildStages().Count.ShouldBe(2);
        if (killFirst)
            await first.KillAsync();
        else
        {
            await ReleaseAsync("first");
            await first.ShouldExitAsync(0);
        }
        second.Process.HasExited.ShouldBeFalse();
        Directory.GetFiles(Store, "*.image").Length.ShouldBe(2);
        Directory.GetFiles(Store, "*.removed").ShouldBeEmpty();

        await ReleaseAsync("second");
        await second.ShouldExitAsync(0);
        Directory.GetFiles(Store, "*.image").Length.ShouldBe(1); // Shared base remains.
        Directory.GetFiles(Store, "*.removed").Select(Path.GetFileName).ShouldBe(new[] { "second.removed" });
        (await File.ReadAllTextAsync(Path.Combine(Store, "second.removed")))
            .ShouldBe(await File.ReadAllTextAsync(Path.Combine(Store, "second.scrape")));
    }

    [Fact]
    public async Task DifferentVersions_CanScrapeAndCleanConcurrently()
    {
        var first = Start("first", blockedStage: "scrape", mode: "scrape");
        await WaitForScrapeAsync("first");
        var second = Start("second", version: "2.0", mode: "scrape");
        await second.ShouldExitAsync(0);
        first.Process.HasExited.ShouldBeFalse();
        Directory.GetFiles(Store, "*.image").Length.ShouldBe(2); // First version and base.
        await ReleaseAsync("first");
        await first.ShouldExitAsync(0);
        Directory.GetFiles(Store, "*.image").Length.ShouldBe(1);
        BuildStages().Count(s => s == "version").ShouldBe(2);
    }

    [Fact]
    public async Task DifferentRecipeBases_CanBuildConcurrently()
    {
        var first = Start("first", blockedStage: "base", differentBase: true);
        await WaitForBuildAsync("first", "base");
        var second = Start("second", version: "2.0", differentBase: true);
        await second.ShouldExitAsync(0);
        first.Process.HasExited.ShouldBeFalse();
        await ReleaseAsync("first");
        await first.ShouldExitAsync(0);
        BuildStages().Count(s => s == "base").ShouldBe(2);
        BuildStages().Count(s => s == "version").ShouldBe(2);
    }

    private Task WaitForScrapeAsync(string worker) => WaitUntilAsync(
        () => File.Exists(Path.Combine(Store, worker + ".scrape")),
        () => string.Join(Environment.NewLine, _probes.Select(p => p.Output)));
    private Probe Start(string worker, string version = "1.0", string blockedStage = "",
        string failedStage = "", string? output = null, string mode = "build", bool differentBase = false)
    {
        var testAssembly = typeof(DesignImageProcessLockTests).Assembly.Location;
        var start = new ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet")
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true,
        };
        // Reuse the test runtime/dependency graph, which includes the probe project reference.
        foreach (var arg in new[]
        {
            "exec", "--runtimeconfig", Path.ChangeExtension(testAssembly, ".runtimeconfig.json"),
            "--depsfile", Path.ChangeExtension(testAssembly, ".deps.json"), typeof(ImageBuildProbeProgram).Assembly.Location,
            Store, Path.Combine(_root, output ?? worker), _imageName, worker, version, blockedStage, failedStage, mode,
            differentBase ? "different-base" : "",
        }) start.ArgumentList.Add(arg);
        var probe = new Probe(Process.Start(start) ?? throw new InvalidOperationException("Cannot start image-build probe."));
        _probes.Add(probe);
        return probe;
    }

    private Task WaitForBuildAsync(string worker, string stage) => WaitUntilAsync(
        () => File.Exists(Path.Combine(Store, "builds", worker + "-" + stage + ".json")),
        () => string.Join(Environment.NewLine, _probes.Select(p => p.Output)));

    private Task ReleaseAsync(string worker) => File.WriteAllTextAsync(Path.Combine(Store, "release-" + worker), "");

    private List<string> BuildStages() => Directory.GetFiles(Path.Combine(Store, "builds"), "*.json")
        .Select(path => JsonSerializer.Deserialize<BuildRecord>(File.ReadAllText(path))!.Stage).ToList();

    private string[] ImageIds(string output) => Directory.GetFiles(Path.Combine(_root, output), "image.json", SearchOption.AllDirectories)
        .Select(path => JsonSerializer.Deserialize<ImageRecord>(File.ReadAllText(path))!.ImageId).Order().ToArray();

    private sealed record BuildRecord(string Stage);
    private sealed record ImageRecord(string ImageId);

    private static async Task WaitUntilAsync(Func<bool> condition, Func<string> diagnostics)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        try
        {
            while (!condition()) await Task.Delay(20, timeout.Token);
        }
        catch (OperationCanceledException)
        {
            throw new TimeoutException("Image-build probe did not reach the expected state.\n" + diagnostics());
        }
    }

    private sealed class Probe : IDisposable
    {
        public Process Process { get; }
        private readonly ConcurrentQueue<string> _lines = new();
        private readonly Task _stdout;
        private readonly Task _stderr;
        public string Output => string.Join(Environment.NewLine, _lines);

        public Probe(Process process)
        {
            Process = process;
            _stdout = ReadAsync(process.StandardOutput);
            _stderr = ReadAsync(process.StandardError);
        }

        private async Task ReadAsync(StreamReader reader)
        {
            while (await reader.ReadLineAsync() is { } line) _lines.Enqueue(line);
        }

        public Task WaitForLockAsync() => WaitUntilAsync(
            () => _lines.Any(line => line.Contains("Waiting for image build lock:", StringComparison.Ordinal)), () => Output);

        public async Task ShouldExitAsync(int exitCode)
        {
            await Process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(20));
            await Task.WhenAll(_stdout, _stderr);
            Process.ExitCode.ShouldBe(exitCode, Output);
        }

        public async Task KillAsync()
        {
            Process.Kill(entireProcessTree: true);
            await Process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
            await Task.WhenAll(_stdout, _stderr);
        }

        public void Dispose()
        {
            if (!Process.HasExited)
            {
                Process.Kill(entireProcessTree: true);
                Process.WaitForExit(10000);
            }
            Process.Dispose();
        }
    }

    public void Dispose()
    {
        foreach (var probe in _probes) probe.Dispose();
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }
}
