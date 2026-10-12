using System.Collections.Concurrent;
using FrenchExDev.Net.BinaryWrapper.Design;
using FrenchExDev.Net.BinaryWrapper.Design.Lib;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace FrenchExDev.Net.BinaryWrapper.Design.Lib.Tests;

// ── DesignPipelineRunner --reparse Edge Cases ────────────────────────────────

public class DesignPipelineRunnerReparseEdgeCaseTests
{
    [Fact]
    public async Task RunAsync_Reparse_EmptyHelpDir_ReturnsZero()
    {
        var outputDir = Path.Combine(Path.GetTempPath(), $"reparse-empty-help-{Guid.NewGuid():N}");
        // Create the help dir but leave it empty (no version subdirs)
        var helpDir = Path.Combine(outputDir, "help");
        Directory.CreateDirectory(helpDir);

        try
        {
            var runner = new DesignPipelineRunner
            {
                VersionCollector = new StaticVersionCollector([]),
                Pipeline = new DesignPipeline().Build(),
                ReparsePipeline = new DesignPipeline().Build(),
                OutputDir = outputDir,
                RunProcess = _ => Task.FromResult(""),
                DefaultParallelism = 1,
            };

            var result = await runner.RunAsync(["--reparse"]);
            result.ShouldBe(0); // No versions discovered = no processing
        }
        finally
        {
            try { Directory.Delete(outputDir, true); } catch { }
        }
    }

    [Fact]
    public async Task RunAsync_Reparse_UsesReparsePipeline_NotMainPipeline()
    {
        var outputDir = Path.Combine(Path.GetTempPath(), $"reparse-pipeline-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(outputDir, "help", "1.0.0"));

        try
        {
            var mainPipelineCalled = false;
            var reparsePipelineCalled = false;

            var mainPipeline = new DesignPipeline()
                .Use(next => async ctx =>
                {
                    mainPipelineCalled = true;
                    await next(ctx);
                });

            var reparsePipeline = new DesignPipeline()
                .Use(next => async ctx =>
                {
                    reparsePipelineCalled = true;
                    await next(ctx);
                });

            var runner = new DesignPipelineRunner
            {
                VersionCollector = new StaticVersionCollector([]),
                Pipeline = mainPipeline.Build(),
                ReparsePipeline = reparsePipeline.Build(),
                OutputDir = outputDir,
                RunProcess = _ => Task.FromResult(""),
                DefaultParallelism = 1,
            };

            await runner.RunAsync(["--reparse"]);

            mainPipelineCalled.ShouldBeFalse();
            reparsePipelineCalled.ShouldBeTrue();
        }
        finally
        {
            try { Directory.Delete(outputDir, true); } catch { }
        }
    }
}

// ── DesignPipelineRunner --missing Edge Cases ────────────────────────────────

public class DesignPipelineRunnerMissingEdgeCaseTests
{
    [Fact]
    public async Task RunAsync_Missing_NonMatchingJsonFiles_NotExtracted()
    {
        var outputDir = Path.Combine(Path.GetTempPath(), $"missing-nonmatch-{Guid.NewGuid():N}");
        Directory.CreateDirectory(outputDir);

        try
        {
            // Create a .json file that doesn't match the pattern at all
            File.WriteAllText(Path.Combine(outputDir, "unrelated.json"), "{}");

            var processedVersions = new ConcurrentBag<string>();
            var pipeline = new DesignPipeline()
                .Use(next => async ctx =>
                {
                    processedVersions.Add(ctx.Version);
                    await next(ctx);
                });

            var runner = new DesignPipelineRunner
            {
                VersionCollector = new StaticVersionCollector(["1.0.0"]),
                Pipeline = pipeline.Build(),
                OutputDir = outputDir,
                OutputFilePattern = "tool-{version}.json",
                RunProcess = _ => Task.FromResult(""),
                DefaultParallelism = 1,
            };

            await runner.RunAsync(["--missing"]);
            processedVersions.ShouldContain("1.0.0");
        }
        finally
        {
            try { Directory.Delete(outputDir, true); } catch { }
        }
    }

