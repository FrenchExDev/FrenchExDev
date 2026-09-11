using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FrenchExDev.Net.BinaryWrapper.Design.Lib;
using Microsoft.Extensions.Logging;
using Shouldly;

namespace FrenchExDev.Net.BinaryWrapper.Design.Lib.Tests;

public sealed partial class DesignImagePlanTests : IDisposable
{
    private readonly string _imageName = "test-tool-" + Guid.NewGuid().ToString("N");
    private readonly string _output = Path.Combine(Path.GetTempPath(), "bw-images-" + Guid.NewGuid().ToString("N"));

    private DesignImagePlan Plan(string setup = "echo dependencies", string install = "echo install",
        string platform = "linux/amd64", string baseImage = "alpine:3.19", string shell = "sh") => new()
    {
        ImageName = _imageName,
        BaseImage = baseImage,
        Shell = shell,
        BaseInstallScript = setup,
        InstallScript = version => $"{install} {version}",
        Platform = platform,
    };

    private DesignPipelineRunner Runner(FakeEngine engine, DesignImagePlan? plan = null,
        VersionDelegate? pipeline = null, string[]? versions = null, string runtime = "podman") => new()
    {
        VersionCollector = new StaticVersionCollector(versions ?? ["1.0", "2.0"]),
        Pipeline = pipeline ?? new DesignPipeline().UseVersionImage().Build(),
        ReparsePipeline = _ => Task.CompletedTask,
        ImagePlan = plan ?? Plan(),
        RunProcess = engine.Run,
        RuntimeBinary = runtime,
        OutputDir = _output,
        MinLogLevel = LogLevel.None,
    };

    [Fact]
    public async Task BaseBeforeParallelVersions_ThenReuseAcrossRuns()
    {
        var engine = new FakeEngine();
        var pipeline = new DesignPipeline().UseVersionImage().Use(next => async ctx =>
        {
            ctx.ImageTag.ShouldNotBeNull();
            ctx.ActiveImages.ShouldBeEmpty(); // Persistent cache must not enter crash cleanup.
            await next(ctx);
        }).Build();
        var runner = Runner(engine, pipeline: pipeline);
        (await runner.RunAsync(["--parallel", "2", "--keep-images"])).ShouldBe(0);
        engine.Builds.Count.ShouldBe(3);
        engine.Builds.First().Stage.ShouldBe("base");
        engine.Builds.Skip(1).ShouldAllBe(b => b.Stage == "version");
        var baseTag = engine.Builds.First().Tag;
        engine.Builds.Skip(1).ShouldAllBe(b => b.Dockerfile.StartsWith("FROM " + baseTag + "\n"));
        engine.Commands.ShouldNotContain(c => c[1] == "rmi" || c[1] == "commit");
        engine.Commands.Where(c => c[1] == "build").ShouldAllBe(c => c.Contains("--layers") && c.Contains("--pull=never"));
        Directory.GetFiles(_output, "Dockerfile", SearchOption.AllDirectories).Length.ShouldBe(3);
        Directory.GetFiles(_output, ".dockerignore", SearchOption.AllDirectories)
            .ShouldAllBe(path => File.ReadAllText(path) == "*\n!Dockerfile\n");

        (await runner.RunAsync(["--keep-images"])).ShouldBe(0);
        engine.Builds.Count.ShouldBe(3);
    }

    [Theory]
    [InlineData("setup", 3)]
    [InlineData("install", 2)]
    [InlineData("parent", 3)]
    [InlineData("platform", 3)]
    public async Task RecipeChanges_InvalidateOnlyAffectedImages(string change, int extraBuilds)
    {
        var engine = new FakeEngine();
        (await Runner(engine).RunAsync(["--build-images"])).ShouldBe(0);
        if (change == "parent") engine.SetBase("changed-parent", "linux/amd64");
        var plan = Plan(
            setup: change == "setup" ? "echo changed-dependencies" : "echo dependencies",
            install: change == "install" ? "echo changed-install" : "echo install",
            platform: change == "platform" ? "linux/arm64" : "linux/amd64");
        (await Runner(engine, plan).RunAsync(["--build-images"])).ShouldBe(0);
        engine.Builds.Count.ShouldBe(3 + extraBuilds);
    }

