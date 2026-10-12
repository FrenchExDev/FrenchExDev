using FrenchExDev.Net.BinaryWrapper.Design;
using FrenchExDev.Net.BinaryWrapper.Design.Lib;
using FrenchExDev.Net.Dotnet.Design;
using FrenchExDev.Net.Wrapper.Versioning;
using Microsoft.Extensions.Logging;

var env = DotEnvLoader.Load();
env.TryGetValue("GITHUB_TOKEN", out var githubToken);
githubToken ??= Environment.GetEnvironmentVariable("GITHUB_TOKEN");

// --version optionally selects one SDK tag; all common runner options are forwarded.
string? selectedVersion = null;
var runnerArgs = new List<string>();
for (var i = 0; i < args.Length; i++)
{
    if (args[i] is "--help" or "-h")
    {
        Console.WriteLine("""
            Collect .NET SDK help in alpine:3.19 containers using GitHub dotnet/sdk tags.
              --version <sdk-version>       Select one stable SDK tag (e.g. 8.0.100).
              --min-version <sdk-version>   Minimum SDK version (default: 8.0.100).
              --list                        List matching tags without starting containers.
              --fail-fast                   Stop scheduling after a failure; preserve failed versions for replay.
              --stop-file <path>            Share a graceful stop signal with other clients (implies --fail-fast).
              --retry-known-missing         Retry excluded versions when using --missing.
              --missing                     Collect versions without an existing JSON.
              --parallel <n>                Concurrent SDK containers (default: 2).
              --scrape-parallel <n>          Concurrent help commands per container.
              --runtime <podman|docker>      Container runtime (default: podman).
              --output <directory>          Override scrape output directory.
              --reparse                     Reparse cached help without containers or GitHub.
              --build-base                  Prepare only the shared dependency image (no GitHub).
              --build-images                Build selected SDK images without collecting help.
              --clean-images                Remove this wrapper's cached images (no GitHub).
              --keep-images                 Keep SDK images after scraping (default: remove).
              --dashboard                   Display the live progress dashboard.
              --add-known-missing <v,...>    Record unavailable SDKs.
              --remove-known-missing <v,...> Remove recorded unavailable SDKs.
              --list-known-missing          List recorded unavailable SDKs.
            """);
        return 0;
    }
    if (args[i] == "--version")
    {
        if (++i >= args.Length || DotnetSdk.VersionFromTag(args[i]) is not { } version)
        {
            Console.Error.WriteLine("--version requires a stable SDK version such as 8.0.100.");
            return 2;
        }
        selectedVersion = version;
    }
    else
        runnerArgs.Add(args[i]);
}

Func<string, ILogger, IHelpParser> parser = (v, logger) =>
    new LoggingHelpParser(new DotnetHelpParser(), logger, v);

var pipeline = new DesignPipeline()
    .UseVersionImage()
    .UseContainer()
    .Use(next => async ctx =>
    {
        var runInContainer = ctx.RunHelp!;
        Task<string> RunDotnet(string[] command) =>
            runInContainer(["env", .. DotnetSdk.EnvironmentVariables, .. command]);

        // Warm up the SDK before parallel help calls and detect failed installations.
        var actualVersion = (await RunDotnet(["dotnet", "--version"])).Trim();
        if (actualVersion != ctx.Version)
            throw new InvalidOperationException($"Expected SDK {ctx.Version}, found {actualVersion}.");
        var rootHelp = await DotnetHelpProcess.RunAsync(ctx.RuntimeBinary, ctx.ContainerId!, ["dotnet", "--help"]);
        if (new DotnetHelpParser().Parse(rootHelp, "dotnet")?.SubCommands.Any(c => c.Name == "build") != true)
            throw new InvalidOperationException("dotnet --help did not expose SDK commands.");

        ctx.RunHelp = async command =>
        {
            try
            {
                return command.Length == 2 ? rootHelp
                    : await DotnetHelpProcess.RunAsync(ctx.RuntimeBinary, ctx.ContainerId!, command);
            }
            catch (Exception ex)
            {
                ctx.Logger.LogWarning(ex, "Help unavailable: {Command}", string.Join(' ', command));
                throw;
            }
        };
        await next(ctx);
    })
    .UseScraper("dotnet", parser)
    .Build();

var reparsePipeline = new DesignPipeline()
    .UseCachedHelp()
    .UseScraper("dotnet", parser)
    .Build();

var runner = new DesignPipelineRunner
{
    VersionCollector = new GitHubTagsVersionCollector("dotnet", "sdk",
        tagToVersion: tag => DotnetSdk.VersionFromTag(tag) is { } version
            && (selectedVersion is null || version == selectedVersion) ? version : null,
        token: githubToken),
    ImagePlanResolver = new SingleDesignImagePlanResolver(new DesignImagePlan
    {
        ImageName = "dotnet-sdk",
        BaseImage = "alpine:3.19",
        BaseInstallScript = DotnetSdk.BaseInstallScript,
        InstallScript = DotnetSdk.InstallScript,
        Platform = "linux/amd64",
    }),
    Pipeline = pipeline,
    ReparsePipeline = reparsePipeline,
    DefaultMinVersion = selectedVersion ?? "8.0.100",
    DefaultParallelism = 2,
    OutputFilePattern = "dotnet-{version}.json",
    OutputDir = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", "FrenchExDev.Net.Dotnet", "scrape")),
};
try
{
    return await runner.RunAsync(runnerArgs.ToArray());
}
catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized)
{
    Console.Error.WriteLine("GitHub rejected GITHUB_TOKEN. Update or remove the token in .env / the environment, then retry.");
    return -1;
}