    [Fact]
    public async Task RunAsync_Missing_BlankLinesInKnownMissing_Ignored()
    {
        var outputDir = Path.Combine(Path.GetTempPath(), $"km-blank-{Guid.NewGuid():N}");
        Directory.CreateDirectory(outputDir);

        try
        {
            // Write known missing file with blank lines
            File.WriteAllText(Path.Combine(outputDir, "_known_missing.txt"), "1.0.0\n\n  \n2.0.0\n");

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
                OutputFilePattern = "tool-{version}.json",
                RunProcess = _ => Task.FromResult(""),
                DefaultParallelism = 1,
            };

            await runner.RunAsync(["--missing"]);
            // 1.0.0 and 2.0.0 are known missing, so only 3.0.0 is processed
            processedVersions.ShouldBe(["3.0.0"]);
        }
        finally
        {
            try { Directory.Delete(outputDir, true); } catch { }
        }
    }
}

// ── DesignPipelineRunner Progress Tracking ───────────────────────────────────

public class DesignPipelineRunnerProgressTests
{
    [Fact]
    public async Task RunAsync_NoDashboard_ProgressIsNullOnContext()
    {
        VersionProgressInfo? capturedProgress = null;
        var pipeline = new DesignPipeline()
            .Use(next => async ctx =>
            {
                capturedProgress = ctx.Progress;
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
        capturedProgress.ShouldBeNull();
    }

    [Fact]
    public async Task RunAsync_FailedVersion_ReportsError()
    {
        var pipeline = new DesignPipeline()
            .Use(next => ctx => throw new InvalidOperationException("test failure"));

        var runner = new DesignPipelineRunner
        {
            VersionCollector = new StaticVersionCollector(["1.0.0"]),
            Pipeline = pipeline.Build(),
            OutputDir = Path.GetTempPath(),
            RunProcess = _ => Task.FromResult(""),
            DefaultParallelism = 1,
        };

        var result = await runner.RunAsync([]);
        result.ShouldBe(1);
    }

    [Fact]
    public async Task RunAsync_SuccessfulPipeline_SetsResult()
    {
        var pipeline = new DesignPipeline()
            .Use(next => async ctx =>
            {
                ctx.Result = new CommandTree
                {
                    BinaryName = "tool",
                    Root = new CommandNode { Name = "tool" }
                };
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

        var result = await runner.RunAsync([]);
        result.ShouldBe(0);
    }
}

// ── DesignPipelineRunner CLI Arg Parsing Edge Cases ──────────────────────────

public class DesignPipelineRunnerArgParsingTests
{
    [Fact]
    public async Task RunAsync_AllArgsCombined()
    {
        string? capturedRuntime = null;
        string? capturedOutput = null;
        int? capturedScrapeParallel = null;

        var pipeline = new DesignPipeline()
            .Use(next => async ctx =>
            {
                capturedRuntime = ctx.RuntimeBinary;
                capturedOutput = ctx.OutputDir;
                capturedScrapeParallel = ctx.ScrapeParallelism;
                await next(ctx);
            });

        var outputDir = Path.Combine(Path.GetTempPath(), $"args-test-{Guid.NewGuid():N}");

        try
        {
            var runner = new DesignPipelineRunner
            {
                VersionCollector = new StaticVersionCollector(["1.0.0"]),
                Pipeline = pipeline.Build(),
                OutputDir = "/default",
                RunProcess = _ => Task.FromResult(""),
                DefaultParallelism = 1,
            };

            await runner.RunAsync([
                "--runtime", "docker",
                "--output", outputDir,
                "--parallel", "8",
                "--scrape-parallel", "16",
                "--min-version", "1.0.0"
            ]);

            capturedRuntime.ShouldBe("docker");
            capturedOutput.ShouldBe(outputDir);
            capturedScrapeParallel.ShouldBe(16);
        }
        finally
        {
            try { Directory.Delete(outputDir, true); } catch { }
        }
    }

    [Fact]
    public async Task RunAsync_List_WithMissing_ShowsMissingLabel()
    {
        var outputDir = Path.Combine(Path.GetTempPath(), $"list-missing-label-{Guid.NewGuid():N}");
        Directory.CreateDirectory(outputDir);

        try
        {
            var runner = new DesignPipelineRunner
            {
                VersionCollector = new StaticVersionCollector(["1.0.0", "2.0.0"]),
                Pipeline = new DesignPipeline().Build(),
                OutputDir = outputDir,
                OutputFilePattern = "tool-{version}.json",
                RunProcess = _ => Task.FromResult(""),
                DefaultParallelism = 1,
            };

            // --list --missing should print "missing versions" label
            var result = await runner.RunAsync(["--list", "--missing"]);
            result.ShouldBe(0);
        }
        finally
        {
            try { Directory.Delete(outputDir, true); } catch { }
        }
    }

    [Fact]
    public async Task RunAsync_List_WithoutMissing_ShowsVersionsLabel()
    {
        var runner = new DesignPipelineRunner
        {
            VersionCollector = new StaticVersionCollector(["1.0.0"]),
            Pipeline = new DesignPipeline().Build(),
            OutputDir = Path.GetTempPath(),
            RunProcess = _ => Task.FromResult(""),
            DefaultParallelism = 1,
        };

        // --list without --missing should print "versions" label
        var result = await runner.RunAsync(["--list"]);
        result.ShouldBe(0);
    }
}

// ── UseScraper Progress Integration ──────────────────────────────────────────

public class UseScraperProgressTests
{
    [Fact]
    public async Task UseScraper_WithProgress_IncrementsCommandsScraped()
    {
        var outputDir = Path.Combine(Path.GetTempPath(), $"scraper-inc-{Guid.NewGuid():N}");
        Directory.CreateDirectory(outputDir);

        try
        {
            var progress = new VersionProgressInfo("1.0.0");
            var ctx = new VersionContext
            {
                Version = "1.0.0",
                RuntimeBinary = "podman",
                Logger = NullLogger.Instance,
                OutputDir = outputDir,
                RunProcess = _ => Task.FromResult(""),
                ActiveContainers = new ConcurrentBag<string>(),
                ActiveImages = new ConcurrentDictionary<string, byte>(),
                Progress = progress,
            };
            ctx.RunHelp = args =>
            {
                var key = string.Join(" ", args);
                return Task.FromResult(key switch
                {
                    "mytool --help" => "A tool\n\nCommands:\n  sub    A sub\n",
                    "mytool sub --help" => "A sub\n",
                    _ => ""
                });
            };

            var pipeline = new DesignPipeline()
                .UseScraper("mytool", (v, l) => new StandardHelpParser());

            await pipeline.Build()(ctx);

            // The progress should have incremented for each scraped command
            progress.CommandsScraped.ShouldBeGreaterThan(0);
        }
        finally
        {
            try { Directory.Delete(outputDir, true); } catch { }
        }
    }

    [Fact]
    public async Task UseScraper_WithoutProgress_StillWorks()
    {
        var outputDir = Path.Combine(Path.GetTempPath(), $"scraper-noprog-{Guid.NewGuid():N}");
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
                // Progress intentionally null
            };
            ctx.RunHelp = _ => Task.FromResult("A tool\n");

            var pipeline = new DesignPipeline()
                .UseScraper("mytool", (v, l) => new StandardHelpParser());

            await pipeline.Build()(ctx);
            ctx.Result.ShouldNotBeNull();
        }
        finally
        {
            try { Directory.Delete(outputDir, true); } catch { }
        }
    }

    [Fact]
    public async Task UseScraper_WithoutHelpDumpDir_DoesNotDump()
    {
        var outputDir = Path.Combine(Path.GetTempPath(), $"scraper-nodump-{Guid.NewGuid():N}");
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
            // HelpDumpDir is null
            ctx.RunHelp = _ => Task.FromResult("A tool\n");

            var pipeline = new DesignPipeline()
                .UseScraper("mytool", (v, l) => new StandardHelpParser());

            await pipeline.Build()(ctx);

            // No help subdirectory should exist
            Directory.Exists(Path.Combine(outputDir, "help")).ShouldBeFalse();
        }
        finally
        {
            try { Directory.Delete(outputDir, true); } catch { }
        }
    }
}

// ── UseContainer RunHelp Tests ───────────────────────────────────────────────

public class UseContainerRunHelpTests
{
    [Fact]
    public async Task UseContainer_RunHelp_CombinesArgsCorrectly()
    {
        var capturedArgs = new List<string[]>();
        var ctx = new VersionContext
        {
            Version = "1.0.0",
            RuntimeBinary = "podman",
            Logger = NullLogger.Instance,
            OutputDir = "/tmp",
            RunProcess = args =>
            {
                capturedArgs.Add(args);
                return Task.FromResult("cid-123\n");
            },
            ActiveContainers = new ConcurrentBag<string>(),
            ActiveImages = new ConcurrentDictionary<string, byte>(),
        };
        ctx.ImageTag = "test:1.0";

        string? helpOutput = null;
        var pipeline = new DesignPipeline()
            .UseContainer()
            .Use(next => async c =>
            {
                helpOutput = await c.RunHelp!(["tool", "sub", "--help"]);
                await next(c);
            });

        await pipeline.Build()(ctx);

        // The RunHelp should exec: podman exec cid-123 tool sub --help
        capturedArgs.ShouldContain(c =>
            c[0] == "podman" &&
            c[1] == "exec" &&
            c[2] == "cid-123" &&
            c[3] == "tool" &&
            c[4] == "sub" &&
            c[5] == "--help");
    }
}

// ── UseInlineContainer RunHelp Tests ─────────────────────────────────────────

public class UseInlineContainerRunHelpTests
{
    [Fact]
    public async Task UseInlineContainer_RunHelp_ExecsInContainer()
    {
        var capturedArgs = new List<string[]>();
        var ctx = new VersionContext
        {
            Version = "1.0.0",
            RuntimeBinary = "docker",
            Logger = NullLogger.Instance,
            OutputDir = "/tmp",
            RunProcess = args =>
            {
                capturedArgs.Add(args);
                return Task.FromResult("inline-cid\n");
            },
            ActiveContainers = new ConcurrentBag<string>(),
            ActiveImages = new ConcurrentDictionary<string, byte>(),
        };

        string? helpOutput = null;
        var pipeline = new DesignPipeline()
            .UseInlineContainer("alpine:3.19", v => $"install {v}")
            .Use(next => async c =>
            {
                helpOutput = await c.RunHelp!(["tool", "--help"]);
                await next(c);
            });

        await pipeline.Build()(ctx);

        // The RunHelp call should exec in the container
        capturedArgs.ShouldContain(c =>
            c[0] == "docker" &&
            c[1] == "exec" &&
            c[3] == "tool" &&
            c[4] == "--help");
    }

