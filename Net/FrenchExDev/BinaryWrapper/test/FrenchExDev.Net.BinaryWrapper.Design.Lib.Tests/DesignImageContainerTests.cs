using System.Collections.Concurrent;
using FrenchExDev.Net.BinaryWrapper.Design;
using FrenchExDev.Net.BinaryWrapper.Design.Lib;
using Microsoft.Extensions.Logging;
using Shouldly;

namespace FrenchExDev.Net.BinaryWrapper.Design.Lib.Tests;

public sealed class ContainerFactAttribute : FactAttribute
{
    public ContainerFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("BINARYWRAPPER_CONTAINER_TESTS") != "1")
            Skip = "Set BINARYWRAPPER_CONTAINER_TESTS=1 to run against a local Podman engine.";
    }
}

public sealed class DesignImageContainerTests
{
    [ContainerFact]
    public async Task DefaultRunner_RecordsBothBuildStreams()
    {
        var name = "output-test-" + Guid.NewGuid().ToString("N");
        var output = Path.Combine(Path.GetTempPath(), name);
        var runner = new DesignPipelineRunner
        {
            VersionCollector = new StaticVersionCollector([]),
            Pipeline = _ => Task.CompletedTask,
            ImagePlan = new DesignImagePlan
            {
                ImageName = name, BaseImage = "alpine:3.19",
                BaseInstallScript = "echo stdout-build; echo stderr-build >&2",
                InstallScript = _ => ":",
            },
            OutputDir = output, MinLogLevel = LogLevel.None,
        };
        try
        {
            (await runner.RunAsync(["--build-base"])).ShouldBe(0);
            var log = await File.ReadAllTextAsync(Directory.GetFiles(output, "build.log", SearchOption.AllDirectories).Single());
            log.ShouldContain("stdout-build");
            log.ShouldContain("stderr-build");
            log.ShouldContain("Build completed");
        }
        finally
        {
            var cleanup = await runner.RunAsync(["--clean-images"]);
            if (Directory.Exists(output)) Directory.Delete(output, true);
            cleanup.ShouldBe(0);
        }
    }
    [ContainerFact]
    public async Task RealImages_BuildReuseScrapeAndClean()
    {
        var name = "cache-test-" + Guid.NewGuid().ToString("N");
        var output = Path.Combine(Path.GetTempPath(), name);
        var commands = new ConcurrentQueue<string[]>();
        async Task<string> Run(string[] args)
        {
            commands.Enqueue(args);
            return await ProcessRunnerContainerRuntime.RunProcessAsync(args);
        }
        var plan = new DesignImagePlan
        {
            ImageName = name, BaseImage = "alpine:3.19",
            BaseInstallScript = "echo prepared > /binarywrapper-base",
            InstallScript = version =>
                $"printf '%s\\n' '#!/bin/sh' 'echo Cache tool {version}' > /usr/local/bin/cache-tool && chmod +x /usr/local/bin/cache-tool",
        };
        var pipeline = new DesignPipeline().UseVersionImage().UseContainer()
            .Use(next => async ctx =>
            {
                (await ctx.RunProcess(["podman", "exec", ctx.ContainerId!, "cat", "/binarywrapper-base"])).Trim()
                    .ShouldBe("prepared");
                (await ctx.RunHelp!(["cache-tool", "--help"])).Trim().ShouldBe($"Cache tool {ctx.Version}");
                await next(ctx);
            })
            .UseScraper("cache-tool", (_, _) => new StandardHelpParser()).Build();
        var runner = new DesignPipelineRunner
        {
            VersionCollector = new StaticVersionCollector(["1.0", "2.0"]),
            Pipeline = pipeline, ImagePlan = plan, OutputDir = output,
            RunProcess = Run, MinLogLevel = LogLevel.Information,
        };
        try
        {
            (await runner.RunAsync(["--build-images", "--parallel", "2"])).ShouldBe(0);
            commands.Count(c => c[1] == "build").ShouldBe(3);
            commands.ShouldNotContain(c => c[1] == "run");
            (await runner.RunAsync(["--parallel", "2", "--keep-images"])).ShouldBe(0);
            commands.Count(c => c[1] == "build").ShouldBe(3);
            File.Exists(Path.Combine(output, "cache-tool-1.0.json")).ShouldBeTrue();
            File.Exists(Path.Combine(output, "cache-tool-2.0.json")).ShouldBeTrue();
            commands.ShouldNotContain(c => c[1] == "rmi");

            (await runner.RunAsync(["--parallel", "2"])).ShouldBe(0);
            commands.Count(c => c[1] == "build").ShouldBe(3);
            commands.Where(c => c[1] == "rmi").Count().ShouldBe(2);
            commands.Where(c => c[1] == "rmi").ShouldAllBe(c => c.Length == 3 && c[2].Contains(":version-"));
            var remaining = await Run(["podman", "image", "ls", "--filter",
                $"label=io.frenchexdev.binarywrapper.cache={name}", "--format", "{{.Repository}}:{{.Tag}}"]);
            var baseBuild = commands.First(c => c[1] == "build");
            remaining.Trim().ShouldBe(baseBuild[Array.IndexOf(baseBuild, "--tag") + 1]);
        }
        finally
        {
            var cleanup = await runner.RunAsync(["--clean-images"]);
            if (Directory.Exists(output)) Directory.Delete(output, true);
            cleanup.ShouldBe(0);
        }
    }
}

