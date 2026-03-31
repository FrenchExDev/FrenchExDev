using FrenchExDev.Net.BinaryWrapper.Design;
using FrenchExDev.Net.BinaryWrapper.Design.Lib;
using FrenchExDev.Net.Packer.Design;
using Microsoft.Extensions.Logging;

Func<string, ILogger, IHelpParser> parser = (_, _) => new PackerHelpParser();

var pipeline = new DesignPipeline()
    .UseInlineContainer(
        baseImage: "alpine:3.19",
        installScript: v =>
            "apk add --no-cache curl unzip && " +
            $"curl -fsSL https://releases.hashicorp.com/packer/{v}/packer_{v}_linux_amd64.zip -o /tmp/p.zip && " +
            "unzip /tmp/p.zip -d /usr/local/bin && rm /tmp/p.zip")
    .UseScraper("packer", parser, helpFlag: "-h")
    .Build();

var reparsePipeline = new DesignPipeline()
    .UseCachedHelp()
    .UseScraper("packer", parser, helpFlag: "-h")
    .Build();

return await new DesignPipelineRunner
{
    VersionCollector = new PackerVersionCollector(),
    Pipeline = pipeline,
    ReparsePipeline = reparsePipeline,
    OutputFilePattern = "packer-{version}.json",
    OutputDir = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", "FrenchExDev.Net.Packer", "scrape")),
}.RunAsync(args);
