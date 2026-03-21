using System.Net;
using FrenchExDev.Net.JsonSchema.Design;
using FrenchExDev.Net.Wrapper.Versioning;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace FrenchExDev.Net.JsonSchema.Design.Tests;

// ── Fakes ───────────────────────────────────────────────────────────────────

internal sealed class FakeHttpHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

    public FakeHttpHandler(string content, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        _handler = _ => new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(content)
        };
    }

    public FakeHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
    {
        _handler = handler;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
        => Task.FromResult(_handler(request));
}

internal sealed class FakeLogger : ILogger
{
    public List<string> Messages { get; } = [];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel, EventId eventId, TState state,
        Exception? exception, Func<TState, Exception?, string> formatter)
    {
        Messages.Add(formatter(state, exception));
    }
}

// ── SchemaVersionProgressInfo Tests ─────────────────────────────────────────

public sealed class SchemaVersionProgressInfoTests
{
    [Fact]
    public void Constructor_SetsVersion_AndDefaultStagePending()
    {
        var info = new SchemaVersionProgressInfo("1.0.0");

        info.Version.ShouldBe("1.0.0");
        info.Stage.ShouldBe("Pending");
        info.Error.ShouldBeNull();
    }

    [Fact]
    public void SetStage_ChangesStage()
    {
        var info = new SchemaVersionProgressInfo("2.0.0");

        info.SetStage("Downloading");

        info.Stage.ShouldBe("Downloading");
    }

    [Fact]
    public void SetError_SetsErrorAndStageToFailed()
    {
        var info = new SchemaVersionProgressInfo("3.0.0");

        info.SetError("Network error");

        info.Error.ShouldBe("Network error");
        info.Stage.ShouldBe("Failed");
    }

    [Fact]
    public void SetDone_SetsStageToDone()
    {
        var info = new SchemaVersionProgressInfo("4.0.0");

        info.SetDone();

        info.Stage.ShouldBe("Done");
    }

    [Fact]
    public void Elapsed_IncreasesOverTime()
    {
        var info = new SchemaVersionProgressInfo("5.0.0");
        var first = info.Elapsed;

        // Spin briefly to let time pass
        Thread.Sleep(15);

        var second = info.Elapsed;
        second.ShouldBeGreaterThan(first);
    }
}

// ── SchemaDesignPipeline Tests ──────────────────────────────────────────────

public sealed class SchemaDesignPipelineTests
{
    [Fact]
    public async Task Build_EmptyPipeline_ReturnsTerminalThatDoesNothing()
    {
        var pipeline = new SchemaDesignPipeline().Build();

        var ctx = CreateMinimalContext("1.0.0");
        await pipeline(ctx);

        // No exception thrown, Content stays null
        ctx.Content.ShouldBeNull();
    }

    [Fact]
    public async Task Use_SingleMiddleware_IsInvoked()
    {
        var invoked = false;
        var pipeline = new SchemaDesignPipeline()
            .Use(next => async ctx =>
            {
                invoked = true;
                await next(ctx);
            })
            .Build();

        await pipeline(CreateMinimalContext("1.0.0"));

        invoked.ShouldBeTrue();
    }

    [Fact]
    public async Task Use_MultipleMiddleware_ComposedInCorrectOrder()
    {
        var order = new List<int>();

        var pipeline = new SchemaDesignPipeline()
            .Use(next => async ctx =>
            {
                order.Add(1);
                await next(ctx);
                order.Add(4);
            })
            .Use(next => async ctx =>
            {
                order.Add(2);
                await next(ctx);
                order.Add(3);
            })
            .Build();

        await pipeline(CreateMinimalContext("1.0.0"));

        // First added is outermost: 1 wraps 2, terminal is innermost
        order.ShouldBe([1, 2, 3, 4]);
    }

    [Fact]
    public void Use_ReturnsSameInstance_ForFluency()
    {
        var pipeline = new SchemaDesignPipeline();
        var result = pipeline.Use(next => ctx => next(ctx));
        result.ShouldBeSameAs(pipeline);
    }

    private static SchemaVersionContext CreateMinimalContext(string version) => new()
    {
        Version = version,
        OutputDir = Path.GetTempPath(),
        Logger = NullLogger.Instance,
        HttpClient = new HttpClient(),
        OutputFilePattern = "schema-v{version}.json",
    };
}

