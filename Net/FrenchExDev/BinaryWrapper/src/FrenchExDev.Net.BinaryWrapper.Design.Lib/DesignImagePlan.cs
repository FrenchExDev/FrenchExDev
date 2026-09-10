using System.Diagnostics;
using FrenchExDev.Net.BinaryWrapper.Design;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace FrenchExDev.Net.BinaryWrapper.Design.Lib;

/// <summary>Recipes for a reusable dependency image and its per-version software images.</summary>
public sealed class DesignImagePlan
{
    /// <summary>Cache namespace, e.g. dotnet-sdk. A lowercase image name without a registry or tag.</summary>
    public required string ImageName { get; init; }
    public required string BaseImage { get; init; }
    public string BaseInstallScript { get; init; } = ":";
    public required Func<string, string> InstallScript { get; init; }
    public string Platform { get; init; } = "linux/amd64";
    public string Shell { get; init; } = "sh";

    internal void Validate()
    {
        if (!Regex.IsMatch(ImageName, @"^[a-z0-9][a-z0-9._-]{0,63}$"))
            throw new ArgumentException("ImageName must be a lowercase image name (maximum 64 characters).");
        if (string.IsNullOrWhiteSpace(BaseImage) || BaseImage.StartsWith('-') || BaseImage.Any(char.IsWhiteSpace))
            throw new ArgumentException("BaseImage must be one image reference.");
        if (!Regex.IsMatch(Platform, @"^linux/[a-z0-9_]+$"))
            throw new ArgumentException("Platform must be linux/<architecture>, e.g. linux/amd64.");
        ArgumentException.ThrowIfNullOrWhiteSpace(Shell);
        ArgumentException.ThrowIfNullOrWhiteSpace(BaseInstallScript);
        ArgumentNullException.ThrowIfNull(InstallScript);
    }
}

/// <summary>Prepares shared images and coordinates their use and cleanup across invocations.</summary>
internal sealed class DesignImageCache
{
    private const string LabelPrefix = "io.frenchexdev.binarywrapper";
    private readonly DesignImagePlan _plan;
    private readonly string _runtime;
    private readonly Func<string[], Task<string>> _run;
    private readonly ILogger _logger;
    private readonly string _directory;
    private readonly bool _streamBuildOutput;
    private ImageInfo? _preparedBase;

    private sealed record ImageInfo(string Tag, string Id, string Platform);

    public DesignImageCache(DesignImagePlan plan, string runtime, Func<string[], Task<string>> run,
        ILogger logger, string outputDirectory, bool streamBuildOutput = false)
    {
        plan.Validate();
        _plan = plan;
        _runtime = runtime;
        _run = run;
        _logger = logger;
        _directory = Path.Combine(outputDirectory, ".images", plan.ImageName);
        _streamBuildOutput = streamBuildOutput;
    }

    private string Repository => $"localhost/binarywrapper/{_plan.ImageName}";

    public async Task PrepareAsync()
    {
        var parent = await InspectAsync(_plan.BaseImage);
        if (parent is null || parent.Platform != _plan.Platform)
        {
            await _run([_runtime, "pull", "--platform", _plan.Platform, _plan.BaseImage]);
            parent = await InspectAsync(_plan.BaseImage);
        }
        if (parent is null || parent.Platform != _plan.Platform)
            throw new InvalidOperationException($"Base image {_plan.BaseImage} is not available for {_plan.Platform}.");

        _preparedBase = (await BuildAsync(parent, "base", "", _plan.BaseInstallScript)).Image;
    }

    public async Task<string> GetVersionImageAsync(string version)
    {
        if (_preparedBase is null)
            throw new InvalidOperationException("The dependency image must be prepared before version builds.");
        var built = await BuildAsync(_preparedBase, "version", version, _plan.InstallScript(version));
        return built.Image.Tag;
    }

    public async Task<VersionImageLease> AcquireVersionImageAsync(string version, bool removeAfterUse)
    {
        if (_preparedBase is null)
            throw new InvalidOperationException("The dependency image must be prepared before version builds.");
        var built = await BuildAsync(_preparedBase, "version", version, _plan.InstallScript(version), retainUsage: true);
        return new VersionImageLease(this, built.Image.Tag, built.Usage!, removeAfterUse);
    }

