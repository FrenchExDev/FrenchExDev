using FrenchExDev.Net.BinaryWrapper.Design;
using FrenchExDev.Net.BinaryWrapper.Design.Lib;
using FrenchExDev.Net.Wrapper.Versioning;
using Microsoft.Extensions.Logging;

var env = DotEnvLoader.Load();
env.TryGetValue("GITHUB_TOKEN", out var githubToken);

Func<string, ILogger, IHelpParser> parser = (_, _) => HelpParsers.Create("cobra");

var pipeline = new DesignPipeline()
    .UseImageBuild(
        imageTagPrefix: "podman-scrape",
        baseImage: "alpine:3.19",
        installScript: v =>
        {
            var asset = GitHubReleasesVersionCollector.CompareVersionStrings(v, "4.4.0") >= 0
                ? "podman-remote-static-linux_amd64.tar.gz"
                : "podman-remote-static.tar.gz";
            return "apk add --no-cache curl tar > /dev/null 2>&1 && " +
                $"curl -fsSL https://github.com/containers/podman/releases/download/v{v}/{asset} -o /tmp/podman.tar.gz && " +
                "tar xzf /tmp/podman.tar.gz --no-same-owner -C /tmp && " +
                "find /tmp -name 'podman*' -type f | head -1 | xargs -I{} mv {} /usr/local/bin/podman && " +
                "chmod +x /usr/local/bin/podman && " +
                "rm -rf /tmp/podman.tar.gz /tmp/bin";
        })
    .UseContainer()
    .UseScraper("podman", parser)
    .Build();

var reparsePipeline = new DesignPipeline()
    .UseCachedHelp()
    .UseScraper("podman", parser)
    .Build();

return await new DesignPipelineRunner
{
    VersionCollector = new GitHubReleasesVersionCollector("containers", "podman", token: githubToken),
    Pipeline = pipeline,
    ReparsePipeline = reparsePipeline,
    DefaultMinVersion = "4.1.0",
    OutputFilePattern = "podman-{version}.json",
    OutputDir = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", "FrenchExDev.Net.Podman", "scrape")),
}.RunAsync(args);