// ── SchemaDesignPipelineExtensions Tests ────────────────────────────────────

public sealed class UseHttpDownloadTests
{
    [Fact]
    public async Task UseHttpDownload_SetsContentFromHttp()
    {
        var handler = new FakeHttpHandler("schema-content-here");
        var client = new HttpClient(handler);

        var pipeline = new SchemaDesignPipeline()
            .UseHttpDownload(v => $"https://example.com/{v}.json")
            .Build();

        var ctx = new SchemaVersionContext
        {
            Version = "1.0.0",
            OutputDir = Path.GetTempPath(),
            Logger = NullLogger.Instance,
            HttpClient = client,
            OutputFilePattern = "schema-v{version}.json",
        };

        await pipeline(ctx);

        ctx.Content.ShouldBe("schema-content-here");
    }

    [Fact]
    public async Task UseHttpDownload_WithProgress_SetsStageThenCallsNext()
    {
        var handler = new FakeHttpHandler("content");
        var client = new HttpClient(handler);
        var progress = new SchemaVersionProgressInfo("1.0.0");
        var stageWhenNextCalled = "";

        var pipeline = new SchemaDesignPipeline()
            .UseHttpDownload(v => $"https://example.com/{v}.json")
            .Use(next => async ctx =>
            {
                stageWhenNextCalled = ctx.Progress!.Stage;
                await next(ctx);
            })
            .Build();

        var ctx = new SchemaVersionContext
        {
            Version = "1.0.0",
            OutputDir = Path.GetTempPath(),
            Logger = NullLogger.Instance,
            HttpClient = client,
            OutputFilePattern = "schema-v{version}.json",
            Progress = progress,
        };

        await pipeline(ctx);

        stageWhenNextCalled.ShouldBe("Downloading");
    }

    [Fact]
    public async Task UseHttpDownload_WithoutProgress_DoesNotThrow()
    {
        var handler = new FakeHttpHandler("content");
        var client = new HttpClient(handler);

        var pipeline = new SchemaDesignPipeline()
            .UseHttpDownload(v => $"https://example.com/{v}.json")
            .Build();

        var ctx = new SchemaVersionContext
        {
            Version = "1.0.0",
            OutputDir = Path.GetTempPath(),
            Logger = NullLogger.Instance,
            HttpClient = client,
            OutputFilePattern = "schema-v{version}.json",
            Progress = null,
        };

        await pipeline(ctx);

        ctx.Content.ShouldBe("content");
    }
}

public sealed class UseContentTransformTests
{
    [Fact]
    public async Task UseContentTransform_TransformsNonNullContent()
    {
        var pipeline = new SchemaDesignPipeline()
            .Use(next => async ctx =>
            {
                ctx.Content = "original";
                await next(ctx);
            })
            .UseContentTransform((version, content) => $"[{version}]{content}")
            .Build();

        var ctx = new SchemaVersionContext
        {
            Version = "2.0.0",
            OutputDir = Path.GetTempPath(),
            Logger = NullLogger.Instance,
            HttpClient = new HttpClient(),
            OutputFilePattern = "schema-v{version}.json",
        };

        await pipeline(ctx);

        ctx.Content.ShouldBe("[2.0.0]original");
    }

    [Fact]
    public async Task UseContentTransform_NullContent_SkipsTransform()
    {
        var transformCalled = false;
        var pipeline = new SchemaDesignPipeline()
            .UseContentTransform((_, _) =>
            {
                transformCalled = true;
                return "should not be set";
            })
            .Build();

        var ctx = new SchemaVersionContext
        {
            Version = "1.0.0",
            OutputDir = Path.GetTempPath(),
            Logger = NullLogger.Instance,
            HttpClient = new HttpClient(),
            OutputFilePattern = "schema-v{version}.json",
        };

        // Content is null by default
        await pipeline(ctx);

        transformCalled.ShouldBeFalse();
        ctx.Content.ShouldBeNull();
    }

