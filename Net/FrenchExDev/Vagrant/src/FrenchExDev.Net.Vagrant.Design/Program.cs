using FrenchExDev.Net.BinaryWrapper.Design;
using FrenchExDev.Net.BinaryWrapper.Design.Lib;
using FrenchExDev.Net.Vagrant.Design;
using Microsoft.Extensions.Logging;

if (args.Any(arg => arg is "--help" or "-h"))
{
    Console.WriteLine("""
        Collect vagrant help using shared dependencies and per-version images (debian:bookworm).
          --min-version <version>       Minimum version (default: none).
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

Func<string, ILogger, IHelpParser> parser = (v, logger) => new LoggingHelpParser(new VagrantHelpParser(), logger, v);

var images = new DesignImagePlan
{
    ImageName = "vagrant-cli",
    BaseImage = "debian:bookworm",
    Platform = "linux/amd64",
    Shell = "bash",
    BaseInstallScript = "apt-get update -qq && apt-get install -y -qq curl",
    InstallScript = v =>
        $"(curl -fsSL https://releases.hashicorp.com/vagrant/{v}/vagrant_{v}-1_amd64.deb -o /tmp/vagrant.deb || " +
        $"curl -fsSL https://releases.hashicorp.com/vagrant/{v}/vagrant_{v}_x86_64.deb -o /tmp/vagrant.deb) &&" +
        "dpkg -i /tmp/vagrant.deb > /dev/null 2>&1 && rm /tmp/vagrant.deb && " +
        "sed -i 's/@_wsl = true/@_wsl = false/' /opt/vagrant/embedded/gems/gems/vagrant-*/lib/vagrant/util/platform.rb && " +
        "apt-get clean > /dev/null 2>&1",
};

var pipeline = new DesignPipeline()
    .UseVersionImage()
    .UseContainer()
    .UseScraper("vagrant", parserFactory: parser, helpFlag: "-h")
    .Build();

var reparsePipeline = new DesignPipeline()
    .UseCachedHelp()
    .UseScraper("vagrant", parserFactory: parser, helpFlag: "-h")
    .Build();

return await new DesignPipelineRunner
{
    ImagePlanResolver = new SingleDesignImagePlanResolver(images),
    VersionCollector = new VagrantVersionCollector(),
    Pipeline = pipeline,
    ReparsePipeline = reparsePipeline,
    MinLogLevel = LogLevel.Debug,
    OutputFilePattern = "vagrant-{version}.json",
    OutputDir = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", "FrenchExDev.Net.Vagrant", "scrape")),
}.RunAsync(args);
