using System.Collections.Concurrent;
using FrenchExDev.Net.BinaryWrapper.Design;
using FrenchExDev.Net.BinaryWrapper.Design.Lib;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace FrenchExDev.Net.BinaryWrapper.Design.Lib.Tests;

// ── DesignPipeline Tests ────────────────────────────────────────────────────

public class DesignPipelineTests
{
    [Fact]
    public async Task Build_WithNoMiddleware_CompletesImmediately()
    {
        var pipeline = new DesignPipeline();
        var handler = pipeline.Build();
        var ctx = CreateContext();

        await handler(ctx);
        // No exception = success; terminal is a no-op
    }

    [Fact]
    public async Task Build_SingleMiddleware_WrapsTerminal()
    {
        var called = false;
        var pipeline = new DesignPipeline()
            .Use(next => async ctx =>
            {
                called = true;
                await next(ctx);
            });

        var handler = pipeline.Build();
        await handler(CreateContext());

        called.ShouldBeTrue();
    }

    [Fact]
    public async Task Build_MultipleMiddleware_ExecuteOuterFirst()
    {
        var order = new List<int>();
        var pipeline = new DesignPipeline()
            .Use(next => async ctx => { order.Add(1); await next(ctx); order.Add(4); })
            .Use(next => async ctx => { order.Add(2); await next(ctx); order.Add(3); });

        var handler = pipeline.Build();
        await handler(CreateContext());

        order.ShouldBe([1, 2, 3, 4]);
    }

    [Fact]
    public void Use_ReturnsSameInstance()
    {
        var pipeline = new DesignPipeline();
        var result = pipeline.Use(next => next);
        result.ShouldBeSameAs(pipeline);
    }

    [Fact]
    public async Task Build_MiddlewareCanDoTeardown()
    {
        var teardownRan = false;
        var pipeline = new DesignPipeline()
            .Use(next => async ctx =>
            {
                try
                {
                    await next(ctx);
                }
                finally
                {
                    teardownRan = true;
                }
            })
            .Use(next => ctx => throw new InvalidOperationException("boom"));

        var handler = pipeline.Build();

        await Should.ThrowAsync<InvalidOperationException>(() => handler(CreateContext()));
        teardownRan.ShouldBeTrue();
    }

    private static VersionContext CreateContext() => new()
    {
        Version = "1.0.0",
        RuntimeBinary = "podman",
        Logger = NullLogger.Instance,
        OutputDir = "/tmp",
        RunProcess = _ => Task.FromResult(""),
        ActiveContainers = new ConcurrentBag<string>(),
        ActiveImages = new ConcurrentDictionary<string, byte>(),
    };
}

// ── VersionContext Tests ────────────────────────────────────────────────────

public class VersionContextTests
{
    [Fact]
    public void MutableState_DefaultsToNull()
    {
        var ctx = new VersionContext
        {
            Version = "1.0.0",
            RuntimeBinary = "podman",
            Logger = NullLogger.Instance,
            OutputDir = "/tmp",
            RunProcess = _ => Task.FromResult(""),
            ActiveContainers = new ConcurrentBag<string>(),
            ActiveImages = new ConcurrentDictionary<string, byte>(),
        };

        ctx.ImageTag.ShouldBeNull();
        ctx.ContainerId.ShouldBeNull();
        ctx.Result.ShouldBeNull();
    }

    [Fact]
    public void MutableState_CanBeSet()
    {
        var ctx = new VersionContext
        {
            Version = "2.0.0",
            RuntimeBinary = "docker",
            Logger = NullLogger.Instance,
            OutputDir = "/out",
            RunProcess = _ => Task.FromResult(""),
            ActiveContainers = new ConcurrentBag<string>(),
            ActiveImages = new ConcurrentDictionary<string, byte>(),
        };

        ctx.ImageTag = "test:1.0";
        ctx.ContainerId = "abc123";

        ctx.ImageTag.ShouldBe("test:1.0");
        ctx.ContainerId.ShouldBe("abc123");
    }
}

// ── DesignPipelineExtensions Tests ──────────────────────────────────────────

public class DesignPipelineExtensionsTests
{
    [Fact]
    public async Task UseContainer_CreatesAndCleansUp()
    {
        var commands = new List<string[]>();
        var ctx = CreateContext(commands);

        var pipeline = new DesignPipeline()
            .UseContainer();

        ctx.ImageTag = "test-image:1.0";
        var handler = pipeline.Build();
        await handler(ctx);

        // Should have called: run -d <image> sleep infinity
        commands[0][0].ShouldBe("podman");
        commands[0][1].ShouldBe("run");
        commands[0][2].ShouldBe("-d");
        commands[0][3].ShouldBe("test-image:1.0");

        // Should have called: rm -f <cid>
        commands[1][0].ShouldBe("podman");
        commands[1][1].ShouldBe("rm");
        commands[1][2].ShouldBe("-f");
    }

    [Fact]
    public async Task UseContainer_SetsContainerId()
    {
        var ctx = CreateContext(runResult: "container-id-123\n");
        ctx.ImageTag = "test:1.0";

        string? capturedContainerId = null;
        var pipeline = new DesignPipeline()
            .UseContainer()
            .Use(next => async c =>
            {
                capturedContainerId = c.ContainerId;
                await next(c);
            });

        await pipeline.Build()(ctx);
        capturedContainerId.ShouldBe("container-id-123");
    }

    [Fact]
    public async Task UseContainer_CleansUp_EvenOnException()
    {
        var commands = new List<string[]>();
        var ctx = CreateContext(commands);
        ctx.ImageTag = "test:1.0";

        var pipeline = new DesignPipeline()
            .UseContainer()
            .Use(next => ctx => throw new InvalidOperationException("boom"));

        await Should.ThrowAsync<InvalidOperationException>(() => pipeline.Build()(ctx));

        // Cleanup rm -f should still have been called
        commands.ShouldContain(c => c[1] == "rm" && c[2] == "-f");
    }

    [Fact]
    public async Task UseContainer_AddsToActiveContainers()
    {
        var ctx = CreateContext(runResult: "cid-abc\n");
        ctx.ImageTag = "test:1.0";

        string? captured = null;
        var pipeline = new DesignPipeline()
            .UseContainer()
            .Use(next => async c =>
            {
                captured = c.ActiveContainers.FirstOrDefault();
                await next(c);
            });

        await pipeline.Build()(ctx);
        captured.ShouldBe("cid-abc");
    }