    [Fact]
    public async Task UseContentTransform_WithProgress_SetsStageThenCallsNext()
    {
        var progress = new SchemaVersionProgressInfo("1.0.0");
        var stageWhenNextCalled = "";

        var pipeline = new SchemaDesignPipeline()
            .Use(next => async ctx =>
            {
                ctx.Content = "input";
                await next(ctx);
            })
            .UseContentTransform((_, c) => c.ToUpperInvariant())
            .Use(next => async ctx =>
            {
                stageWhenNextCalled = ctx.Progress!.Stage;
                await next(ctx);
            })
            .Build();

        var ctx = new SchemaVersionContext
        {
            Version = "1.0.0",
            OutputDir = Path.GetTempPath(),
            Logger = NullLogger.Instance,
            HttpClient = new HttpClient(),
            OutputFilePattern = "schema-v{version}.json",
            Progress = progress,
        };

        await pipeline(ctx);

        stageWhenNextCalled.ShouldBe("Transforming");
        ctx.Content.ShouldBe("INPUT");
    }

    [Fact]
    public async Task UseContentTransform_WithoutProgress_DoesNotThrow()
    {
        var pipeline = new SchemaDesignPipeline()
            .Use(next => async ctx =>
            {
                ctx.Content = "data";
                await next(ctx);
            })
            .UseContentTransform((_, c) => c + "-transformed")
            .Build();

        var ctx = new SchemaVersionContext
        {
            Version = "1.0.0",
            OutputDir = Path.GetTempPath(),
            Logger = NullLogger.Instance,
            HttpClient = new HttpClient(),
            OutputFilePattern = "schema-v{version}.json",
            Progress = null,
        };

        await pipeline(ctx);

        ctx.Content.ShouldBe("data-transformed");
    }
}

public sealed class UseSaveTests
{
    [Fact]
    public async Task UseSave_WritesContentToDisk()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"save-test-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(tempDir);

            var pipeline = new SchemaDesignPipeline()
                .Use(next => async ctx =>
                {
                    ctx.Content = "{\"schema\":true}";
                    await next(ctx);
                })
                .UseSave()
                .Build();

            var ctx = new SchemaVersionContext
            {
                Version = "3.0.0",
                OutputDir = tempDir,
                Logger = NullLogger.Instance,
                HttpClient = new HttpClient(),
                OutputFilePattern = "schema-v{version}.json",
            };

            await pipeline(ctx);

            ctx.OutputFilePath.ShouldNotBeNull();
            var expectedPath = Path.Combine(tempDir, "schema-v3.0.0.json");
            ctx.OutputFilePath.ShouldBe(expectedPath);
            File.Exists(expectedPath).ShouldBeTrue();
            File.ReadAllText(expectedPath).ShouldBe("{\"schema\":true}");
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task UseSave_NullContent_DoesNotWriteFile()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"save-null-test-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(tempDir);

            var pipeline = new SchemaDesignPipeline()
                .UseSave()
                .Build();

            var ctx = new SchemaVersionContext
            {
                Version = "1.0.0",
                OutputDir = tempDir,
                Logger = NullLogger.Instance,
                HttpClient = new HttpClient(),
                OutputFilePattern = "schema-v{version}.json",
            };

            // Content is null by default
            await pipeline(ctx);

            ctx.OutputFilePath.ShouldBeNull();
            Directory.GetFiles(tempDir).Length.ShouldBe(0);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task UseSave_WithProgress_SetsStageThenCallsNext()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"save-prog-test-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(tempDir);

            var progress = new SchemaVersionProgressInfo("1.0.0");
            var stageWhenNextCalled = "";

            var pipeline = new SchemaDesignPipeline()
                .Use(next => async ctx =>
                {
                    ctx.Content = "data";
                    await next(ctx);
                })
                .UseSave()
                .Use(next => async ctx =>
                {
                    stageWhenNextCalled = ctx.Progress!.Stage;
                    await next(ctx);
                })
                .Build();

            var ctx = new SchemaVersionContext
            {
                Version = "1.0.0",
                OutputDir = tempDir,
                Logger = NullLogger.Instance,
                HttpClient = new HttpClient(),
                OutputFilePattern = "schema-v{version}.json",
                Progress = progress,
            };

            await pipeline(ctx);

            stageWhenNextCalled.ShouldBe("Saving");
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task UseSave_WithoutProgress_DoesNotThrow()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"save-noprog-test-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(tempDir);

