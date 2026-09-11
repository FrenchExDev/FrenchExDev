using FrenchExDev.Net.BinaryWrapper.Design.Lib;
using Microsoft.Extensions.Logging;
using Shouldly;

namespace FrenchExDev.Net.BinaryWrapper.Design.Lib.Tests;

public sealed partial class DesignImagePlanTests
{
    private DesignPipelineRunner ResolverRunner(FakeEngine engine, IDesignImagePlanResolver resolver,
        string[]? versions = null, VersionDelegate? pipeline = null, IVersionCollector? collector = null) => new()
    {
        ImagePlanResolver = resolver,
        VersionCollector = collector ?? new StaticVersionCollector(versions ?? ["1.0", "2.0"]),
        Pipeline = pipeline ?? new DesignPipeline().UseVersionImage().Build(),
        ReparsePipeline = _ => Task.CompletedTask,
        OutputFilePattern = "tool-{version}.json",
        OutputDir = _output, RunProcess = engine.Run, MinLogLevel = LogLevel.None,
    };

    private sealed class TestResolver(params DesignImagePlan[] plans) : IDesignImagePlanResolver
    {
        public IReadOnlyList<DesignImagePlan> Plans { get; } = Array.AsReadOnly(plans);
        public List<string> Calls { get; } = [];
        public DesignImagePlan? Override { get; init; }
        public DesignImagePlan Resolve(string version)
        {
            Calls.Add(version);
            return Override ?? Plans[version == "1.0" ? 0 : Plans.Count - 1];
        }
    }

    private sealed class ThrowingResolver : IDesignImagePlanResolver
    {
        public IReadOnlyList<DesignImagePlan> Plans => throw new InvalidOperationException("Must not read recipes");
        public DesignImagePlan Resolve(string version) => throw new InvalidOperationException("Must not resolve");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Resolver_UsesCorrectParentsBeforeParallelWorkers_AndReusesImages(bool sharedBase)
    {
        var engine = new FakeEngine();
        var resolver = new TestResolver(Plan("echo legacy", "install legacy"),
            Plan(sharedBase ? "echo legacy" : "echo modern", "install modern"));
        var baseCount = sharedBase ? 1 : 2;
        var pipeline = new DesignPipeline().UseVersionImage().UseContainer().Use(next => async ctx =>
        {
            engine.Builds.Count(b => b.Stage == "base").ShouldBe(baseCount);
            var versionBuild = engine.Builds.Single(b => b.Tag == ctx.ImageTag);
            versionBuild.Dockerfile.ShouldContain(ctx.Version == "1.0" ? "install legacy" : "install modern");
            var parentTag = versionBuild.Dockerfile.Split('\n')[0]["FROM ".Length..];
            var parent = engine.Builds.Single(b => b.Tag == parentTag);
            parent.Dockerfile.ShouldContain(sharedBase || ctx.Version == "1.0" ? "echo legacy" : "echo modern");
            engine.Containers[ctx.ContainerId!].ShouldBe(ctx.ImageTag);
            await next(ctx);
        }).Build();
        var runner = ResolverRunner(engine, resolver, pipeline: pipeline);
        (await runner.RunAsync(["--parallel", "2", "--keep-images"])).ShouldBe(0);
        resolver.Calls.ShouldBe(new[] { "1.0", "2.0" });
        engine.Builds.Count.ShouldBe(baseCount + 2);
        engine.Builds.Take(baseCount).ShouldAllBe(b => b.Stage == "base");

        (await runner.RunAsync(["--keep-images"])).ShouldBe(0);
        engine.Builds.Count.ShouldBe(baseCount + 2);
    }

    [Fact]
    public async Task Resolver_ResolvesOnlyFilteredDistinctVersions()
    {
        var engine = new FakeEngine();
        var resolver = new TestResolver(Plan("echo unused"), Plan("echo selected"));
        Directory.CreateDirectory(_output);
        await File.WriteAllTextAsync(Path.Combine(_output, "tool-1.0.json"), "{}");
        var runner = ResolverRunner(engine, resolver, versions: ["0.0", "1.0", "2.0", "2.0"]);
        (await runner.RunAsync(["--min-version", "1.0", "--missing", "--build-images"])).ShouldBe(0);
        resolver.Calls.ShouldBe(new[] { "2.0" });
        engine.Builds.Count.ShouldBe(2);
        engine.Builds.Single(b => b.Stage == "base").Dockerfile.ShouldContain("echo selected");
    }