    [Fact]
    public async Task UseImageBuild_BuildsNewImage_WhenNotExists()
    {
        var commands = new List<string[]>();
        var callIndex = 0;
        var ctx = CreateContext(args =>
        {
            commands.Add(args);
            callIndex++;
            // First call is image inspect → throw (image doesn't exist)
            if (callIndex == 1) throw new InvalidOperationException("not found");
            // Second call is run -d (build container)
            if (callIndex == 2) return "build-cid\n";
            return "";
        });

        var pipeline = new DesignPipeline()
            .UseImageBuild("myimg", "alpine:3.19", v => $"apk add tool={v}");

        await pipeline.Build()(ctx);

        // First: image inspect
        commands[0].ShouldContain("image");
        commands[0].ShouldContain("inspect");

        // Second: run -d alpine:3.19 sleep infinity
        commands[1].ShouldContain("run");
        commands[1].ShouldContain("alpine:3.19");

        // Third: exec <cid> sh -c <install script>
        commands[2].ShouldContain("exec");

        // Fourth: commit
        commands[3].ShouldContain("commit");
        commands[3].ShouldContain("myimg:1.0.0");

        ctx.ImageTag.ShouldBe("myimg:1.0.0");
    }

    [Fact]
    public async Task UseImageBuild_SkipsBuild_WhenImageExists()
    {
        var commands = new List<string[]>();
        var ctx = CreateContext(commands);

        var pipeline = new DesignPipeline()
            .UseImageBuild("myimg", "alpine:3.19", v => $"install {v}");

        await pipeline.Build()(ctx);

        // image inspect succeeds → no run/exec/commit calls (just inspect + rmi cleanup)
        commands.ShouldNotContain(c => c.Contains("commit"));
    }

    [Fact]
    public async Task UseImageBuild_CleansUpImage_AfterNext()
    {
        var commands = new List<string[]>();
        var ctx = CreateContext(commands);

        var pipeline = new DesignPipeline()
            .UseImageBuild("myimg", "alpine:3.19", v => $"install {v}");

        await pipeline.Build()(ctx);

        // Should have rmi cleanup at the end
        commands.ShouldContain(c => c.Contains("rmi"));
    }

    [Fact]
    public async Task UseImageBuild_TracksActiveImages()
    {
        var callIndex = 0;
        var ctx = CreateContext(args =>
        {
            callIndex++;
            if (callIndex == 1) throw new InvalidOperationException("not found");
            if (callIndex == 2) return "cid\n";
            return "";
        });

        bool hadImage = false;
        var pipeline = new DesignPipeline()
            .UseImageBuild("myimg", "alpine:3.19", v => $"install {v}")
            .Use(next => async c =>
            {
                hadImage = c.ActiveImages.ContainsKey("myimg:1.0.0");
                await next(c);
            });

        await pipeline.Build()(ctx);
        hadImage.ShouldBeTrue();
    }

    [Fact]
    public async Task UseInlineContainer_InstallsAndCleansUp()
    {
        var commands = new List<string[]>();
        var ctx = CreateContext(args =>
        {
            commands.Add(args);
            return "inline-cid\n";
        });

        var pipeline = new DesignPipeline()
            .UseInlineContainer("alpine:3.19", v => $"apk add tool={v}");

        await pipeline.Build()(ctx);

        // run -d
        commands[0].ShouldContain("run");
        commands[0].ShouldContain("alpine:3.19");

        // exec install
        commands[1].ShouldContain("exec");

        // rm -f cleanup
        commands.Last().ShouldContain("rm");
    }

    [Fact]
    public async Task UseInlineContainer_SetsContainerIdAndTracksActive()
    {
        string? capturedCid = null;
        var ctx = CreateContext(runResult: "inline-cid-42\n");

        var pipeline = new DesignPipeline()
            .UseInlineContainer("alpine:3.19", v => $"install {v}")
            .Use(next => async c =>
            {
                capturedCid = c.ContainerId;
                await next(c);
            });

        await pipeline.Build()(ctx);
        capturedCid.ShouldBe("inline-cid-42");
        ctx.ActiveContainers.ShouldContain("inline-cid-42");
    }

    [Fact]
    public async Task UseInlineContainer_CleansUp_EvenOnException()
    {
        var commands = new List<string[]>();
        var ctx = CreateContext(args =>
        {
            commands.Add(args);
            return "inline-cid\n";
        });

        var pipeline = new DesignPipeline()
            .UseInlineContainer("alpine:3.19", v => $"install {v}")
            .Use(next => ctx => throw new InvalidOperationException("boom"));

        await Should.ThrowAsync<InvalidOperationException>(() => pipeline.Build()(ctx));

        // rm -f cleanup should still have happened
        commands.ShouldContain(c => c.Contains("rm") && c.Contains("-f"));
    }

    [Fact]
    public async Task UseImageBuild_RmiFailure_SwallowsException()
    {
        var callIndex = 0;
        var ctx = CreateContext(args =>
        {
            callIndex++;
            // image inspect → not found
            if (callIndex == 1) throw new InvalidOperationException("not found");
            // run -d → build container
            if (callIndex == 2) return "build-cid\n";
            // exec, commit, rm -f build container → ok
            if (callIndex <= 5) return "";
            // rmi → throw (simulate failure to remove image)
            throw new InvalidOperationException("rmi failed");
        });

        var pipeline = new DesignPipeline()
            .UseImageBuild("myimg", "alpine:3.19", v => $"install {v}");

        // Should not throw despite rmi failure
        await pipeline.Build()(ctx);
    }

    [Fact]
    public async Task UseImageBuild_BuildContainerCleanup_OnInstallFailure()
    {
        var commands = new List<string[]>();
        var callIndex = 0;
        var ctx = CreateContext(args =>
        {
            commands.Add(args);
            callIndex++;
            // image inspect → not found
            if (callIndex == 1) throw new InvalidOperationException("not found");
            // run -d → build container
            if (callIndex == 2) return "build-cid\n";
            // exec (install script) → throw
            if (callIndex == 3) throw new InvalidOperationException("install failed");
            return "";
        });

        var pipeline = new DesignPipeline()
            .UseImageBuild("myimg", "alpine:3.19", v => $"install {v}");

        // The install failure should propagate (no image was committed, but rmi still happens in outer finally)
        await Should.ThrowAsync<InvalidOperationException>(() => pipeline.Build()(ctx));

        // Build container should still be cleaned up (rm -f)
        commands.ShouldContain(c => c.Contains("rm") && c.Contains("-f") && c.Contains("build-cid"));
    }

    [Fact]
    public async Task UseImageBuild_CustomShell()
    {
        var commands = new List<string[]>();
        var callIndex = 0;
        var ctx = CreateContext(args =>
        {
            commands.Add(args);
            callIndex++;
            if (callIndex == 1) throw new InvalidOperationException("not found");
            if (callIndex == 2) return "cid\n";
            return "";
        });

        var pipeline = new DesignPipeline()
            .UseImageBuild("myimg", "alpine:3.19", v => $"install {v}", shell: "bash");

        await pipeline.Build()(ctx);

        // exec should use "bash" not "sh"
        commands[2].ShouldContain("bash");
    }