            var pipeline = new SchemaDesignPipeline()
                .Use(next => async ctx =>
                {
                    ctx.Content = "saved";
                    await next(ctx);
                })
                .UseSave()
                .Build();

            var ctx = new SchemaVersionContext
            {
                Version = "1.0.0",
                OutputDir = tempDir,
                Logger = NullLogger.Instance,
                HttpClient = new HttpClient(),
                OutputFilePattern = "schema-v{version}.json",
                Progress = null,
            };

            await pipeline(ctx);

            ctx.OutputFilePath.ShouldNotBeNull();
            File.ReadAllText(ctx.OutputFilePath!).ShouldBe("saved");
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }
}

// ── SchemaDesignPipelineRunner Tests ────────────────────────────────────────

public sealed class RunnerListAndFilterTests
{
    [Fact]
    public async Task RunAsync_ListOnly_PrintsVersionsAndReturnsZero()
    {
        var runner = CreateRunner(["1.0.0", "2.0.0"]);
        var result = await runner.RunAsync(["--list"]);
        result.ShouldBe(0);
    }

    [Fact]
    public async Task RunAsync_ListMissing_FiltersExistingFiles()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"list-missing-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(tempDir);
            File.WriteAllText(Path.Combine(tempDir, "schema-v1.0.0.json"), "existing");
            var runner = CreateRunner(["1.0.0", "2.0.0"], tempDir);
            var result = await runner.RunAsync(["--list", "--missing"]);
            result.ShouldBe(0);
        }
        finally { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); }
    }

    [Fact]
    public async Task RunAsync_NoVersionsToProcess_ReturnsZero()
    {
        var runner = CreateRunner([]);
        var result = await runner.RunAsync([]);
        result.ShouldBe(0);
    }

    [Fact]
    public async Task RunAsync_WithVersionFilter_AppliesFilter()
    {
        var runner = new SchemaDesignPipelineRunner
        {
            VersionCollector = new StaticVersionCollector(["1.0.0", "1.0.1", "1.1.0"]),
            Pipeline = _ => Task.CompletedTask,
            OutputDir = Path.GetTempPath(),
            VersionFilter = VersionFilters.LatestPatchPerMinor,
            AuthTokenEnvVar = null,
            MinLogLevel = LogLevel.None,
        };
        var result = await runner.RunAsync(["--list"]);
        result.ShouldBe(0);
    }

    [Fact]
    public async Task RunAsync_WithoutVersionFilter_UsesAllVersions()
    {
        var runner = new SchemaDesignPipelineRunner
        {
            VersionCollector = new StaticVersionCollector(["1.0.0", "2.0.0"]),
            Pipeline = _ => Task.CompletedTask,
            OutputDir = Path.GetTempPath(),
            VersionFilter = null,
            AuthTokenEnvVar = null,
            MinLogLevel = LogLevel.None,
        };
        var result = await runner.RunAsync(["--list"]);
        result.ShouldBe(0);
    }

    [Fact]
    public async Task RunAsync_DefaultProperties_HaveExpectedValues()
    {
        var runner = new SchemaDesignPipelineRunner
        {
            VersionCollector = new StaticVersionCollector([]),
            Pipeline = _ => Task.CompletedTask,
            OutputDir = Path.GetTempPath(),
        };
        runner.OutputFilePattern.ShouldBe("schema-v{version}.json");
        runner.DefaultParallelism.ShouldBe(6);
        runner.UserAgent.ShouldBe("FrenchExDev-SchemaScrape/1.0");
        runner.AuthTokenEnvVar.ShouldBe("GITHUB_TOKEN");
        runner.VersionFilter.ShouldBeNull();
        runner.MinLogLevel.ShouldBe(LogLevel.Information);
        var result = await runner.RunAsync(["--list"]);
        result.ShouldBe(0);
    }

    internal static SchemaDesignPipelineRunner CreateRunner(
        IEnumerable<string> versions, string? outputDir = null)
    {
        return new SchemaDesignPipelineRunner
        {
            VersionCollector = new StaticVersionCollector(versions),
            Pipeline = _ => Task.CompletedTask,
            OutputDir = outputDir ?? Path.GetTempPath(),
            AuthTokenEnvVar = null,
            MinLogLevel = LogLevel.None,
        };
    }
}