    internal sealed class VersionImageLease(DesignImageCache cache, string tag, FileStream usage, bool removeAfterUse)
        : IAsyncDisposable
    {
        private FileStream? _usage = usage;
        public string Tag { get; } = tag;

        public ValueTask DisposeAsync()
        {
            var current = Interlocked.Exchange(ref _usage, null);
            return current is null ? ValueTask.CompletedTask : new(cache.ReleaseVersionImageAsync(Tag, current, removeAfterUse));
        }
    }

    private async Task ReleaseVersionImageAsync(string tag, FileStream usage, bool removeAfterUse)
    {
        try
        {
            if (!removeAfterUse) return;
            using var buildLock = await DesignImageBuildLock.AcquireAsync(tag, _logger);
            usage.Dispose();
            using var unused = DesignImageBuildLock.TryAcquireUnused(tag);
            if (unused is null)
            {
                _logger.LogInformation("Version image still in use by another scrape: {Tag}", tag);
                return;
            }
            // Never force removal: containers outside this runner also protect their images.
            await _run([_runtime, "rmi", tag]);
            _logger.LogInformation("Removed version image after scraping: {Tag}", tag);
        }
        catch (Exception ex)
        {
            // Cleanup must not hide a scrape failure or discard successfully collected output.
            _logger.LogWarning(ex, "Could not remove version image after scraping: {Tag}", tag);
        }
        finally
        {
            usage.Dispose();
        }
    }