    [Fact]
    public async Task UseInlineContainer_CustomShell()
    {
        var commands = new List<string[]>();
        var ctx = CreateContext(args =>
        {
            commands.Add(args);
            return "cid\n";
        });

        var pipeline = new DesignPipeline()
            .UseInlineContainer("alpine:3.19", v => $"install {v}", shell: "bash");

        await pipeline.Build()(ctx);

        commands[1].ShouldContain("bash");
    }

    [Fact]
    public async Task UseScraper_ScrapesHelpAndSetsResult()
    {
        var outputDir = Path.Combine(Path.GetTempPath(), $"scraper-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(outputDir);

        try
        {
            var ctx = new VersionContext
            {
                Version = "1.0.0",
                RuntimeBinary = "podman",
                Logger = NullLogger.Instance,
                OutputDir = outputDir,
                RunProcess = _ => Task.FromResult(""),
                ActiveContainers = new ConcurrentBag<string>(),
                ActiveImages = new ConcurrentDictionary<string, byte>(),
            };
            ctx.RunHelp = _ => Task.FromResult("A test CLI\n\nCommands:\n  sub1  A subcommand\n");

            var pipeline = new DesignPipeline()
                .UseScraper("mytool", (version, logger) => new StandardHelpParser());

            await pipeline.Build()(ctx);

            ctx.Result.ShouldNotBeNull();
            ctx.Result.BinaryName.ShouldBe("mytool");
            ctx.Result.Root.ShouldNotBeNull();

            // Output file should have been written
            var expectedFile = Path.Combine(outputDir, "mytool-1.0.0.json");
            File.Exists(expectedFile).ShouldBeTrue();
        }
        finally
        {
            try { Directory.Delete(outputDir, true); } catch { }
        }
    }

    [Fact]
    public async Task UseScraper_CustomOutputFilePattern()
    {
        var outputDir = Path.Combine(Path.GetTempPath(), $"scraper-pattern-{Guid.NewGuid():N}");
        Directory.CreateDirectory(outputDir);

        try
        {
            var ctx = new VersionContext
            {
                Version = "2.5.0",
                RuntimeBinary = "podman",
                Logger = NullLogger.Instance,
                OutputDir = outputDir,
                RunProcess = _ => Task.FromResult(""),
                ActiveContainers = new ConcurrentBag<string>(),
                ActiveImages = new ConcurrentDictionary<string, byte>(),
            };
            ctx.RunHelp = _ => Task.FromResult("A tool\n");

            var pipeline = new DesignPipeline()
                .UseScraper("mytool", (v, l) => new StandardHelpParser(),
                    outputFilePattern: "custom-{version}.json");

            await pipeline.Build()(ctx);

            var expectedFile = Path.Combine(outputDir, "custom-2.5.0.json");
            File.Exists(expectedFile).ShouldBeTrue();
        }
        finally
        {
            try { Directory.Delete(outputDir, true); } catch { }
        }
    }

    [Fact]
    public async Task UseScraper_CustomHelpFlag()
    {
        var outputDir = Path.Combine(Path.GetTempPath(), $"scraper-flag-{Guid.NewGuid():N}");
        Directory.CreateDirectory(outputDir);

        try
        {
            var helpArgs = new List<string[]>();
            var ctx = new VersionContext
            {
                Version = "1.0.0",
                RuntimeBinary = "podman",
                Logger = NullLogger.Instance,
                OutputDir = outputDir,
                RunProcess = args => Task.FromResult(""),
                ActiveContainers = new ConcurrentBag<string>(),
                ActiveImages = new ConcurrentDictionary<string, byte>(),
            };
            ctx.RunHelp = args =>
            {
                helpArgs.Add(args);
                return Task.FromResult("A tool\n");
            };

            var pipeline = new DesignPipeline()
                .UseScraper("mytool", (v, l) => new StandardHelpParser(), helpFlag: "-h");

            await pipeline.Build()(ctx);

            // The help args should use -h instead of --help
            helpArgs[0].ShouldContain("-h");
        }
        finally
        {
            try { Directory.Delete(outputDir, true); } catch { }
        }
    }

    [Fact]
    public async Task UseScraper_CallsNextAfterScraping()
    {
        var outputDir = Path.Combine(Path.GetTempPath(), $"scraper-next-{Guid.NewGuid():N}");
        Directory.CreateDirectory(outputDir);

        try
        {
            var ctx = new VersionContext
            {
                Version = "1.0.0",
                RuntimeBinary = "podman",
                Logger = NullLogger.Instance,
                OutputDir = outputDir,
                RunProcess = _ => Task.FromResult(""),
                ActiveContainers = new ConcurrentBag<string>(),
                ActiveImages = new ConcurrentDictionary<string, byte>(),
            };
            ctx.RunHelp = _ => Task.FromResult("A tool\n");

            var nextCalled = false;
            var pipeline = new DesignPipeline()
                .UseScraper("mytool", (v, l) => new StandardHelpParser())
                .Use(next => async c => { nextCalled = true; await next(c); });

            await pipeline.Build()(ctx);
            nextCalled.ShouldBeTrue();
        }
        finally
        {
            try { Directory.Delete(outputDir, true); } catch { }
        }
    }

    private static VersionContext CreateContext(List<string[]>? commands = null, string runResult = "")
    {
        return new VersionContext
        {
            Version = "1.0.0",
            RuntimeBinary = "podman",
            Logger = NullLogger.Instance,
            OutputDir = "/tmp/output",
            RunProcess = args =>
            {
                commands?.Add(args);
                return Task.FromResult(runResult);
            },
            ActiveContainers = new ConcurrentBag<string>(),
            ActiveImages = new ConcurrentDictionary<string, byte>(),
        };
    }

    private static VersionContext CreateContext(Func<string[], string> handler)
    {
        return new VersionContext
        {
            Version = "1.0.0",
            RuntimeBinary = "podman",
            Logger = NullLogger.Instance,
            OutputDir = "/tmp/output",
            RunProcess = args => Task.FromResult(handler(args)),
            ActiveContainers = new ConcurrentBag<string>(),
            ActiveImages = new ConcurrentDictionary<string, byte>(),
        };
    }
}

// ── DesignPipelineRunner Tests ──────────────────────────────────────────────

public class DesignPipelineRunnerTests
{
    [Fact]
    public async Task RunAsync_ListFlag_ReturnsZero()
    {
        var runner = CreateRunner(["1.0.0", "2.0.0"]);
        var result = await runner.RunAsync(["--list"]);
        result.ShouldBe(0);
    }

    [Fact]
    public async Task RunAsync_AllSucceed_ReturnsZero()
    {
        var runner = CreateRunner(["1.0.0"]);
        var result = await runner.RunAsync([]);
        result.ShouldBe(0);
    }