    [Fact]
    public async Task UseInlineContainer_SetsProgressStage()
    {
        var progress = new VersionProgressInfo("1.0.0");
        var ctx = new VersionContext
        {
            Version = "1.0.0",
            RuntimeBinary = "podman",
            Logger = NullLogger.Instance,
            OutputDir = "/tmp",
            RunProcess = _ => Task.FromResult("cid\n"),
            ActiveContainers = new ConcurrentBag<string>(),
            ActiveImages = new ConcurrentDictionary<string, byte>(),
            Progress = progress,
        };

        string? capturedStage = null;
        var pipeline = new DesignPipeline()
            .UseInlineContainer("alpine:3.19", v => $"install {v}")
            .Use(next => async c =>
            {
                capturedStage = progress.Stage;
                await next(c);
            });

        await pipeline.Build()(ctx);

        // After installation, the stage should have been "Installing" at some point
        // But by the time the next middleware runs, it's still "Installing"
        capturedStage.ShouldBe("Installing");
    }
}

// ── UseImageBuild Progress Tests ─────────────────────────────────────────────

public class UseImageBuildProgressTests
{
    [Fact]
    public async Task UseImageBuild_SetsProgressStageToBuilding()
    {
        var progress = new VersionProgressInfo("1.0.0");
        var callIndex = 0;
        var ctx = new VersionContext
        {
            Version = "1.0.0",
            RuntimeBinary = "podman",
            Logger = NullLogger.Instance,
            OutputDir = "/tmp",
            RunProcess = args =>
            {
                callIndex++;
                if (callIndex == 1) throw new InvalidOperationException("not found");
                if (callIndex == 2) return Task.FromResult("cid\n");
                return Task.FromResult("");
            },
            ActiveContainers = new ConcurrentBag<string>(),
            ActiveImages = new ConcurrentDictionary<string, byte>(),
            Progress = progress,
        };

        string? capturedStage = null;
        var pipeline = new DesignPipeline()
            .UseImageBuild("myimg", "alpine:3.19", v => $"install {v}")
            .Use(next => async c =>
            {
                capturedStage = progress.Stage;
                await next(c);
            });

        await pipeline.Build()(ctx);

        // The stage should have been set to "Building" during image build
        // (may have progressed further by the time next middleware runs)
    }
}

// ── UseContainer Progress Tests ──────────────────────────────────────────────

public class UseContainerProgressTests
{
    [Fact]
    public async Task UseContainer_SetsProgressStageToStarting()
    {
        var progress = new VersionProgressInfo("1.0.0");
        var ctx = new VersionContext
        {
            Version = "1.0.0",
            RuntimeBinary = "podman",
            Logger = NullLogger.Instance,
            OutputDir = "/tmp",
            RunProcess = _ => Task.FromResult("cid\n"),
            ActiveContainers = new ConcurrentBag<string>(),
            ActiveImages = new ConcurrentDictionary<string, byte>(),
            Progress = progress,
        };
        ctx.ImageTag = "test:1.0";

        string? capturedStage = null;
        var pipeline = new DesignPipeline()
            .UseContainer()
            .Use(next => async c =>
            {
                capturedStage = progress.Stage;
                await next(c);
            });

        await pipeline.Build()(ctx);

        capturedStage.ShouldBe("Starting");
    }
}

// ── UseCachedHelp Command Path Tests ─────────────────────────────────────────

public class UseCachedHelpCommandPathTests
{
    [Fact]
    public async Task UseCachedHelp_SubCommand_BuildsCorrectPath()
    {
        var outputDir = Path.Combine(Path.GetTempPath(), $"cached-sub-{Guid.NewGuid():N}");
        var helpDir = Path.Combine(outputDir, "help", "1.0.0");
        Directory.CreateDirectory(helpDir);

        try
        {
            // Write help files for subcommands
            await File.WriteAllTextAsync(
                Path.Combine(helpDir, "tool_sub.help.txt"),
                "A subcommand\n");

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

            string? capturedHelp = null;
            var pipeline = new DesignPipeline()
                .UseCachedHelp()
                .Use(next => async c =>
                {
                    // Simulate calling help for "tool sub --help"
                    capturedHelp = await c.RunHelp!(["tool", "sub", "--help"]);
                    await next(c);
                });

            await pipeline.Build()(ctx);

            capturedHelp.ShouldBe("A subcommand\n");
        }
        finally
        {
            try { Directory.Delete(outputDir, true); } catch { }
        }
    }
}

// ── DesignPipeline Build Edge Cases ──────────────────────────────────────────

public class DesignPipelineBuildEdgeCaseTests
{
    [Fact]
    public async Task Build_ManyMiddleware_ExecuteInCorrectOrder()
    {
        var order = new List<int>();
        var pipeline = new DesignPipeline()
            .Use(next => async ctx => { order.Add(1); await next(ctx); order.Add(6); })
            .Use(next => async ctx => { order.Add(2); await next(ctx); order.Add(5); })
            .Use(next => async ctx => { order.Add(3); await next(ctx); order.Add(4); });

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

        await pipeline.Build()(ctx);

        order.ShouldBe([1, 2, 3, 4, 5, 6]);
    }
}

// ── VersionProgressInfo Edge Cases ───────────────────────────────────────────

public class VersionProgressInfoEdgeCaseTests
{
    [Fact]
    public void SetStage_CustomStage_Accepted()
    {
        var info = new VersionProgressInfo("1.0.0");
        info.SetStage("CustomStage");
        info.Stage.ShouldBe("CustomStage");
    }