    private async Task<(ImageInfo Image, FileStream? Usage)> BuildAsync(
        ImageInfo parent, string stage, string version, string script, bool retainUsage = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(script);
        script = script.Replace("\r\n", "\n").Replace('\r', '\n');
        // Include the actual parent identity so a refreshed/rebuilt parent invalidates descendants.
        var recipe = JsonSerializer.Serialize(new
        {
            Format = 1, Parent = parent.Id, _plan.Platform, _plan.Shell, Script = script, Stage = stage, Version = version
        });
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(recipe))).ToLowerInvariant();
        var tag = $"{Repository}:{stage}-{key}";
        // Recheck the engine only after acquiring the cross-process lock. It also protects
        // recipe/log/metadata writes when two invocations share an output directory.
        using var buildLock = await DesignImageBuildLock.AcquireAsync(tag, _logger);
        var directory = Path.Combine(_directory, key);
        Directory.CreateDirectory(directory);
        var dockerfile = $"FROM {parent.Tag}\n"
            + "RUN " + JsonSerializer.Serialize(new[] { _plan.Shell, "-ec", script }) + "\n"
            + $"LABEL {LabelPrefix}.cache={JsonSerializer.Serialize(_plan.ImageName)} "
            + $"{LabelPrefix}.stage={JsonSerializer.Serialize(stage)} "
            + $"{LabelPrefix}.recipe={JsonSerializer.Serialize(key)} "
            + $"{LabelPrefix}.version={JsonSerializer.Serialize(version)}\n";
        var dockerfilePath = Path.Combine(directory, "Dockerfile");
        await WriteIfChangedAsync(dockerfilePath, dockerfile);
        // Only this generated recipe is sent; .env and repository files stay outside the context.
        await WriteIfChangedAsync(Path.Combine(directory, ".dockerignore"), "*\n!Dockerfile\n");

        var image = await InspectAsync(tag);
        if (image is null || image.Platform != _plan.Platform)
        {
            _logger.LogInformation("Building {Stage} image {Tag} ({Version})", stage, tag, version);
            var arguments = new List<string> { _runtime, "build", "--platform", _plan.Platform, "--force-rm" };
            if (Path.GetFileNameWithoutExtension(_runtime).Equals("podman", StringComparison.OrdinalIgnoreCase))
                arguments.AddRange(["--layers", "--pull=never"]);
            else
                arguments.Add("--pull=false");
            arguments.AddRange(["--tag", tag, "--file", dockerfilePath, directory]);
            await RunBuildAsync(arguments.ToArray(), directory, stage, version);
            image = await InspectAsync(tag);
            if (image is null || image.Platform != _plan.Platform)
                throw new InvalidOperationException($"Build did not produce {tag} for {_plan.Platform}.");
        }
        else
            _logger.LogInformation("Reusing {Stage} image {Tag}", stage, tag);

        await WriteIfChangedAsync(Path.Combine(directory, "image.json"), JsonSerializer.Serialize(new
        {
            Tag = tag, ImageId = image.Id, ParentTag = parent.Tag, ParentId = parent.Id,
            _plan.Platform, Stage = stage, Version = version, RecipeHash = key
        }, new JsonSerializerOptions { WriteIndented = true }));
        // Acquire shared usage before releasing the build lock, including the gap before container startup.
        return (image, retainUsage ? DesignImageBuildLock.AcquireUsage(tag) : null);
    }

    private async Task RunBuildAsync(string[] arguments, string directory, string stage, string version)
    {
        var name = stage == "base" ? $"{_plan.ImageName}/base" : $"{_plan.ImageName}/{version}";
        var logPath = Path.Combine(directory, "build.log");
        using var log = new StreamWriter(logPath, false, new UTF8Encoding(false)) { AutoFlush = true };
        var sync = new object();
        var elapsed = Stopwatch.StartNew();
        void Report(string line)
        {
            lock (sync) log.WriteLine(line);
            _logger.LogInformation("[{Build}] {Output}", name, line);
        }

        Report($"Build started. Log: {logPath}");
        try
        {
            var build = _streamBuildOutput
                ? ProcessRunnerContainerRuntime.RunProcessAsync(arguments, Report, Report)
                : _run(arguments);
            using var heartbeat = new PeriodicTimer(TimeSpan.FromSeconds(30));
            while (!build.IsCompleted)
            {
                var tick = heartbeat.WaitForNextTickAsync().AsTask();
                if (await Task.WhenAny(build, tick) == build) break;
                await tick;
                Report($"Build still running ({elapsed.Elapsed.TotalSeconds:F0}s elapsed).");
            }
            var output = await build;
            if (!_streamBuildOutput && !string.IsNullOrWhiteSpace(output))
                Report(output.TrimEnd());
            Report($"Build completed in {elapsed.Elapsed.TotalSeconds:F1}s.");
        }
        catch (Exception ex)
        {
            Report($"Build failed after {elapsed.Elapsed.TotalSeconds:F1}s: {ex.Message}");
            throw;
        }
    }

    public async Task CleanAsync()
    {
        // Only remove our tagged images, without forcing removal from running containers.
        // Children go first; distribution images and other wrappers are outside this scope.
        foreach (var stage in new[] { "version", "base" })
        {
            var output = await _run([_runtime, "image", "ls",
                "--filter", $"label={LabelPrefix}.cache={_plan.ImageName}",
                "--filter", $"label={LabelPrefix}.stage={stage}",
                "--format", "{{.Repository}}:{{.Tag}}"]);
            foreach (var tag in output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct())
            {
                if (!Regex.IsMatch(tag, "^" + Regex.Escape(Repository + ":" + stage + "-") + "[0-9a-f]{64}$"))
                    continue;
                await _run([_runtime, "rmi", tag]);
                _logger.LogInformation("Removed cached image {Tag}", tag);
            }
        }
    }

    private async Task<ImageInfo?> InspectAsync(string tag)
    {
        string output;
        try { output = await _run([_runtime, "image", "inspect", "--format", "{{.Id}} {{.Os}}/{{.Architecture}}", tag]); }
        catch { return null; }
        var parts = output.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2 || !Regex.IsMatch(parts[0], @"^(sha256:)?[0-9a-fA-F]{64}$"))
            throw new InvalidOperationException($"Unexpected image inspection output for {tag}.");
        return new ImageInfo(tag, parts[0].Replace("sha256:", "").ToLowerInvariant(), parts[1]);
    }

    private static async Task WriteIfChangedAsync(string path, string content)
    {
        if (!File.Exists(path) || await File.ReadAllTextAsync(path) != content)
            await File.WriteAllTextAsync(path, content, new UTF8Encoding(false));
    }
}