    [Fact]
    public async Task RunAsync_SomeFail_ReturnsOne()
    {
        var failOnVersion = "2.0.0";
        var pipeline = new DesignPipeline()
            .Use(next => ctx =>
            {
                if (ctx.Version == failOnVersion)
                    throw new InvalidOperationException("fail");
                return next(ctx);
            });

        var runner = new DesignPipelineRunner
        {
            VersionCollector = new StaticVersionCollector(["1.0.0", "2.0.0"]),
            Pipeline = pipeline.Build(),
            OutputDir = Path.GetTempPath(),
            RunProcess = _ => Task.FromResult(""),
            DefaultParallelism = 1,
        };

        var result = await runner.RunAsync([]);
        result.ShouldBe(1);
    }

    [Fact]
    public async Task RunAsync_ParsesParallelArg()
    {
        var runner = CreateRunner(["1.0.0"]);
        // Just verify it doesn't crash with --parallel
        var result = await runner.RunAsync(["--parallel", "2"]);
        result.ShouldBe(0);
    }

    [Fact]
    public async Task RunAsync_ParsesScrapeParallelArg()
    {
        int? capturedScrapeParallelism = null;
        var pipeline = new DesignPipeline()
            .Use(next => async ctx =>
            {
                capturedScrapeParallelism = ctx.ScrapeParallelism;
                await next(ctx);
            });

        var runner = new DesignPipelineRunner
        {
            VersionCollector = new StaticVersionCollector(["1.0.0"]),
            Pipeline = pipeline.Build(),
            OutputDir = Path.GetTempPath(),
            RunProcess = _ => Task.FromResult(""),
            DefaultParallelism = 1,
        };

        await runner.RunAsync(["--scrape-parallel", "8"]);
        capturedScrapeParallelism.ShouldBe(8);
    }

    [Fact]
    public async Task RunAsync_DefaultScrapeParallelism_IsFour()
    {
        int? capturedScrapeParallelism = null;
        var pipeline = new DesignPipeline()
            .Use(next => async ctx =>
            {
                capturedScrapeParallelism = ctx.ScrapeParallelism;
                await next(ctx);
            });

        var runner = new DesignPipelineRunner
        {
            VersionCollector = new StaticVersionCollector(["1.0.0"]),
            Pipeline = pipeline.Build(),
            OutputDir = Path.GetTempPath(),
            RunProcess = _ => Task.FromResult(""),
            DefaultParallelism = 1,
        };

        await runner.RunAsync([]);
        capturedScrapeParallelism.ShouldBe(4);
    }

    [Fact]
    public async Task RunAsync_ParsesRuntimeArg()
    {
        string? capturedRuntime = null;
        var pipeline = new DesignPipeline()
            .Use(next => async ctx =>
            {
                capturedRuntime = ctx.RuntimeBinary;
                await next(ctx);
            });

        var runner = new DesignPipelineRunner
        {
            VersionCollector = new StaticVersionCollector(["1.0.0"]),
            Pipeline = pipeline.Build(),
            OutputDir = Path.GetTempPath(),
            RunProcess = _ => Task.FromResult(""),
            DefaultParallelism = 1,
        };

        await runner.RunAsync(["--runtime", "docker"]);
        capturedRuntime.ShouldBe("docker");
    }

    [Fact]
    public async Task RunAsync_ParsesMinVersionArg_FiltersVersions()
    {
        var processedVersions = new ConcurrentBag<string>();
        var pipeline = new DesignPipeline()
            .Use(next => async ctx =>
            {
                processedVersions.Add(ctx.Version);
                await next(ctx);
            });

        var runner = new DesignPipelineRunner
        {
            VersionCollector = new StaticVersionCollector(["1.0.0", "2.0.0", "3.0.0"]),
            Pipeline = pipeline.Build(),
            OutputDir = Path.GetTempPath(),
            RunProcess = _ => Task.FromResult(""),
            DefaultParallelism = 1,
        };

        await runner.RunAsync(["--min-version", "2.0.0"]);
        processedVersions.ShouldNotContain("1.0.0");
        processedVersions.ShouldContain("2.0.0");
        processedVersions.ShouldContain("3.0.0");
    }

    [Fact]
    public async Task RunAsync_ParsesOutputArg()
    {
        string? capturedOutput = null;
        var pipeline = new DesignPipeline()
            .Use(next => async ctx =>
            {
                capturedOutput = ctx.OutputDir;
                await next(ctx);
            });

        var customOutput = Path.Combine(Path.GetTempPath(), "custom-output-test");
        var runner = new DesignPipelineRunner
        {
            VersionCollector = new StaticVersionCollector(["1.0.0"]),
            Pipeline = pipeline.Build(),
            OutputDir = "/default",
            RunProcess = _ => Task.FromResult(""),
            DefaultParallelism = 1,
        };

        try
        {
            await runner.RunAsync(["--output", customOutput]);
            capturedOutput.ShouldBe(customOutput);
        }
        finally
        {
            try { Directory.Delete(customOutput, true); } catch { }
        }
    }

    [Fact]
    public async Task RunAsync_CrashRecovery_CleansUpStragglers()
    {
        var cleanupCommands = new ConcurrentBag<string[]>();

        // Pipeline that leaves a straggler container tracked
        var pipeline = new DesignPipeline()
            .Use(next => async ctx =>
            {
                ctx.ActiveContainers.Add("straggler-cid");
                ctx.ActiveImages.TryAdd("straggler-img:1.0", 0);
                throw new InvalidOperationException("crash");
            });

        var runner = new DesignPipelineRunner
        {
            VersionCollector = new StaticVersionCollector(["1.0.0"]),
            Pipeline = pipeline.Build(),
            OutputDir = Path.GetTempPath(),
            RunProcess = args =>
            {
                cleanupCommands.Add(args);
                return Task.FromResult("");
            },
            DefaultParallelism = 1,
        };

        await runner.RunAsync([]);

        // Crash recovery should clean up straggler container
        cleanupCommands.ShouldContain(c => c.Contains("rm") && c.Contains("straggler-cid"));
        // Crash recovery should clean up straggler image
        cleanupCommands.ShouldContain(c => c.Contains("rmi") && c.Contains("straggler-img:1.0"));
    }

    [Fact]
    public async Task RunAsync_EmptyVersions_ReturnsZero()
    {
        var runner = CreateRunner([]);
        var result = await runner.RunAsync([]);
        result.ShouldBe(0);
    }

