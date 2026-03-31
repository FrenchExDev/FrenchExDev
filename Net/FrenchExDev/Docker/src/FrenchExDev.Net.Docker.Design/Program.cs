using FrenchExDev.Net.BinaryWrapper.Design;
using FrenchExDev.Net.BinaryWrapper.Design.Lib;
using FrenchExDev.Net.Wrapper.Versioning;
using Microsoft.Extensions.Logging;

var env = DotEnvLoader.Load();
env.TryGetValue("GITHUB_TOKEN", out var githubToken);

Func<string, ILogger, IHelpParser> parser = (_, _) => HelpParsers.Create("cobra");

var pipeline = new DesignPipeline()
    .UseImageBuild(
        imageTagPrefix: "docker-scrape",
        baseImage: "alpine:3.19",
        installScript: v =>
            "apk add --no-cache curl tar > /dev/null 2>&1 && " +
            $"curl -fsSL https://download.docker.com/linux/static/stable/x86_64/docker-{v}.tgz -o /tmp/docker.tgz && " +
            "tar xzf /tmp/docker.tgz --no-same-owner -C /tmp docker/docker && " +
            "mv /tmp/docker/docker /usr/local/bin/docker && " +
            "chmod +x /usr/local/bin/docker && " +
            "rm -rf /tmp/docker.tgz /tmp/docker")
    .UseContainer()
    .UseScraper("docker", parser)
    .Build();

var reparsePipeline = new DesignPipeline()
    .UseCachedHelp()
    .UseScraper("docker", parser)
    .Build();

return await new DesignPipelineRunner
{
    VersionCollector = new GitHubTagsVersionCollector("docker", "cli", token: githubToken),
    Pipeline = pipeline,
    ReparsePipeline = reparsePipeline,
    DefaultMinVersion = "23.0.0",
    OutputFilePattern = "docker-{version}.json",
    OutputDir = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", "FrenchExDev.Net.Docker", "scrape")),
}.RunAsync(args);