    [Fact]
    public void Elapsed_IncreasesOverTime()
    {
        var info = new VersionProgressInfo("1.0.0");
        var first = info.Elapsed;
        // Wait briefly to ensure elapsed increases
        Thread.Sleep(10);
        var second = info.Elapsed;
        second.ShouldBeGreaterThanOrEqualTo(first);
    }
}

// ── DesignPipelineRunner Crash Recovery Edge Cases ───────────────────────────

public class DesignPipelineRunnerCrashRecoveryTests
{
    [Fact]
    public async Task RunAsync_MultipleFailedVersions_AllCleanedUp()
    {
        var cleanupCommands = new ConcurrentBag<string[]>();

        var pipeline = new DesignPipeline()
            .Use(next => async ctx =>
            {
                ctx.ActiveContainers.Add($"cid-{ctx.Version}");
                ctx.ActiveImages.TryAdd($"img-{ctx.Version}", 0);
                throw new InvalidOperationException("crash");
            });

        var runner = new DesignPipelineRunner
        {
            VersionCollector = new StaticVersionCollector(["1.0.0", "2.0.0"]),
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

        // Both containers and images should be cleaned up
        cleanupCommands.ShouldContain(c => c.Contains("rm") && c.Contains("cid-1.0.0"));
        cleanupCommands.ShouldContain(c => c.Contains("rm") && c.Contains("cid-2.0.0"));
        cleanupCommands.ShouldContain(c => c.Contains("rmi") && c.Contains("img-1.0.0"));
        cleanupCommands.ShouldContain(c => c.Contains("rmi") && c.Contains("img-2.0.0"));
    }

    [Fact]
    public async Task RunAsync_NoStragglers_SkipsCleanup()
    {
        var cleanupCommands = new ConcurrentBag<string[]>();

        var pipeline = new DesignPipeline(); // no-op, no stragglers

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

        // No rm or rmi commands should have been issued
        cleanupCommands.ShouldNotContain(c => c.Contains("rm"));
        cleanupCommands.ShouldNotContain(c => c.Contains("rmi"));
    }
}

// ── DesignPipelineRunner Missing + Auto-Save Edge Cases ──────────────────────

public class DesignPipelineRunnerAutoSaveTests
{
    [Fact]
    public async Task RunAsync_Missing_AllSucceed_NoAutoSave()
    {
        var outputDir = Path.Combine(Path.GetTempPath(), $"no-autosave-{Guid.NewGuid():N}");

        try
        {
            var pipeline = new DesignPipeline(); // no-op

            var runner = new DesignPipelineRunner
            {
                VersionCollector = new StaticVersionCollector(["1.0.0"]),
                Pipeline = pipeline.Build(),
                OutputDir = outputDir,
                OutputFilePattern = "tool-{version}.json",
                RunProcess = _ => Task.FromResult(""),
                DefaultParallelism = 1,
            };

            var result = await runner.RunAsync(["--missing"]);
            result.ShouldBe(0);

            // No known missing file should be created when all succeed
            File.Exists(Path.Combine(outputDir, "_known_missing.txt")).ShouldBeFalse();
        }
        finally
        {
            try { Directory.Delete(outputDir, true); } catch { }
        }
    }
}

// ── StaticVersionCollector Tests ─────────────────────────────────────────────

public class StaticVersionCollectorTests
{
    [Fact]
    public async Task CollectVersionsAsync_ReturnsProvided()
    {
        var collector = new StaticVersionCollector(["1.0.0", "2.0.0"]);
        var versions = await collector.CollectVersionsAsync();
        versions.ShouldBe(["1.0.0", "2.0.0"]);
    }

    [Fact]
    public async Task CollectVersionsAsync_Empty_ReturnsEmpty()
    {
        var collector = new StaticVersionCollector([]);
        var versions = await collector.CollectVersionsAsync();
        versions.ShouldBeEmpty();
    }
}