    [Fact]
    public async Task RunAsync_CrashRecovery_SwallowsCleanupFailures()
    {
        // Pipeline that leaves stragglers AND cleanup throws
        var pipeline = new DesignPipeline()
            .Use(next => ctx =>
            {
                ctx.ActiveContainers.Add("orphan-cid");
                ctx.ActiveImages.TryAdd("orphan-img:1.0", 0);
                throw new InvalidOperationException("crash");
            });

        var runner = new DesignPipelineRunner
        {
            VersionCollector = new StaticVersionCollector(["1.0.0"]),
            Pipeline = pipeline.Build(),
            OutputDir = Path.GetTempPath(),
            RunProcess = args =>
            {
                // Cleanup calls throw too
                throw new InvalidOperationException("cleanup failed");
            },
            DefaultParallelism = 1,
        };

        // Should not throw — catch {} swallows cleanup failures
        var result = await runner.RunAsync([]);
        result.ShouldBe(1);
    }

    [Fact]
    public async Task RunAsync_NoMinVersion_ProcessesAllVersions()
    {
        var processedVersions = new ConcurrentBag<string>();
        var pipeline = new DesignPipeline()
            .Use(next => async ctx =>
            {
                processedVersions.Add(ctx.Version);
                await next(ctx);
            });

        var runner = new DesignPipelineRunner
        {
            VersionCollector = new StaticVersionCollector(["1.0.0", "2.0.0", "3.0.0"]),
            Pipeline = pipeline.Build(),
            OutputDir = Path.GetTempPath(),
            RunProcess = _ => Task.FromResult(""),
            DefaultParallelism = 1,
            // No DefaultMinVersion set (null)
        };

        await runner.RunAsync([]);
        processedVersions.Count.ShouldBe(3);
    }

    [Fact]
    public async Task RunAsync_DefaultMinVersion_FiltersVersions()
    {
        var processedVersions = new ConcurrentBag<string>();
        var pipeline = new DesignPipeline()
            .Use(next => async ctx =>
            {
                processedVersions.Add(ctx.Version);
                await next(ctx);
            });

        var runner = new DesignPipelineRunner
        {
            VersionCollector = new StaticVersionCollector(["1.0.0", "2.0.0", "3.0.0"]),
            Pipeline = pipeline.Build(),
            OutputDir = Path.GetTempPath(),
            RunProcess = _ => Task.FromResult(""),
            DefaultParallelism = 1,
            DefaultMinVersion = "2.0.0",
        };

        await runner.RunAsync([]);
        processedVersions.ShouldNotContain("1.0.0");
        processedVersions.ShouldContain("2.0.0");
        processedVersions.ShouldContain("3.0.0");
    }

    [Fact]
    public async Task RunAsync_MultipleVersions_ParallelExecution()
    {
        var processedVersions = new ConcurrentBag<string>();
        var pipeline = new DesignPipeline()
            .Use(next => async ctx =>
            {
                processedVersions.Add(ctx.Version);
                await next(ctx);
            });

        var runner = new DesignPipelineRunner
        {
            VersionCollector = new StaticVersionCollector(["1.0.0", "2.0.0", "3.0.0", "4.0.0"]),
            Pipeline = pipeline.Build(),
            OutputDir = Path.GetTempPath(),
            RunProcess = _ => Task.FromResult(""),
            DefaultParallelism = 4,
        };

        var result = await runner.RunAsync([]);
        result.ShouldBe(0);
        processedVersions.Count.ShouldBe(4);
    }

    [Fact]
    public async Task RunAsync_ListFlag_WithMinVersion_FiltersBeforeListing()
    {
        var runner = new DesignPipelineRunner
        {
            VersionCollector = new StaticVersionCollector(["1.0.0", "2.0.0", "3.0.0"]),
            Pipeline = new DesignPipeline().Build(),
            OutputDir = Path.GetTempPath(),
            RunProcess = _ => Task.FromResult(""),
            DefaultParallelism = 1,
        };

        var result = await runner.RunAsync(["--min-version", "2.0.0", "--list"]);
        result.ShouldBe(0);
    }

    [Fact]
    public async Task RunAsync_DefaultRuntimeBinary_IsPodman()
    {
        string? capturedRuntime = null;
        var pipeline = new DesignPipeline()
            .Use(next => async ctx =>
            {
                capturedRuntime = ctx.RuntimeBinary;
                await next(ctx);
            });

        var runner = new DesignPipelineRunner
        {
            VersionCollector = new StaticVersionCollector(["1.0.0"]),
            Pipeline = pipeline.Build(),
            OutputDir = Path.GetTempPath(),
            RunProcess = _ => Task.FromResult(""),
            DefaultParallelism = 1,
            // No RuntimeBinary set — defaults to "podman"
        };

        await runner.RunAsync([]);
        capturedRuntime.ShouldBe("podman");
    }

    [Fact]
    public async Task RunAsync_CreatesOutputDirectory()
    {
        var outputDir = Path.Combine(Path.GetTempPath(), $"runner-mkdir-{Guid.NewGuid():N}");

        try
        {
            var runner = new DesignPipelineRunner
            {
                VersionCollector = new StaticVersionCollector(["1.0.0"]),
                Pipeline = new DesignPipeline().Build(),
                OutputDir = outputDir,
                RunProcess = _ => Task.FromResult(""),
                DefaultParallelism = 1,
            };

            await runner.RunAsync([]);
            Directory.Exists(outputDir).ShouldBeTrue();
        }
        finally
        {
            try { Directory.Delete(outputDir, true); } catch { }
        }
    }

    [Fact]
    public async Task RunAsync_NullRunProcess_FallsBackToDefault_ListOnly()
    {
        // When RunProcess is null, it falls back to ProcessRunnerContainerRuntime.RunProcessAsync.
        // Use --list so we never actually invoke the process runner.
        var runner = new DesignPipelineRunner
        {
            VersionCollector = new StaticVersionCollector(["1.0.0"]),
            Pipeline = new DesignPipeline().Build(),
            OutputDir = Path.GetTempPath(),
            DefaultParallelism = 1,
            // RunProcess intentionally omitted (null)
        };

        var result = await runner.RunAsync(["--list"]);
        result.ShouldBe(0);
    }

    [Fact]
    public async Task RunAsync_AllFail_ReturnsOne()
    {
        var pipeline = new DesignPipeline()
            .Use(next => ctx => throw new InvalidOperationException("fail"));

        var runner = new DesignPipelineRunner
        {
            VersionCollector = new StaticVersionCollector(["1.0.0", "2.0.0"]),
            Pipeline = pipeline.Build(),
            OutputDir = Path.GetTempPath(),
            RunProcess = _ => Task.FromResult(""),
            DefaultParallelism = 2,
        };

        var result = await runner.RunAsync([]);
        result.ShouldBe(1);
    }