    [Fact]
    public async Task BuildOnly_DoesNotRunPipeline_AndDuplicatesBuildOnce()
    {
        var engine = new FakeEngine();
        var runner = Runner(engine, pipeline: _ => throw new InvalidOperationException("Must not scrape"),
            versions: ["1.0", "1.0", "2.0"]);
        (await runner.RunAsync(["--build-images"])).ShouldBe(0);
        engine.Builds.Count.ShouldBe(3);
        engine.Commands.ShouldNotContain(c => c[1] == "run" || c[1] == "exec" || c[1] == "rmi");
        Directory.GetFiles(_output, "*.json").ShouldBeEmpty();
    }

    [Fact]
    public async Task BuildBase_AndCleanup_DoNotCollectVersions()
    {
        var engine = new FakeEngine();
        var runner = new DesignPipelineRunner
        {
            VersionCollector = new ThrowingCollector(),
            Pipeline = _ => throw new InvalidOperationException("Must not scrape"),
            ImagePlan = Plan(), RunProcess = engine.Run, OutputDir = _output, MinLogLevel = LogLevel.None,
        };
        (await runner.RunAsync(["--build-base"])).ShouldBe(0);
        engine.Builds.Count.ShouldBe(1);
        (await runner.RunAsync(["--clean-images"])).ShouldBe(0);
        engine.Images.Keys.ShouldBe(new[] { "alpine:3.19" });
    }

    [Fact]
    public async Task ListEmptyAndReparse_DoNotTouchEngine()
    {
        var engine = new FakeEngine();
        (await Runner(engine).RunAsync(["--list"])).ShouldBe(0);
        (await Runner(engine, versions: []).RunAsync([])).ShouldBe(0);
        Directory.CreateDirectory(Path.Combine(_output, "help", "1.0"));
        (await Runner(engine).RunAsync(["--reparse"])).ShouldBe(0);
        engine.Commands.ShouldBeEmpty();
    }

    [Fact]
    public async Task Clean_RemovesOnlyOwnedTags_ChildrenFirst_WithoutForce()
    {
        var engine = new FakeEngine();
        var runner = Runner(engine);
        (await runner.RunAsync(["--build-images"])).ShouldBe(0);
        engine.ExtraListedTag = "localhost/binarywrapper/another:version-" + new string('a', 64);
        (await runner.RunAsync(["--clean-images"])).ShouldBe(0);
        var removals = engine.Commands.Where(c => c[1] == "rmi").ToArray();
        removals.Length.ShouldBe(3);
        removals.Take(2).ShouldAllBe(c => c[2].Contains(":version-"));
        removals.Last()[2].ShouldContain(":base-");
        removals.ShouldAllBe(c => c.Length == 3);
        engine.Images.Keys.ShouldBe(new[] { "alpine:3.19" });
    }

    [Fact]
    public async Task FailedBase_StopsBeforeAnyVersion_AndCanRetry()
    {
        var engine = new FakeEngine { FailStage = "base" };
        (await Runner(engine).RunAsync([])).ShouldBe(1);
        engine.Builds.ShouldBeEmpty();
        engine.FailStage = null;
        (await Runner(engine).RunAsync([])).ShouldBe(0);
        engine.Builds.Count.ShouldBe(3);
    }

