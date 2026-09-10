using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FrenchExDev.Net.BinaryWrapper.Design.Lib;
using FrenchExDev.Net.Wrapper.Versioning;
using Microsoft.Extensions.Logging;

namespace FrenchExDev.Net.BinaryWrapper.Design.ImageBuildProbe;

/// <summary>A separate process exercising the real image cache against a shared, file-backed fake engine.</summary>
public static class Program
{
    public static Task<int> Main(string[] args)
    {
        if (args.Length is < 7 or > 9) throw new ArgumentException("Expected store, output, image name, worker, version, blocked stage, failed stage, optional mode and optional different-base flag.");
        var engine = new FileEngine(args[0], args[3], args[5], args[6]);
        var runner = new DesignPipelineRunner
        {
            VersionCollector = new StaticVersionCollector([args[4]]),
            Pipeline = new DesignPipeline().UseVersionImage().Use(next => async ctx =>
            {
                await engine.ScrapeAsync(ctx.ImageTag!);
                await next(ctx);
            }).Build(),
            ImagePlanResolver = new ProbeResolver(args[2], args.Length == 9 && args[8] == "different-base"),
            OutputDir = args[1], RuntimeBinary = "fake-engine", RunProcess = engine.RunAsync,
            MinLogLevel = LogLevel.Information,
        };
        return runner.RunAsync(args.Length >= 8 && args[7] == "scrape" ? [] : ["--build-images"]);
    }

    private sealed class ProbeResolver(string imageName, bool differentBase) : IDesignImagePlanResolver
    {
        public IReadOnlyList<DesignImagePlan> Plans { get; } = Array.AsReadOnly(new[]
        {
            Create(imageName, "prepare dependencies"),
            Create(imageName, differentBase ? "prepare different dependencies" : "prepare dependencies"),
        });

        public DesignImagePlan Resolve(string version) => Plans[version == "1.0" ? 0 : 1];

        private static DesignImagePlan Create(string imageName, string setup) => new()
        {
            ImageName = imageName, BaseImage = "fake-base:1",
            BaseInstallScript = setup, InstallScript = v => "install " + v,
        };
    }

    private sealed class FileEngine(string store, string worker, string blockedStage, string failedStage)
    {
        private static string Hash(string value) =>
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

        public async Task ScrapeAsync(string tag)
        {
            // Pause before creating any container: only the usage lease protects this interval.
            await File.WriteAllTextAsync(Path.Combine(store, worker + ".scrape"), tag);
            if (blockedStage == "scrape")
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                while (!File.Exists(Path.Combine(store, "release-" + worker)))
                    await Task.Delay(20, timeout.Token);
            }
            if (!File.Exists(Path.Combine(store, Hash(tag) + ".image")))
                throw new InvalidOperationException("Image removed during scraping");
        }

        public async Task<string> RunAsync(string[] args)
        {
            Directory.CreateDirectory(store);
            if (args[1] == "image" && args[2] == "inspect")
            {
                if (args[^1] == "fake-base:1") return Hash("parent") + " linux/amd64";
                var imagePath = Path.Combine(store, Hash(args[^1]) + ".image");
                if (!File.Exists(imagePath)) throw new InvalidOperationException("Missing image");
                return await File.ReadAllTextAsync(imagePath);
            }

            if (args[1] == "build")
            {
                var tag = args[Array.IndexOf(args, "--tag") + 1];
                var stage = tag.Contains(":base-", StringComparison.Ordinal) ? "base" : "version";
                var dockerfile = await File.ReadAllTextAsync(args[Array.IndexOf(args, "--file") + 1]);
                var builds = Path.Combine(store, "builds");
                Directory.CreateDirectory(builds);
                await File.WriteAllTextAsync(Path.Combine(builds, worker + "-" + stage + ".json"),
                    JsonSerializer.Serialize(new { Tag = tag, Stage = stage, Worker = worker }));
                if (stage == blockedStage)
                {
                    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                    while (!File.Exists(Path.Combine(store, "release-" + worker)))
                        await Task.Delay(20, timeout.Token);
                }
                if (stage == failedStage) throw new InvalidOperationException("Simulated build failure");

                // Publish complete fake images atomically, as a container engine does.
                var imagePath = Path.Combine(store, Hash(tag) + ".image");
                var temporary = imagePath + "." + worker + ".tmp";
                await File.WriteAllTextAsync(temporary, Hash(dockerfile) + " linux/amd64");
                File.Move(temporary, imagePath, true);
                return "Image built.";
            }

            if (args[1] == "rmi")
            {
                if (args.Length != 3) throw new InvalidOperationException("Unexpected forced removal");
                var imagePath = Path.Combine(store, Hash(args[2]) + ".image");
                File.Delete(imagePath);
                await File.WriteAllTextAsync(Path.Combine(store, worker + ".removed"), args[2]);
                return "";
            }

            throw new InvalidOperationException("Unexpected fake-engine command: " + string.Join(' ', args));
        }
    }
}