public sealed class RunnerDownloadTests
{
    [Fact]
    public async Task RunAsync_DownloadSuccess_ReturnsZero()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"dl-success-{Guid.NewGuid():N}");
        try
        {
            var pipeline = new SchemaDesignPipeline()
                .Use(next => async ctx => { ctx.Content = "downloaded"; await next(ctx); })
                .Build();
            var runner = new SchemaDesignPipelineRunner
            {
                VersionCollector = new StaticVersionCollector(["1.0.0"]),
                Pipeline = pipeline, OutputDir = tempDir, DefaultParallelism = 1,
                AuthTokenEnvVar = null, MinLogLevel = LogLevel.None,
            };
            var result = await runner.RunAsync([]);
            result.ShouldBe(0);
        }
        finally { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); }
    }

    [Fact]
    public async Task RunAsync_DownloadFailure_ReturnsOne()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"dl-fail-{Guid.NewGuid():N}");
        try
        {
            SchemaVersionDelegate failingPipeline = _ => throw new InvalidOperationException("download failed");
            var runner = new SchemaDesignPipelineRunner
            {
                VersionCollector = new StaticVersionCollector(["1.0.0"]),
                Pipeline = failingPipeline, OutputDir = tempDir, DefaultParallelism = 1,
                AuthTokenEnvVar = null, MinLogLevel = LogLevel.None,
            };
            var result = await runner.RunAsync([]);
            result.ShouldBe(1);
        }
        finally { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); }
    }

    [Fact]
    public async Task RunAsync_ParallelAndOutput_ParsedCorrectly()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"dl-parallel-{Guid.NewGuid():N}");
        try
        {
            var pipeline = new SchemaDesignPipeline()
                .Use(next => async ctx => { ctx.Content = "ok"; await next(ctx); })
                .Build();
            var runner = new SchemaDesignPipelineRunner
            {
                VersionCollector = new StaticVersionCollector(["1.0.0", "2.0.0"]),
                Pipeline = pipeline, OutputDir = "unused-default", DefaultParallelism = 1,
                AuthTokenEnvVar = null, MinLogLevel = LogLevel.None,
            };
            var result = await runner.RunAsync(["--parallel", "2", "--output", tempDir]);
            result.ShouldBe(0);
        }
        finally { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); }
    }

    [Fact]
    public async Task RunAsync_PartialFailure_ReturnsOne()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"dl-partial-{Guid.NewGuid():N}");
        try
        {
            SchemaVersionDelegate partialFailPipeline = ctx =>
            {
                if (ctx.Version == "1.0.0") throw new InvalidOperationException("boom");
                ctx.Content = "ok";
                return Task.CompletedTask;
            };
            var runner = new SchemaDesignPipelineRunner
            {
                VersionCollector = new StaticVersionCollector(["1.0.0", "2.0.0"]),
                Pipeline = partialFailPipeline, OutputDir = tempDir, DefaultParallelism = 1,
                AuthTokenEnvVar = null, MinLogLevel = LogLevel.None,
            };
            var result = await runner.RunAsync([]);
            result.ShouldBe(1);
        }
        finally { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); }
    }
}

