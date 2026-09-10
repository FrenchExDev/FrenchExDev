using FrenchExDev.Net.BinaryWrapper.Design;
using FrenchExDev.Net.BinaryWrapper.Design.Lib;
using FrenchExDev.Net.Wrapper.Versioning;
using Microsoft.Extensions.Logging;

if (args.Any(arg => arg is "--help" or "-h"))
{
    Console.WriteLine("""
        Collect docker help using shared dependencies and per-version images (alpine:3.19).
          --min-version <version>       Minimum version (default: 23.0.0).
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
          --build-base                  Prepare only the dependency image, without version discovery.
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

Func<string, ILogger, IHelpParser> parser = (_, _) => HelpParsers.Create("cobra");

var images = new DesignImagePlan
{
    ImageName = "docker-cli",
    BaseImage = "alpine:3.19",
    Platform = "linux/amd64",
    BaseInstallScript = "apk add --no-cache curl tar",
    InstallScript = v =>
        $"curl -fsSL https://download.docker.com/linux/static/stable/x86_64/docker-{v}.tgz -o /tmp/docker.tgz && " +
        "tar xzf /tmp/docker.tgz --no-same-owner -C /tmp docker/docker && " +
        "mv /tmp/docker/docker /usr/local/bin/docker && " +
        "chmod +x /usr/local/bin/docker && " +
        "rm -rf /tmp/docker.tgz /tmp/docker",
};

var pipeline = new DesignPipeline()
    .UseVersionImage()
    .UseContainer()
    .UseScraper("docker", parser)
    .Build();

var reparsePipeline = new DesignPipeline()
    .UseCachedHelp()
    .UseScraper("docker", parser)
    .Build();

return await new DesignPipelineRunner
{
    ImagePlanResolver = new SingleDesignImagePlanResolver(images),
    VersionCollector = new GitHubTagsVersionCollector("docker", "cli", token: githubToken),
    Pipeline = pipeline,
    ReparsePipeline = reparsePipeline,
    DefaultMinVersion = "23.0.0",
    OutputFilePattern = "docker-{version}.json",
    OutputDir = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", "FrenchExDev.Net.Docker", "scrape")),
}.RunAsync(args);