    [Fact]
    public async Task FailedVersion_IsNotCached_AndCanRetry()
    {
        var engine = new FakeEngine { FailStage = "version" };
        (await Runner(engine, versions: ["1.0"]).RunAsync(["--build-images"])).ShouldBe(1);
        engine.Builds.Count.ShouldBe(1);
        engine.FailStage = null;
        (await Runner(engine, versions: ["1.0"]).RunAsync(["--build-images"])).ShouldBe(0);
        engine.Builds.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Docker_UsesPortableBuildArguments()
    {
        var engine = new FakeEngine();
        (await Runner(engine, runtime: "docker").RunAsync(["--build-base"])).ShouldBe(0);
        var build = engine.Commands.Single(c => c[1] == "build");
        build.ShouldContain("--pull=false");
        build.ShouldNotContain("--layers");
    }

    [Theory]
    [InlineData("--reparse", "--build-images")]
    [InlineData("--build-base", "--clean-images")]
    [InlineData("--list", "--clean-images")]
    public async Task ConflictingModes_FailBeforeTouchingEngine(string first, string second)
    {
        var engine = new FakeEngine();
        await Should.ThrowAsync<ArgumentException>(() => Runner(engine).RunAsync([first, second]));
        engine.Commands.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Scrape_RemovesOnlyItsVersion_AfterContainerCleanup(bool failScrape)
    {
        var engine = new FakeEngine();
        engine.Images["unrelated:version"] = new FakeEngine.Image(new string('f', 64), "linux/amd64", "version");
        var pipeline = new DesignPipeline().UseVersionImage().UseContainer().Use(next => async ctx =>
        {
            engine.Images.ContainsKey(ctx.ImageTag!).ShouldBeTrue();
            engine.Containers.ContainsKey(ctx.ContainerId!).ShouldBeTrue();
            await File.WriteAllTextAsync(Path.Combine(ctx.OutputDir, "collected-help.txt"), "captured help");
            if (failScrape) throw new InvalidOperationException("Scrape failed");
            await next(ctx);
        }).Build();

        (await Runner(engine, pipeline: pipeline, versions: ["1.0"]).RunAsync([])).ShouldBe(failScrape ? 1 : 0);

        var removal = engine.Commands.Single(c => c[1] == "rmi");
        removal.ShouldBe(new[] { "podman", "rmi", engine.Builds.Single(b => b.Stage == "version").Tag });
        var commands = engine.Commands.ToArray();
        Array.FindIndex(commands, c => c[1] == "rm").ShouldBeLessThan(Array.IndexOf(commands, removal));
        engine.Containers.ShouldBeEmpty();
        engine.Images.ContainsKey("unrelated:version").ShouldBeTrue();
        engine.Images.Values.Count(i => i.Stage == "base").ShouldBe(1);
        (await File.ReadAllTextAsync(Path.Combine(_output, "collected-help.txt"))).ShouldBe("captured help");
    }

    [Fact]
    public async Task ContainerStartFailure_StillRemovesVersionImage()
    {
        var engine = new FakeEngine { FailStart = true };
        var pipeline = new DesignPipeline().UseVersionImage().UseContainer().Build();
        (await Runner(engine, pipeline: pipeline, versions: ["1.0"]).RunAsync([])).ShouldBe(1);
        engine.Images.Values.ShouldNotContain(i => i.Stage == "version");
        engine.Commands.Single(c => c[1] == "rmi").Length.ShouldBe(3);
    }

    [Fact]
    public async Task RemovalFailure_DoesNotFailScrape_OrForceRemoval()
    {
        var engine = new FakeEngine { FailRemoval = true };
        var pipeline = new DesignPipeline().UseVersionImage().UseContainer().Use(next => async ctx =>
        {
            await File.WriteAllTextAsync(Path.Combine(ctx.OutputDir, "result.json"), "{}");
            await next(ctx);
        }).Build();
        (await Runner(engine, pipeline: pipeline, versions: ["1.0"]).RunAsync([])).ShouldBe(0);
        File.Exists(Path.Combine(_output, "result.json")).ShouldBeTrue();
        engine.Images.Values.Count(i => i.Stage == "version").ShouldBe(1);
        engine.Commands.Single(c => c[1] == "rmi").Length.ShouldBe(3);
    }

    [Fact]
    public async Task SequentialDuplicateVersion_RechecksImageAfterPreviousCleanup()
    {
        var engine = new FakeEngine();
        var pipeline = new DesignPipeline().UseVersionImage().UseContainer().Build();
        (await Runner(engine, pipeline: pipeline, versions: ["1.0", "1.0"]).RunAsync(["--parallel", "1"])).ShouldBe(0);
        engine.Builds.Count(b => b.Stage == "version").ShouldBe(2);
        engine.Images.Values.ShouldNotContain(i => i.Stage == "version");
    }
    public void Dispose()
    {
        if (Directory.Exists(_output)) Directory.Delete(_output, true);
    }

    private sealed class ThrowingCollector : IVersionCollector
    {
        public Task<IReadOnlyList<string>> CollectVersionsAsync(CancellationToken ct = default) =>
            throw new InvalidOperationException("Must not collect versions");
        public Task<IReadOnlyList<string>> CollectItemsAsync(CancellationToken ct = default) =>
            CollectVersionsAsync(ct);
    }

    private sealed class FakeEngine
    {
        public record Image(string Id, string Platform, string Stage);
        public record Build(string Tag, string Stage, string Dockerfile);
        public ConcurrentDictionary<string, Image> Images { get; } = new();
        public ConcurrentQueue<string[]> Commands { get; } = new();
        public ConcurrentQueue<Build> Builds { get; } = new();
        public ConcurrentDictionary<string, string> Containers { get; } = new();
        public bool FailStart { get; set; }
        public bool FailRemoval { get; set; }
        public string? FailStage { get; set; }
        public string? FailScript { get; set; }
        public string? ExtraListedTag { get; set; }

        public FakeEngine() => SetBase("original-parent", "linux/amd64");
        public void SetBase(string seed, string platform, string image = "alpine:3.19") =>
            Images[image] = new Image(Hash(seed + platform), platform, "");
        private static string Hash(string text) =>
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
        private static string After(string[] args, string flag) => args[Array.IndexOf(args, flag) + 1];

        public async Task<string> Run(string[] args)
        {
            Commands.Enqueue(args);
            if (args[1] == "image" && args[2] == "inspect")
            {
                if (!Images.TryGetValue(args[^1], out var image)) throw new InvalidOperationException("Missing image");
                return image.Id + " " + image.Platform;
            }
            if (args[1] == "pull")
            {
                SetBase("pulled-parent", After(args, "--platform"), args[^1]);
                return "";
            }
            if (args[1] == "build")
            {
                var tag = After(args, "--tag");
                var file = After(args, "--file");
                Path.GetDirectoryName(file).ShouldBe(args[^1]);
                var dockerfile = await File.ReadAllTextAsync(file);
                var stage = tag.Contains(":base-") ? "base" : "version";
                if (stage == FailStage || (FailScript is not null && dockerfile.Contains(FailScript)))
                    throw new InvalidOperationException("Build failed");
                Images[tag] = new Image(Hash(dockerfile), After(args, "--platform"), stage);
                Builds.Enqueue(new Build(tag, stage, dockerfile));
                return "";
            }
            if (args[1] == "image" && args[2] == "ls")
            {
                var stage = args.Single(a => a.StartsWith("label=io.frenchexdev.binarywrapper.stage=")).Split('=').Last();
                return string.Join("\n", Images.Where(pair => pair.Value.Stage == stage).Select(pair => pair.Key))
                    + (ExtraListedTag is null ? "" : "\n" + ExtraListedTag);
            }
            if (args[1] == "run")
            {
                if (FailStart) throw new InvalidOperationException("Container start failed");
                if (!Images.ContainsKey(args[3])) throw new InvalidOperationException("Starting from a missing image");
                var id = Guid.NewGuid().ToString("N");
                Containers[id] = args[3];
                return id;
            }
            if (args[1] == "rm") { Containers.TryRemove(args[^1], out _); return ""; }
            if (args[1] == "rmi")
            {
                if (FailRemoval || Containers.Values.Contains(args[2]))
                    throw new InvalidOperationException("Image is in use");
                Images.TryRemove(args[2], out _);
                return "";
            }
            throw new InvalidOperationException("Unexpected command: " + string.Join(' ', args));
        }
    }
}