public sealed class RunnerAuthTests
{
    [Fact]
    public async Task RunAsync_AuthTokenEnvVarNull_NoAuthHeader()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"dl-noauth-{Guid.NewGuid():N}");
        try
        {
            var pipeline = new SchemaDesignPipeline()
                .Use(next => async ctx => { ctx.Content = "ok"; await next(ctx); }).Build();
            var runner = new SchemaDesignPipelineRunner
            {
                VersionCollector = new StaticVersionCollector(["1.0.0"]),
                Pipeline = pipeline, OutputDir = tempDir,
                AuthTokenEnvVar = null, MinLogLevel = LogLevel.None,
            };
            var result = await runner.RunAsync([]);
            result.ShouldBe(0);
        }
        finally { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); }
    }

    [Fact]
    public async Task RunAsync_AuthTokenEnvVarSet_ButEnvVarEmpty_NoAuthHeader()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"dl-emptytoken-{Guid.NewGuid():N}");
        var uniqueEnvVar = $"TEST_TOKEN_{Guid.NewGuid():N}";
        try
        {
            Environment.SetEnvironmentVariable(uniqueEnvVar, "");
            var pipeline = new SchemaDesignPipeline()
                .Use(next => async ctx => { ctx.Content = "ok"; await next(ctx); }).Build();
            var runner = new SchemaDesignPipelineRunner
            {
                VersionCollector = new StaticVersionCollector(["1.0.0"]),
                Pipeline = pipeline, OutputDir = tempDir,
                AuthTokenEnvVar = uniqueEnvVar, MinLogLevel = LogLevel.None,
            };
            var result = await runner.RunAsync([]);
            result.ShouldBe(0);
        }
        finally
        {
            Environment.SetEnvironmentVariable(uniqueEnvVar, null);
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task RunAsync_AuthTokenEnvVarSet_WithToken_AddsBearer()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"dl-authtoken-{Guid.NewGuid():N}");
        var uniqueEnvVar = $"TEST_TOKEN_{Guid.NewGuid():N}";
        try
        {
            Environment.SetEnvironmentVariable(uniqueEnvVar, "my-secret-token");
            var pipeline = new SchemaDesignPipeline()
                .Use(next => async ctx => { ctx.Content = "ok"; await next(ctx); }).Build();
            var runner = new SchemaDesignPipelineRunner
            {
                VersionCollector = new StaticVersionCollector(["1.0.0"]),
                Pipeline = pipeline, OutputDir = tempDir,
                AuthTokenEnvVar = uniqueEnvVar, MinLogLevel = LogLevel.None,
            };
            var result = await runner.RunAsync([]);
            result.ShouldBe(0);
        }
        finally
        {
            Environment.SetEnvironmentVariable(uniqueEnvVar, null);
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }
}

