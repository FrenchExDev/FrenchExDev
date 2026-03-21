using FrenchExDev.Net.BinaryWrapper.Design;
using FrenchExDev.Net.BinaryWrapper.Design.Lib;
using FrenchExDev.Net.Wrapper.Versioning;
using Microsoft.Extensions.Logging;

Func<string, ILogger, IHelpParser> parser = (_, _) =>
    new GhStyleHelpParser(binaryName: "glab");

var pipeline = new DesignPipeline()
    .UseImageBuild(
        imageTagPrefix: "glab-scrape",
        baseImage: "alpine:3.19",
        installScript: v =>
            "apk add --no-cache curl tar > /dev/null 2>&1 && " +
            $"curl -fsSL https://gitlab.com/gitlab-org/cli/-/releases/v{v}/downloads/glab_{v}_linux_amd64.tar.gz -o /tmp/glab.tar.gz && " +
            "tar xzf /tmp/glab.tar.gz --no-same-owner -C /tmp && " +
            "mv /tmp/bin/glab /usr/local/bin/glab && " +
            "chmod +x /usr/local/bin/glab && " +
            "rm -rf /tmp/glab.tar.gz /tmp/bin")
    .UseContainer()
    .UseScraper("glab", parser)
    .Build();

var reparsePipeline = new DesignPipeline()
    .UseCachedHelp()
    .UseScraper("glab", parser)
    .Build();

return await new DesignPipelineRunner
{
    VersionCollector = new GitLabReleasesVersionCollector("gitlab-org%2Fcli"),
    Pipeline = pipeline,
    ReparsePipeline = reparsePipeline,
    DefaultMinVersion = "1.47.0",
    OutputFilePattern = "glab-{version}.json",
    OutputDir = Path.GetFullPath(Path.Combine("..", "FrenchExDev.Net.GitLab.Cli", "scrape")),
}.RunAsync(args);