    [Theory]
    [InlineData("install", 1)]
    [InlineData("setup", 2)]
    [InlineData("parent", 2)]
    [InlineData("platform", 2)]
    [InlineData("shell", 2)]
    public async Task Resolver_RecipeChangesInvalidateOnlyTheirDescendants(string change, int extraBuilds)
    {
        var engine = new FakeEngine();
        var legacy = Plan("echo legacy");
        var modern = Plan("echo modern");
        (await ResolverRunner(engine, new TestResolver(legacy, modern)).RunAsync(["--build-images"])).ShouldBe(0);
        var legacyTags = engine.Builds.Where(b => b.Dockerfile.Contains("echo legacy")
            || b.Dockerfile.Contains("install 1.0")).Select(b => b.Tag).ToArray();
        var changed = Plan(change == "setup" ? "echo changed" : "echo modern",
            change == "install" ? "install changed" : "echo install",
            platform: change == "platform" ? "linux/arm64" : "linux/amd64",
            baseImage: change == "parent" ? "alpine:3.20" : "alpine:3.19",
            shell: change == "shell" ? "bash" : "sh");
        (await ResolverRunner(engine, new TestResolver(legacy, changed)).RunAsync(["--build-images"])).ShouldBe(0);
        engine.Builds.Count.ShouldBe(4 + extraBuilds);
        foreach (var tag in legacyTags)
            engine.Builds.Count(b => b.Tag == tag).ShouldBe(1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Resolver_BuildBaseAndCleanDoNotDiscoverOrResolveVersions(bool sharedBase)
    {
        var engine = new FakeEngine();
        var resolver = new TestResolver(Plan("echo legacy"), Plan(sharedBase ? "echo legacy" : "echo modern"));
        var runner = ResolverRunner(engine, resolver, collector: new ThrowingCollector());
        (await runner.RunAsync(["--build-base"])).ShouldBe(0);
        resolver.Calls.ShouldBeEmpty();
        engine.Builds.Count.ShouldBe(sharedBase ? 1 : 2);
        (await runner.RunAsync(["--clean-images"])).ShouldBe(0);
        resolver.Calls.ShouldBeEmpty();
        engine.Images.Keys.ShouldBe(new[] { "alpine:3.19" });
    }

    [Fact]
    public async Task Resolver_CleanRemovesAllVersionsBeforeAllBases()
    {
        var engine = new FakeEngine();
        var resolver = new TestResolver(Plan("echo legacy"), Plan("echo modern"));
        (await ResolverRunner(engine, resolver).RunAsync(["--build-images"])).ShouldBe(0);
        engine.ExtraListedTag = "localhost/binarywrapper/unrelated:base-" + new string('a', 64);
        (await ResolverRunner(engine, resolver, collector: new ThrowingCollector()).RunAsync(["--clean-images"])).ShouldBe(0);
        var removals = engine.Commands.Where(c => c[1] == "rmi").ToArray();
        removals.Length.ShouldBe(4);
        removals.Take(2).ShouldAllBe(c => c[2].Contains(":version-"));
        removals.Skip(2).ShouldAllBe(c => c[2].Contains(":base-"));
        removals.ShouldAllBe(c => c.Length == 3);
        engine.Images.Keys.ShouldBe(new[] { "alpine:3.19" });
    }

    [Theory]
    [InlineData("echo modern")]
    [InlineData("install modern")]
    public async Task Resolver_FailedRecipeCanBeRetriedWithoutRebuildingUnaffectedImages(string failingScript)
    {
        var engine = new FakeEngine { FailScript = failingScript };
        var resolver = new TestResolver(Plan("echo legacy"), Plan("echo modern", "install modern"));
        var runner = ResolverRunner(engine, resolver);
        (await runner.RunAsync(["--build-images"])).ShouldBe(1);
        if (failingScript == "echo modern")
            engine.Builds.ShouldNotContain(b => b.Stage == "version");
        engine.FailScript = null;
        (await runner.RunAsync(["--build-images"])).ShouldBe(0);
        engine.Builds.Count.ShouldBe(4);
    }

    [Theory]
    [InlineData("--list")]
    [InlineData("--reparse")]
    [InlineData("empty")]
    public async Task Resolver_ReadOnlyAndEmptyModesDoNotAccessRecipes(string mode)
    {
        var engine = new FakeEngine();
        Directory.CreateDirectory(Path.Combine(_output, "help", "1.0"));
        var runner = ResolverRunner(engine, new ThrowingResolver(), versions: mode == "empty" ? [] : ["1.0"]);
        (await runner.RunAsync(mode == "empty" ? [] : [mode])).ShouldBe(0);
        engine.Commands.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("empty")]
    [InlineData("null")]
    [InlineData("namespace")]
    [InlineData("undeclared")]
    public async Task Resolver_InvalidCatalogFailsBeforeAnyEngineCommand(string invalid)
    {
        var engine = new FakeEngine();
        var other = new DesignImagePlan { ImageName = "another", BaseImage = "alpine:3.19", InstallScript = _ => ":" };
        var resolver = invalid switch
        {
            "empty" => new TestResolver(),
            "null" => new TestResolver((DesignImagePlan)null!),
            "namespace" => new TestResolver(Plan(), other),
            _ => new TestResolver(Plan()) { Override = Plan() },
        };
        (await ResolverRunner(engine, resolver).RunAsync(["--build-images"])).ShouldBe(1);
        engine.Commands.ShouldBeEmpty();
    }

    [Fact]
    public async Task Resolver_ConflictsWithLegacyConfiguration()
    {
        var engine = new FakeEngine();
        var runner = new DesignPipelineRunner
        {
            ImagePlan = Plan(), ImagePlanResolver = new SingleDesignImagePlanResolver(Plan()),
            VersionCollector = new ThrowingCollector(), Pipeline = _ => Task.CompletedTask,
            OutputDir = _output, RunProcess = engine.Run,
        };
        await Should.ThrowAsync<ArgumentException>(() => runner.RunAsync(["--build-base"]));
        engine.Commands.ShouldBeEmpty();
    }

    [Fact]
    public async Task Resolver_SingleRecipeReusesLegacyCache()
    {
        var engine = new FakeEngine();
        var plan = Plan();
        (await Runner(engine, plan).RunAsync(["--build-images"])).ShouldBe(0);
        (await ResolverRunner(engine, new SingleDesignImagePlanResolver(plan)).RunAsync(["--build-images"])).ShouldBe(0);
        engine.Builds.Count.ShouldBe(3);
    }

    [ContainerFact]
    public async Task Resolver_RealContainersUseTheirSelectedBase()
    {
        var resolver = new TestResolver(Plan("echo legacy > /base-marker"), Plan("echo modern > /base-marker"));
        var pipeline = new DesignPipeline().UseVersionImage().UseContainer().Use(next => async ctx =>
        {
            (await ctx.RunProcess(["podman", "exec", ctx.ContainerId!, "cat", "/base-marker"])).Trim()
                .ShouldBe(ctx.Version == "1.0" ? "legacy" : "modern");
            await next(ctx);
        }).Build();
        var runner = new DesignPipelineRunner
        {
            ImagePlanResolver = resolver, VersionCollector = new StaticVersionCollector(["1.0", "2.0"]),
            Pipeline = pipeline, OutputDir = _output, MinLogLevel = LogLevel.None,
        };
        try
        {
            (await runner.RunAsync(["--parallel", "2", "--keep-images"])).ShouldBe(0);
            (await runner.RunAsync(["--keep-images"])).ShouldBe(0);
            Directory.GetFiles(_output, "Dockerfile", SearchOption.AllDirectories).Length.ShouldBe(4);
            (await runner.RunAsync([])).ShouldBe(0);
        }
        finally
        {
            (await runner.RunAsync(["--clean-images"])).ShouldBe(0);
        }
    }

    [ContainerFact]
    public async Task Resolver_RealFailedBuildRetainsBothDiagnosticStreams()
    {
        var runner = new DesignPipelineRunner
        {
            ImagePlanResolver = new SingleDesignImagePlanResolver(
                Plan("echo resolver-stdout; echo resolver-stderr >&2; exit 17")),
            VersionCollector = new ThrowingCollector(), Pipeline = _ => Task.CompletedTask,
            OutputDir = _output, MinLogLevel = LogLevel.None,
        };
        try
        {
            (await runner.RunAsync(["--build-base"])).ShouldBe(1);
            var log = await File.ReadAllTextAsync(Directory.GetFiles(_output, "build.log", SearchOption.AllDirectories).Single());
            log.ShouldContain("resolver-stdout");
            log.ShouldContain("resolver-stderr");
            log.ShouldContain("Build failed");
            log.ShouldContain("17");
        }
        finally
        {
            (await runner.RunAsync(["--clean-images"])).ShouldBe(0);
        }
    }
}
