using FrenchExDev.Net.BinaryWrapper.Design;
using FrenchExDev.Net.BinaryWrapper.Design.Lib;
using Microsoft.Extensions.Logging;

Func<string, ILogger, IHelpParser> parser = (_, _) => HelpParsers.Create("cobra");

var pipeline = new DesignPipeline()
    .UseImageBuild(
        imageTagPrefix: "docker-compose-scrape",
        baseImage: "alpine:3.19",
        installScript: v =>
            "apk add --no-cache curl > /dev/null 2>&1 && " +
            $"curl -fsSL https://github.com/docker/compose/releases/download/v{v}/docker-compose-linux-x86_64 " +
            "-o /usr/local/bin/docker-compose && " +
            "chmod +x /usr/local/bin/docker-compose")
    .UseContainer()
    .UseScraper("docker-compose", parser)
    .Build();

var reparsePipeline = new DesignPipeline()
    .UseCachedHelp()
    .UseScraper("docker-compose", parser)
    .Build();

return await new DesignPipelineRunner
{
    VersionCollector = new GitHubReleasesVersionCollector("docker", "compose"),
    Pipeline = pipeline,
    ReparsePipeline = reparsePipeline,
    DefaultMinVersion = "2.20.0",
    OutputFilePattern = "docker-compose-{version}.json",
    OutputDir = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", "FrenchExDev.Net.DockerCompose", "scrape")),
}.RunAsync(args);
