using FrenchExDev.Net.BinaryWrapper.Design;
using FrenchExDev.Net.BinaryWrapper.Design.Lib;
using FrenchExDev.Net.Wrapper.Versioning;
using Microsoft.Extensions.Logging;

var env = DotEnvLoader.Load();
env.TryGetValue("GITHUB_TOKEN", out var githubToken);

Func<string, ILogger, IHelpParser> parser = (_, _) => HelpParsers.Create("argparse");

var pipeline = new DesignPipeline()
    .UseImageBuild(
        imageTagPrefix: "podman-compose-scrape",
        baseImage: "alpine:3.19",
        installScript: v =>
            "apk add --no-cache python3 py3-pip py3-yaml py3-dotenv > /dev/null 2>&1 && " +
            $"pip install --break-system-packages podman-compose=={v} > /dev/null 2>&1")
    .UseContainer()
    .UseScraper("podman-compose", parser)
    .Build();

var reparsePipeline = new DesignPipeline()
    .UseCachedHelp()
    .UseScraper("podman-compose", parser)
    .Build();

return await new DesignPipelineRunner
{
    VersionCollector = new GitHubReleasesVersionCollector("containers", "podman-compose", token: githubToken),
    Pipeline = pipeline,
    ReparsePipeline = reparsePipeline,
    DefaultMinVersion = "1.0.0",
    OutputFilePattern = "podman-compose-{version}.json",
    OutputDir = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", "FrenchExDev.Net.PodmanCompose", "scrape")),
}.RunAsync(args);