    [Fact]
    public async Task RunAsync_ContextPassesCorrectOutputDir()
    {
        string? capturedDir = null;
        var pipeline = new DesignPipeline()
            .Use(next => async ctx =>
            {
                capturedDir = ctx.OutputDir;
                await next(ctx);
            });

        var runner = new DesignPipelineRunner
        {
            VersionCollector = new StaticVersionCollector(["1.0.0"]),
            Pipeline = pipeline.Build(),
            OutputDir = Path.GetTempPath(),
            RunProcess = _ => Task.FromResult(""),
            DefaultParallelism = 1,
        };

        await runner.RunAsync([]);
        capturedDir.ShouldBe(Path.GetTempPath());
    }

    // ── --known-missing tests ─────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_AddKnownMissing_CreatesFile()
    {
        var outputDir = Path.Combine(Path.GetTempPath(), $"km-create-{Guid.NewGuid():N}");
        Directory.CreateDirectory(outputDir);

        try
        {
            var runner = CreateRunnerWithOutputDir(outputDir);
            var result = await runner.RunAsync(["--add-known-missing", "2.0.0,1.0.0,3.0.0"]);
            result.ShouldBe(0);

            var lines = File.ReadAllLines(Path.Combine(outputDir, "_known_missing.txt"));
            lines.ShouldBe(["1.0.0", "2.0.0", "3.0.0"]); // sorted
        }
        finally
        {
            try { Directory.Delete(outputDir, true); } catch { }
        }
    }

    [Fact]
    public async Task RunAsync_AddKnownMissing_MergesWithExisting()
    {
        var outputDir = Path.Combine(Path.GetTempPath(), $"km-merge-{Guid.NewGuid():N}");
        Directory.CreateDirectory(outputDir);

        try
        {
            File.WriteAllLines(Path.Combine(outputDir, "_known_missing.txt"), ["1.0.0", "2.0.0"]);

            var runner = CreateRunnerWithOutputDir(outputDir);
            var result = await runner.RunAsync(["--add-known-missing", "2.0.0,3.0.0"]);
            result.ShouldBe(0);

            var lines = File.ReadAllLines(Path.Combine(outputDir, "_known_missing.txt"));
            lines.ShouldBe(["1.0.0", "2.0.0", "3.0.0"]); // deduplicated, sorted
        }
        finally
        {
            try { Directory.Delete(outputDir, true); } catch { }
        }
    }

    [Fact]
    public async Task RunAsync_RemoveKnownMissing_RemovesVersions()
    {
        var outputDir = Path.Combine(Path.GetTempPath(), $"km-remove-{Guid.NewGuid():N}");
        Directory.CreateDirectory(outputDir);

        try
        {
            File.WriteAllLines(Path.Combine(outputDir, "_known_missing.txt"), ["1.0.0", "2.0.0", "3.0.0"]);

            var runner = CreateRunnerWithOutputDir(outputDir);
            var result = await runner.RunAsync(["--remove-known-missing", "2.0.0"]);
            result.ShouldBe(0);

            var lines = File.ReadAllLines(Path.Combine(outputDir, "_known_missing.txt"));
            lines.ShouldBe(["1.0.0", "3.0.0"]);
        }
        finally
        {
            try { Directory.Delete(outputDir, true); } catch { }
        }
    }

    [Fact]
    public async Task RunAsync_RemoveKnownMissing_NoFile_Succeeds()
    {
        var outputDir = Path.Combine(Path.GetTempPath(), $"km-remove-nofile-{Guid.NewGuid():N}");

        try
        {
            var runner = CreateRunnerWithOutputDir(outputDir);
            var result = await runner.RunAsync(["--remove-known-missing", "1.0.0"]);
            result.ShouldBe(0);

            // File should be created (empty after removing non-existent version)
            var lines = File.ReadAllLines(Path.Combine(outputDir, "_known_missing.txt"));
            lines.ShouldBeEmpty();
        }
        finally
        {
            try { Directory.Delete(outputDir, true); } catch { }
        }
    }

    [Fact]
    public async Task RunAsync_ListKnownMissing_PrintsVersions()
    {
        var outputDir = Path.Combine(Path.GetTempPath(), $"km-list-{Guid.NewGuid():N}");
        Directory.CreateDirectory(outputDir);

        try
        {
            File.WriteAllLines(Path.Combine(outputDir, "_known_missing.txt"), ["1.0.0", "2.0.0"]);

            var runner = CreateRunnerWithOutputDir(outputDir);
            var result = await runner.RunAsync(["--list-known-missing"]);
            result.ShouldBe(0);
        }
        finally
        {
            try { Directory.Delete(outputDir, true); } catch { }
        }
    }

    [Fact]
    public async Task RunAsync_Missing_AutoSavesFailedAsKnownMissing()
    {
        var outputDir = Path.Combine(Path.GetTempPath(), $"km-autosave-{Guid.NewGuid():N}");
        Directory.CreateDirectory(outputDir);

        try
        {
            // Pipeline that fails for 2.0.0 but succeeds for 1.0.0
            var pipeline = new DesignPipeline()
                .Use(next => ctx =>
                {
                    if (ctx.Version == "2.0.0")
                        throw new InvalidOperationException("download failed");
                    return next(ctx);
                });

            var runner = new DesignPipelineRunner
            {
                VersionCollector = new StaticVersionCollector(["1.0.0", "2.0.0"]),
                Pipeline = pipeline.Build(),
                OutputDir = outputDir,
                OutputFilePattern = "mytool-{version}.json",
                RunProcess = _ => Task.FromResult(""),
                DefaultParallelism = 1,
            };

            var result = await runner.RunAsync(["--missing"]);
            result.ShouldBe(1); // 1 failure

            // 2.0.0 should have been auto-saved as known-missing
            var knownMissing = File.ReadAllLines(Path.Combine(outputDir, "_known_missing.txt"));
            knownMissing.ShouldContain("2.0.0");
            knownMissing.ShouldNotContain("1.0.0");
        }
        finally
        {
            try { Directory.Delete(outputDir, true); } catch { }
        }
    }

    [Fact]
    public async Task RunAsync_Missing_AutoSaveMergesWithExistingKnownMissing()
    {
        var outputDir = Path.Combine(Path.GetTempPath(), $"km-automerge-{Guid.NewGuid():N}");
        Directory.CreateDirectory(outputDir);

        try
        {
            // Pre-existing known-missing
            File.WriteAllLines(Path.Combine(outputDir, "_known_missing.txt"), ["0.9.0"]);

            var pipeline = new DesignPipeline()
                .Use(next => ctx => throw new InvalidOperationException("fail"));

            var runner = new DesignPipelineRunner
            {
                VersionCollector = new StaticVersionCollector(["1.0.0"]),
                Pipeline = pipeline.Build(),
                OutputDir = outputDir,
                OutputFilePattern = "mytool-{version}.json",
                RunProcess = _ => Task.FromResult(""),
                DefaultParallelism = 1,
            };

            await runner.RunAsync(["--missing"]);

            var knownMissing = File.ReadAllLines(Path.Combine(outputDir, "_known_missing.txt"));
            knownMissing.ShouldBe(["0.9.0", "1.0.0"]); // merged and sorted
        }
        finally
        {
            try { Directory.Delete(outputDir, true); } catch { }
        }
    }

