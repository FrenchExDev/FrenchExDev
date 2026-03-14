using FrenchExDev.Net.BinaryWrapper.Design;
using FrenchExDev.Net.BinaryWrapper.Design.Lib;
using FrenchExDev.Net.Vagrant.Design;
using Microsoft.Extensions.Logging;

Func<string, ILogger, IHelpParser> parser = (v, logger) => new LoggingHelpParser(new VagrantHelpParser(), logger, v);

var pipeline = new DesignPipeline()
    .UseImageBuild(
        imageTagPrefix: "vagrant-scrape",
        baseImage: "debian:bookworm",
        installScript: v =>
            "apt-get update -qq > /dev/null 2>&1 && " +
            "apt-get install -y -qq curl > /dev/null 2>&1 && " +
            $"(curl -fsSL https://releases.hashicorp.com/vagrant/{v}/vagrant_{v}-1_amd64.deb -o /tmp/vagrant.deb || " +
            $"curl -fsSL https://releases.hashicorp.com/vagrant/{v}/vagrant_{v}_x86_64.deb -o /tmp/vagrant.deb) &&" +
            "dpkg -i /tmp/vagrant.deb > /dev/null 2>&1 && rm /tmp/vagrant.deb && " +
            "sed -i 's/@_wsl = true/@_wsl = false/' /opt/vagrant/embedded/gems/gems/vagrant-*/lib/vagrant/util/platform.rb && " +
            "apt-get clean > /dev/null 2>&1",
        shell: "bash")
    .UseContainer()
    .UseScraper("vagrant", parserFactory: parser, helpFlag: "-h")
    .Build();

var reparsePipeline = new DesignPipeline()
    .UseCachedHelp()
    .UseScraper("vagrant", parserFactory: parser, helpFlag: "-h")
    .Build();

return await new DesignPipelineRunner
{
    VersionCollector = new VagrantVersionCollector(),
    Pipeline = pipeline,
    ReparsePipeline = reparsePipeline,
    MinLogLevel = LogLevel.Debug,
    OutputFilePattern = "vagrant-{version}.json",
    OutputDir = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", "FrenchExDev.Net.Vagrant", "scrape")),
}.RunAsync(args);