public sealed class RunnerMissingFilterTests
{
    [Fact]
    public async Task RunAsync_MissingOnly_FiltersAlreadyDownloaded()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"dl-missing-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(tempDir);
            File.WriteAllText(Path.Combine(tempDir, "schema-v1.0.0.json"), "existing");
            var versionsProcessed = new List<string>();
            SchemaVersionDelegate trackingPipeline = ctx =>
            { versionsProcessed.Add(ctx.Version); ctx.Content = "new"; return Task.CompletedTask; };
            var runner = new SchemaDesignPipelineRunner
            {
                VersionCollector = new StaticVersionCollector(["1.0.0", "2.0.0"]),
                Pipeline = trackingPipeline, OutputDir = tempDir, DefaultParallelism = 1,
                AuthTokenEnvVar = null, MinLogLevel = LogLevel.None,
            };
            var result = await runner.RunAsync(["--missing"]);
            result.ShouldBe(0);
            versionsProcessed.ShouldBe(["2.0.0"]);
        }
        finally { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); }
    }

    [Fact]
    public async Task RunAsync_MissingOnly_NonExistentOutputDir_ReturnsAll()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"dl-missing-nodir-{Guid.NewGuid():N}");
        try
        {
            var versionsProcessed = new List<string>();
            SchemaVersionDelegate trackingPipeline = ctx =>
            { versionsProcessed.Add(ctx.Version); ctx.Content = "new"; return Task.CompletedTask; };
            var runner = new SchemaDesignPipelineRunner
            {
                VersionCollector = new StaticVersionCollector(["1.0.0", "2.0.0"]),
                Pipeline = trackingPipeline, OutputDir = tempDir, DefaultParallelism = 1,
                AuthTokenEnvVar = null, MinLogLevel = LogLevel.None,
            };
            var result = await runner.RunAsync(["--missing"]);
            result.ShouldBe(0);
            versionsProcessed.Count.ShouldBe(2);
        }
        finally { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); }
    }

    [Fact]
    public async Task RunAsync_MissingOnly_AllVersionsExist_NoProcessing()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"dl-all-exist-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(tempDir);
            File.WriteAllText(Path.Combine(tempDir, "schema-v1.0.0.json"), "existing");
            File.WriteAllText(Path.Combine(tempDir, "schema-v2.0.0.json"), "existing");
            var runner = RunnerListAndFilterTests.CreateRunner(["1.0.0", "2.0.0"], tempDir);
            var result = await runner.RunAsync(["--missing"]);
            result.ShouldBe(0);
        }
        finally { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); }
    }

    [Fact]
    public async Task RunAsync_OutputFilePattern_WithNonDefaultPattern()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"dl-pattern-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(tempDir);
            File.WriteAllText(Path.Combine(tempDir, "compose-1.0.0.yaml"), "existing");
            var runner = new SchemaDesignPipelineRunner
            {
                VersionCollector = new StaticVersionCollector(["1.0.0", "2.0.0"]),
                Pipeline = _ => Task.CompletedTask, OutputDir = tempDir,
                OutputFilePattern = "compose-{version}.yaml",
                AuthTokenEnvVar = null, MinLogLevel = LogLevel.None,
            };
            var result = await runner.RunAsync(["--list", "--missing"]);
            result.ShouldBe(0);
        }
        finally { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); }
    }

    [Fact]
    public async Task RunAsync_OutputFilePattern_WithoutVersionPlaceholder_NoFiltering()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"dl-badpattern-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(tempDir);
            File.WriteAllText(Path.Combine(tempDir, "something.json"), "existing");
            var versionsProcessed = new List<string>();
            SchemaVersionDelegate trackingPipeline = ctx =>
            { versionsProcessed.Add(ctx.Version); return Task.CompletedTask; };
            var runner = new SchemaDesignPipelineRunner
            {
                VersionCollector = new StaticVersionCollector(["1.0.0"]),
                Pipeline = trackingPipeline, OutputDir = tempDir,
                OutputFilePattern = "no-placeholder-here.json", DefaultParallelism = 1,
                AuthTokenEnvVar = null, MinLogLevel = LogLevel.None,
            };
            var result = await runner.RunAsync(["--missing"]);
            result.ShouldBe(0);
            versionsProcessed.ShouldBe(["1.0.0"]);
        }
        finally { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); }
    }

    [Fact]
    public async Task RunAsync_ExistingFiles_ThatDoNotMatchPattern_AreNotFiltered()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"dl-nomatch-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(tempDir);
            File.WriteAllText(Path.Combine(tempDir, "unrelated.txt"), "data");
            var versionsProcessed = new List<string>();
            SchemaVersionDelegate trackingPipeline = ctx =>
            { versionsProcessed.Add(ctx.Version); return Task.CompletedTask; };
            var runner = new SchemaDesignPipelineRunner
            {
                VersionCollector = new StaticVersionCollector(["1.0.0"]),
                Pipeline = trackingPipeline, OutputDir = tempDir, DefaultParallelism = 1,
                AuthTokenEnvVar = null, MinLogLevel = LogLevel.None,
            };
            var result = await runner.RunAsync(["--missing"]);
            result.ShouldBe(0);
            versionsProcessed.ShouldBe(["1.0.0"]);
        }
        finally { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); }
    }
}

// ── VersionFilters Tests ────────────────────────────────────────────────────

public sealed class VersionFiltersTests
{
    [Fact]
    public void LatestPatchPerMinor_NormalCase_KeepsHighestPatch()
    {
        var versions = new List<string> { "1.0.1", "1.0.3", "1.1.0", "1.1.2" };

        var result = VersionFilters.LatestPatchPerMinor(versions);

        result.ShouldBe(["1.0.3", "1.1.2"]);
    }

    [Fact]
    public void LatestPatchPerMinor_SingleVersion_ReturnsThatVersion()
    {
        var result = VersionFilters.LatestPatchPerMinor(["5.2.1"]);

        result.ShouldBe(["5.2.1"]);
    }

    [Fact]
    public void LatestPatchPerMinor_EmptyList_ReturnsEmpty()
    {
        var result = VersionFilters.LatestPatchPerMinor([]);

        result.ShouldBeEmpty();
    }

    [Fact]
    public void LatestPatchPerMinor_VersionsWithLessThan3Parts_FilteredOut()
    {
        var versions = new List<string> { "1.0", "2", "3.0.0" };

        var result = VersionFilters.LatestPatchPerMinor(versions);

        result.ShouldBe(["3.0.0"]);
    }

