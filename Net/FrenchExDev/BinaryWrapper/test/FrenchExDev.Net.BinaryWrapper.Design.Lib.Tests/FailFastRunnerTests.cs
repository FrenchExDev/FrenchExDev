using System.Collections.Concurrent;
using FrenchExDev.Net.BinaryWrapper.Design.Lib;
using Microsoft.Extensions.Logging;
using Shouldly;

namespace FrenchExDev.Net.BinaryWrapper.Design.Lib.Tests;

public sealed class FailFastRunnerTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "bw-fail-fast-" + Guid.NewGuid().ToString("N"));

    private DesignPipelineRunner Runner(VersionDelegate pipeline, string? output = null,
        IVersionCollector? collector = null) => new()
    {
        VersionCollector = collector ?? new StaticVersionCollector(["1.0", "2.0", "3.0"]),
        Pipeline = pipeline,
        OutputDir = output ?? _directory,
        OutputFilePattern = "tool-{version}.json",
        MinLogLevel = LogLevel.None,
        RunProcess = _ => Task.FromResult(""),
    };

    [Fact]
    public async Task FirstFailure_StopsQueue_AndMissingReplayRetriesFailure()
    {
        var attempts = new List<string>();
        var broken = true;
        var runner = Runner(ctx =>
        {
            attempts.Add(ctx.Version);
            if (broken && ctx.Version == "2.0") throw new IOException("transient failure");
            File.WriteAllText(Path.Combine(ctx.OutputDir, $"tool-{ctx.Version}.json"), "{}");
            return Task.CompletedTask;
        });
        string[] args = ["--missing", "--fail-fast", "--parallel", "1"];
        (await runner.RunAsync(args)).ShouldBe(1);
        attempts.ShouldBe(["1.0", "2.0"]);
        File.Exists(Path.Combine(_directory, "_known_missing.txt")).ShouldBeFalse();
        broken = false;
        attempts.Clear();
        (await runner.RunAsync(args)).ShouldBe(0);
        attempts.ShouldBe(["2.0", "3.0"]);
    }

    [Fact]
    public async Task SharedStop_DrainsActiveVersion_AndStopsSiblingQueue()
    {
        Directory.CreateDirectory(_directory);
        var stopFile = Path.Combine(_directory, "batch.stop");
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var siblingVersions = new ConcurrentBag<string>();
        var cleaned = false;
        var sibling = Runner(async ctx =>
        {
            siblingVersions.Add(ctx.Version);
            started.SetResult();
            try
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                while (!File.Exists(stopFile)) await Task.Delay(10, timeout.Token);
            }
            finally { cleaned = true; }
        }, Path.Combine(_directory, "sibling"));
        var failed = Runner(async _ =>
        {
            await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
            throw new IOException("first client fails");
        }, Path.Combine(_directory, "failed"));
        string[] args = ["--parallel", "1", "--stop-file", stopFile];
        var codes = await Task.WhenAll(sibling.RunAsync(args), failed.RunAsync(args));
        codes.ShouldBe([1, 1]);
        siblingVersions.ShouldBe(["1.0"]);
        cleaned.ShouldBeTrue();
        File.Exists(stopFile).ShouldBeTrue();
    }

    [Fact]
    public async Task ExistingStop_DoesNotDiscoverVersions_OrRunPipeline()
    {
        Directory.CreateDirectory(_directory);
        var stopFile = Path.Combine(_directory, "stop");
        File.WriteAllText(stopFile, "");
        var runner = Runner(_ => throw new Exception("must not run"), collector: new FailingCollector());
        (await runner.RunAsync(["--stop-file", stopFile])).ShouldBe(1);
    }

    [Fact]
    public async Task DiscoveryFailure_SignalsSiblings_AndReturnsFailure()
    {
        var stopFile = Path.Combine(_directory, "signals", "stop");
        var runner = Runner(_ => throw new Exception("must not run"), collector: new FailingCollector());
        (await runner.RunAsync(["--stop-file", stopFile])).ShouldBe(1);
        File.Exists(stopFile).ShouldBeTrue();
    }

    [Fact]
    public async Task StopDuringDiscovery_PreventsEvenAnEmptySelectionFromReportingSuccess()
    {
        Directory.CreateDirectory(_directory);
        var stopFile = Path.Combine(_directory, "stop");
        var runner = Runner(_ => throw new Exception("must not run"),
            collector: new CallbackCollector(() => File.WriteAllText(stopFile, "")));
        (await runner.RunAsync(["--stop-file", stopFile])).ShouldBe(1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RetryKnownMissing_IsExplicit_AndPreservesManualExclusions(bool retry)
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(Path.Combine(_directory, "tool-1.0.json"), "{}");
        var excluded = Path.Combine(_directory, "_known_missing.txt");
        File.WriteAllText(excluded, "2.0\n");
        var seen = new List<string>();
        var runner = Runner(ctx => { seen.Add(ctx.Version); return Task.CompletedTask; });
        var args = new List<string> { "--missing", "--fail-fast", "--parallel", "1" };
        if (retry) args.Add("--retry-known-missing");
        (await runner.RunAsync(args.ToArray())).ShouldBe(0);
        seen.ShouldBe(retry ? ["2.0", "3.0"] : ["3.0"]);
        File.ReadAllText(excluded).ShouldBe("2.0\n");
    }

    [Fact]
    public async Task ParallelFailures_CanSignalTheSameFile()
    {
        var stopFile = Path.Combine(_directory, "stop");
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var running = 0;
        var attempts = 0;
        var runner = Runner(async _ =>
        {
            Interlocked.Increment(ref attempts);
            if (Interlocked.Increment(ref running) == 2) started.SetResult();
            await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
            throw new IOException("simultaneous failures");
        });
        (await runner.RunAsync(["--parallel", "2", "--stop-file", stopFile])).ShouldBe(1);
        attempts.ShouldBe(2);
    }

    private sealed class FailingCollector : IVersionCollector
    {
        public Task<IReadOnlyList<string>> CollectVersionsAsync(CancellationToken cancellationToken = default)
            => throw new IOException("discovery unavailable");
        public Task<IReadOnlyList<string>> CollectItemsAsync(CancellationToken cancellationToken = default)
            => CollectVersionsAsync(cancellationToken);
    }

    private sealed class CallbackCollector(Action callback) : IVersionCollector
    {
        public Task<IReadOnlyList<string>> CollectVersionsAsync(CancellationToken cancellationToken = default)
        {
            callback();
            return Task.FromResult<IReadOnlyList<string>>([]);
        }
        public Task<IReadOnlyList<string>> CollectItemsAsync(CancellationToken cancellationToken = default)
            => CollectVersionsAsync(cancellationToken);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }
}
