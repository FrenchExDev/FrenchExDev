using FrenchExDev.Net.BinaryWrapper.Design;
using FrenchExDev.Net.BinaryWrapper.Design.Lib;
using FrenchExDev.Net.Packer.Design;
using Microsoft.Extensions.Logging;

if (args.Any(arg => arg is "--help" or "-h"))
{
    Console.WriteLine("""
        Collect packer help using shared dependencies and per-version images (alpine:3.19).
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

Func<string, ILogger, IHelpParser> parser = (_, _) => new PackerHelpParser();

var images = new DesignImagePlan
{
    ImageName = "packer-cli",
    BaseImage = "alpine:3.19",
    Platform = "linux/amd64",
    BaseInstallScript = "apk add --no-cache curl unzip",
    InstallScript = v =>
        $"curl -fsSL https://releases.hashicorp.com/packer/{v}/packer_{v}_linux_amd64.zip -o /tmp/p.zip && " +
        "unzip /tmp/p.zip -d /usr/local/bin && rm /tmp/p.zip",
};

var pipeline = new DesignPipeline()
    .UseVersionImage()
    .UseContainer()
    .UseScraper("packer", parser, helpFlag: "-h")
    .Build();

var reparsePipeline = new DesignPipeline()
    .UseCachedHelp()
    .UseScraper("packer", parser, helpFlag: "-h")
    .Build();

return await new DesignPipelineRunner
{
    ImagePlanResolver = new SingleDesignImagePlanResolver(images),
    VersionCollector = new PackerVersionCollector(),
    Pipeline = pipeline,
    ReparsePipeline = reparsePipeline,
    OutputFilePattern = "packer-{version}.json",
    OutputDir = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", "FrenchExDev.Net.Packer", "scrape")),
}.RunAsync(args);