    [Fact]
    public async Task RunAsync_NotMissing_DoesNotAutoSaveFailedVersions()
    {
        var outputDir = Path.Combine(Path.GetTempPath(), $"km-no-autosave-{Guid.NewGuid():N}");

        try
        {
            var pipeline = new DesignPipeline()
                .Use(next => ctx => throw new InvalidOperationException("fail"));

            var runner = new DesignPipelineRunner
            {
                VersionCollector = new StaticVersionCollector(["1.0.0"]),
                Pipeline = pipeline.Build(),
                OutputDir = outputDir,
                RunProcess = _ => Task.FromResult(""),
                DefaultParallelism = 1,
            };

            // Regular run (no --missing) — should NOT auto-save
            await runner.RunAsync([]);

            File.Exists(Path.Combine(outputDir, "_known_missing.txt")).ShouldBeFalse();
        }
        finally
        {
            try { Directory.Delete(outputDir, true); } catch { }
        }
    }

    [Fact]
    public async Task RunAsync_ListKnownMissing_NoFile_PrintsZero()
    {
        var outputDir = Path.Combine(Path.GetTempPath(), $"km-list-empty-{Guid.NewGuid():N}");

        try
        {
            var runner = CreateRunnerWithOutputDir(outputDir);
            var result = await runner.RunAsync(["--list-known-missing"]);
            result.ShouldBe(0);
        }
        finally
        {
            try { Directory.Delete(outputDir, true); } catch { }
        }
    }

    [Fact]
    public async Task RunAsync_Missing_ExcludesKnownMissing()
    {
        var outputDir = Path.Combine(Path.GetTempPath(), $"km-exclude-{Guid.NewGuid():N}");
        Directory.CreateDirectory(outputDir);

        try
        {
            // 1.0.0 already scraped, 2.0.0 is known-missing
            File.WriteAllText(Path.Combine(outputDir, "mytool-1.0.0.json"), "{}");
            File.WriteAllLines(Path.Combine(outputDir, "_known_missing.txt"), ["2.0.0"]);

            var processedVersions = new ConcurrentBag<string>();
            var pipeline = new DesignPipeline()
                .Use(next => async ctx =>
                {
                    processedVersions.Add(ctx.Version);
                    await next(ctx);
                });

            var runner = new DesignPipelineRunner
            {
                VersionCollector = new StaticVersionCollector(["1.0.0", "2.0.0", "3.0.0"]),
                Pipeline = pipeline.Build(),
                OutputDir = outputDir,
                OutputFilePattern = "mytool-{version}.json",
                RunProcess = _ => Task.FromResult(""),
                DefaultParallelism = 1,
            };

            var result = await runner.RunAsync(["--missing"]);
            result.ShouldBe(0);

            // Only 3.0.0 should be processed (1.0.0 scraped, 2.0.0 known-missing)
            processedVersions.ShouldBe(["3.0.0"]);
        }
        finally
        {
            try { Directory.Delete(outputDir, true); } catch { }
        }
    }

    [Fact]
    public async Task RunAsync_ListMissing_ExcludesKnownMissing()
    {
        var outputDir = Path.Combine(Path.GetTempPath(), $"km-list-exclude-{Guid.NewGuid():N}");
        Directory.CreateDirectory(outputDir);

        try
        {
            File.WriteAllText(Path.Combine(outputDir, "mytool-1.0.0.json"), "{}");
            File.WriteAllLines(Path.Combine(outputDir, "_known_missing.txt"), ["2.0.0"]);

            var runner = new DesignPipelineRunner
            {
                VersionCollector = new StaticVersionCollector(["1.0.0", "2.0.0", "3.0.0"]),
                Pipeline = new DesignPipeline().Build(),
                OutputDir = outputDir,
                OutputFilePattern = "mytool-{version}.json",
                RunProcess = _ => Task.FromResult(""),
                DefaultParallelism = 1,
            };

            // --list --missing should only show 3.0.0
            var result = await runner.RunAsync(["--list", "--missing"]);
            result.ShouldBe(0);
        }
        finally
        {
            try { Directory.Delete(outputDir, true); } catch { }
        }
    }

    // ── --missing tests ──────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_ListMissing_ShowsOnlyMissingVersions()
    {
        var outputDir = Path.Combine(Path.GetTempPath(), $"missing-list-{Guid.NewGuid():N}");
        Directory.CreateDirectory(outputDir);

        try
        {
            // Pre-create 2 existing scraped files
            File.WriteAllText(Path.Combine(outputDir, "mytool-1.0.0.json"), "{}");
            File.WriteAllText(Path.Combine(outputDir, "mytool-3.0.0.json"), "{}");

            var processedVersions = new ConcurrentBag<string>();
            var runner = new DesignPipelineRunner
            {
                VersionCollector = new StaticVersionCollector(["1.0.0", "2.0.0", "3.0.0"]),
                Pipeline = new DesignPipeline().Build(),
                OutputDir = outputDir,
                OutputFilePattern = "mytool-{version}.json",
                RunProcess = _ => Task.FromResult(""),
                DefaultParallelism = 1,
            };

            var result = await runner.RunAsync(["--list", "--missing"]);
            result.ShouldBe(0);
        }
        finally
        {
            try { Directory.Delete(outputDir, true); } catch { }
        }
    }

    [Fact]
    public async Task RunAsync_Missing_ScrapesOnlyMissingVersions()
    {
        var outputDir = Path.Combine(Path.GetTempPath(), $"missing-scrape-{Guid.NewGuid():N}");
        Directory.CreateDirectory(outputDir);

        try
        {
            File.WriteAllText(Path.Combine(outputDir, "mytool-1.0.0.json"), "{}");
            File.WriteAllText(Path.Combine(outputDir, "mytool-3.0.0.json"), "{}");

            var processedVersions = new ConcurrentBag<string>();
            var pipeline = new DesignPipeline()
                .Use(next => async ctx =>
                {
                    processedVersions.Add(ctx.Version);
                    await next(ctx);
                });

            var runner = new DesignPipelineRunner
            {
                VersionCollector = new StaticVersionCollector(["1.0.0", "2.0.0", "3.0.0"]),
                Pipeline = pipeline.Build(),
                OutputDir = outputDir,
                OutputFilePattern = "mytool-{version}.json",
                RunProcess = _ => Task.FromResult(""),
                DefaultParallelism = 1,
            };

            var result = await runner.RunAsync(["--missing"]);
            result.ShouldBe(0);

            processedVersions.ShouldBe(["2.0.0"]);
        }
        finally
        {
            try { Directory.Delete(outputDir, true); } catch { }
        }
    }