    [Fact]
    public void LatestPatchPerMinor_NonNumericMajor_FilteredOut()
    {
        var result = VersionFilters.LatestPatchPerMinor(["abc.0.0", "1.0.0"]);

        result.ShouldBe(["1.0.0"]);
    }

    [Fact]
    public void LatestPatchPerMinor_NonNumericMinor_FilteredOut()
    {
        var result = VersionFilters.LatestPatchPerMinor(["1.abc.0", "1.0.0"]);

        result.ShouldBe(["1.0.0"]);
    }

    [Fact]
    public void LatestPatchPerMinor_NonNumericPatch_FilteredOut()
    {
        var result = VersionFilters.LatestPatchPerMinor(["1.0.abc", "1.0.1"]);

        result.ShouldBe(["1.0.1"]);
    }

    [Fact]
    public void LatestPatchPerMinor_ResultSortedByMajorThenMinor()
    {
        var versions = new List<string> { "2.1.0", "1.0.0", "3.0.0", "1.1.0", "2.0.0" };

        var result = VersionFilters.LatestPatchPerMinor(versions);

        result.ShouldBe(["1.0.0", "1.1.0", "2.0.0", "2.1.0", "3.0.0"]);
    }

    [Fact]
    public void LatestPatchPerMinor_MultipleMajorVersions_GroupedCorrectly()
    {
        var versions = new List<string>
        {
            "1.0.0", "1.0.5", "1.0.3",
            "2.0.0", "2.0.1",
            "2.1.0", "2.1.3", "2.1.1"
        };

        var result = VersionFilters.LatestPatchPerMinor(versions);

        result.ShouldBe(["1.0.5", "2.0.1", "2.1.3"]);
    }

    [Fact]
    public void LatestPatchPerMinor_AllNonNumeric_ReturnsEmpty()
    {
        var result = VersionFilters.LatestPatchPerMinor(["a.b.c", "x.y.z"]);

        result.ShouldBeEmpty();
    }

    [Fact]
    public void LatestPatchPerMinor_AllTooFewParts_ReturnsEmpty()
    {
        var result = VersionFilters.LatestPatchPerMinor(["1.0", "2"]);

        result.ShouldBeEmpty();
    }
}

// ── SchemaVersionContext Tests ──────────────────────────────────────────────

public sealed class SchemaVersionContextTests
{
    [Fact]
    public void RequiredProperties_AreSet()
    {
        var logger = NullLogger.Instance;
        var client = new HttpClient();

        var ctx = new SchemaVersionContext
        {
            Version = "1.0.0",
            OutputDir = "/tmp/output",
            Logger = logger,
            HttpClient = client,
            OutputFilePattern = "schema-{version}.json",
        };

        ctx.Version.ShouldBe("1.0.0");
        ctx.OutputDir.ShouldBe("/tmp/output");
        ctx.Logger.ShouldBe(logger);
        ctx.HttpClient.ShouldBe(client);
        ctx.OutputFilePattern.ShouldBe("schema-{version}.json");
        ctx.Content.ShouldBeNull();
        ctx.OutputFilePath.ShouldBeNull();
        ctx.Progress.ShouldBeNull();
    }

    [Fact]
    public void MutableProperties_CanBeSet()
    {
        var ctx = new SchemaVersionContext
        {
            Version = "1.0.0",
            OutputDir = "/tmp",
            Logger = NullLogger.Instance,
            HttpClient = new HttpClient(),
            OutputFilePattern = "s-{version}.json",
        };

        ctx.Content = "hello";
        ctx.OutputFilePath = "/tmp/s-1.0.0.json";

        ctx.Content.ShouldBe("hello");
        ctx.OutputFilePath.ShouldBe("/tmp/s-1.0.0.json");
    }

    [Fact]
    public void Progress_CanBeSetViaInit()
    {
        var progress = new SchemaVersionProgressInfo("1.0.0");
        var ctx = new SchemaVersionContext
        {
            Version = "1.0.0",
            OutputDir = "/tmp",
            Logger = NullLogger.Instance,
            HttpClient = new HttpClient(),
            OutputFilePattern = "s-{version}.json",
            Progress = progress,
        };

        ctx.Progress.ShouldBe(progress);
    }
}