    [Fact]
    public async Task RunAsync_Missing_WithMinVersion_FiltersBoth()
    {
        var outputDir = Path.Combine(Path.GetTempPath(), $"missing-minver-{Guid.NewGuid():N}");
        Directory.CreateDirectory(outputDir);

        try
        {
            // 3.0.0 already scraped
            File.WriteAllText(Path.Combine(outputDir, "mytool-3.0.0.json"), "{}");

            var processedVersions = new ConcurrentBag<string>();
            var pipeline = new DesignPipeline()
                .Use(next => async ctx =>
                {
                    processedVersions.Add(ctx.Version);
                    await next(ctx);
                });

            var runner = new DesignPipelineRunner
            {
                VersionCollector = new StaticVersionCollector(["1.0.0", "2.0.0", "3.0.0"]),
                Pipeline = pipeline.Build(),
                OutputDir = outputDir,
                OutputFilePattern = "mytool-{version}.json",
                RunProcess = _ => Task.FromResult(""),
                DefaultParallelism = 1,
            };

            // min-version filters out 1.0.0, missing filters out 3.0.0 → only 2.0.0
            var result = await runner.RunAsync(["--missing", "--min-version", "2.0.0"]);
            result.ShouldBe(0);

            processedVersions.ShouldBe(["2.0.0"]);
        }
        finally
        {
            try { Directory.Delete(outputDir, true); } catch { }
        }
    }

    [Fact]
    public async Task RunAsync_Missing_EmptyOutputDir_AllAreMissing()
    {
        var outputDir = Path.Combine(Path.GetTempPath(), $"missing-empty-{Guid.NewGuid():N}");

        try
        {
            var processedVersions = new ConcurrentBag<string>();
            var pipeline = new DesignPipeline()
                .Use(next => async ctx =>
                {
                    processedVersions.Add(ctx.Version);
                    await next(ctx);
                });

            var runner = new DesignPipelineRunner
            {
                VersionCollector = new StaticVersionCollector(["1.0.0", "2.0.0"]),
                Pipeline = pipeline.Build(),
                OutputDir = outputDir,
                OutputFilePattern = "mytool-{version}.json",
                RunProcess = _ => Task.FromResult(""),
                DefaultParallelism = 1,
            };

            var result = await runner.RunAsync(["--missing"]);
            result.ShouldBe(0);
            processedVersions.Count.ShouldBe(2);
        }
        finally
        {
            try { Directory.Delete(outputDir, true); } catch { }
        }
    }

    [Fact]
    public async Task RunAsync_Missing_WithoutOutputFilePattern_Throws()
    {
        var runner = new DesignPipelineRunner
        {
            VersionCollector = new StaticVersionCollector(["1.0.0"]),
            Pipeline = new DesignPipeline().Build(),
            OutputDir = Path.GetTempPath(),
            RunProcess = _ => Task.FromResult(""),
            DefaultParallelism = 1,
            // OutputFilePattern intentionally omitted
        };

        await Should.ThrowAsync<InvalidOperationException>(() => runner.RunAsync(["--missing"]));
    }

    [Fact]
    public async Task RunAsync_Missing_AllAlreadyScraped_ReturnsZero()
    {
        var outputDir = Path.Combine(Path.GetTempPath(), $"missing-all-{Guid.NewGuid():N}");
        Directory.CreateDirectory(outputDir);

        try
        {
            File.WriteAllText(Path.Combine(outputDir, "mytool-1.0.0.json"), "{}");
            File.WriteAllText(Path.Combine(outputDir, "mytool-2.0.0.json"), "{}");

            var processedVersions = new ConcurrentBag<string>();
            var pipeline = new DesignPipeline()
                .Use(next => async ctx =>
                {
                    processedVersions.Add(ctx.Version);
                    await next(ctx);
                });

            var runner = new DesignPipelineRunner
            {
                VersionCollector = new StaticVersionCollector(["1.0.0", "2.0.0"]),
                Pipeline = pipeline.Build(),
                OutputDir = outputDir,
                OutputFilePattern = "mytool-{version}.json",
                RunProcess = _ => Task.FromResult(""),
                DefaultParallelism = 1,
            };

            var result = await runner.RunAsync(["--missing"]);
            result.ShouldBe(0);
            processedVersions.ShouldBeEmpty();
        }
        finally
        {
            try { Directory.Delete(outputDir, true); } catch { }
        }
    }

    [Fact]
    public async Task RunAsync_Missing_IgnoresUnrelatedJsonFiles()
    {
        var outputDir = Path.Combine(Path.GetTempPath(), $"missing-unrelated-{Guid.NewGuid():N}");
        Directory.CreateDirectory(outputDir);

        try
        {
            // This file matches the pattern
            File.WriteAllText(Path.Combine(outputDir, "mytool-1.0.0.json"), "{}");
            // This file does NOT match the pattern (different prefix)
            File.WriteAllText(Path.Combine(outputDir, "other-2.0.0.json"), "{}");

            var processedVersions = new ConcurrentBag<string>();
            var pipeline = new DesignPipeline()
                .Use(next => async ctx =>
                {
                    processedVersions.Add(ctx.Version);
                    await next(ctx);
                });

            var runner = new DesignPipelineRunner
            {
                VersionCollector = new StaticVersionCollector(["1.0.0", "2.0.0"]),
                Pipeline = pipeline.Build(),
                OutputDir = outputDir,
                OutputFilePattern = "mytool-{version}.json",
                RunProcess = _ => Task.FromResult(""),
                DefaultParallelism = 1,
            };

            var result = await runner.RunAsync(["--missing"]);
            result.ShouldBe(0);

            // 1.0.0 is on disk, 2.0.0 is NOT (other-2.0.0.json doesn't match)
            processedVersions.ShouldBe(["2.0.0"]);
        }
        finally
        {
            try { Directory.Delete(outputDir, true); } catch { }
        }
    }

    private static DesignPipelineRunner CreateRunner(string[] versions)
    {
        var pipeline = new DesignPipeline(); // empty pipeline = no-op
        return new DesignPipelineRunner
        {
            VersionCollector = new StaticVersionCollector(versions),
            Pipeline = pipeline.Build(),
            OutputDir = Path.GetTempPath(),
            RunProcess = _ => Task.FromResult(""),
            DefaultParallelism = 1,
        };
    }

    private static DesignPipelineRunner CreateRunnerWithOutputDir(string outputDir)
    {
        return new DesignPipelineRunner
        {
            VersionCollector = new StaticVersionCollector([]),
            Pipeline = new DesignPipeline().Build(),
            OutputDir = outputDir,
            RunProcess = _ => Task.FromResult(""),
            DefaultParallelism = 1,
        };
    }
}
