using FrenchExDev.Net.BinaryWrapper.Design;
using Microsoft.Extensions.Logging;

namespace FrenchExDev.Net.BinaryWrapper.Design.Lib;

/// <summary>
/// Pre-built middleware for common scraping pipeline steps.
/// Each middleware eagerly cleans up its resources when no longer needed.
/// </summary>
public static class DesignPipelineExtensions
{
    /// <summary>
    /// Builds or reuses the version image from the runner's ImagePlan.
    /// Releases the image after inner middleware (including container cleanup) completes.
    /// </summary>
    public static DesignPipeline UseVersionImage(this DesignPipeline pipeline)
    {
        return pipeline.Use(next => async ctx =>
        {
            var acquire = ctx.AcquireVersionImage
                ?? throw new InvalidOperationException("UseVersionImage requires DesignPipelineRunner.ImagePlan.");
            ctx.Progress?.SetStage("Building");
            await using var image = await acquire(ctx.Version);
            ctx.ImageTag = image.Tag;
            await next(ctx);
        });
    }

    /// <summary>
    /// Builds a container image for the version if it doesn't already exist.
    /// Eagerly removes the image in finally after inner middleware completes.
    /// </summary>
    public static DesignPipeline UseImageBuild(
        this DesignPipeline pipeline,
        string imageTagPrefix,
        string baseImage,
        Func<string, string> installScript,
        string shell = "sh")
    {
        return pipeline.Use(next => async ctx =>
        {
            var tag = $"{imageTagPrefix}:{ctx.Version}";
            ctx.ImageTag = tag;
            ctx.Progress?.SetStage("Building");

            if (!await ImageExists(ctx, tag))
            {
                ctx.Logger.LogInformation("[{V}] Building image...", ctx.Version);
                var cid = (await ctx.RunProcess([ctx.RuntimeBinary, "run", "-d", baseImage, "sleep", "infinity"])).Trim();
                try
                {
                    await ctx.RunProcess([ctx.RuntimeBinary, "exec", cid, shell, "-c", installScript(ctx.Version)]);
                    await ctx.RunProcess([ctx.RuntimeBinary, "commit", cid, tag]);
                    ctx.ActiveImages.TryAdd(tag, 0);
                }
                finally
                {
                    try { await ctx.RunProcess([ctx.RuntimeBinary, "rm", "-f", cid]); } catch { }
                }
            }

            try
            {
                await next(ctx);
            }
            finally
            {
                // Eager cleanup: image is no longer needed after scraping
                if (ctx.ImageTag is not null)
                {
                    try
                    {
                        await ctx.RunProcess([ctx.RuntimeBinary, "rmi", "-f", ctx.ImageTag]);
                        ctx.ActiveImages.TryRemove(ctx.ImageTag, out _);
                    }
                    catch { }
                }
            }
        });
    }

    /// <summary>
    /// Creates a container from the current image, sets ctx.RunHelp to exec inside it,
    /// sets ctx.HelpDumpDir for help text capture, and cleans up in finally.
    /// </summary>
    public static DesignPipeline UseContainer(this DesignPipeline pipeline)
    {
        return pipeline.Use(next => async ctx =>
        {
            ctx.Progress?.SetStage("Starting");
            ctx.Logger.LogInformation("[{V}] Starting container...", ctx.Version);
            var cid = (await ctx.RunProcess([ctx.RuntimeBinary, "run", "-d", ctx.ImageTag!, "sleep", "infinity"])).Trim();
            ctx.ContainerId = cid;
            ctx.ActiveContainers.Add(cid);
            ctx.RunHelp = async helpArgs =>
            {
                var execArgs = new List<string> { ctx.RuntimeBinary, "exec", cid };
                execArgs.AddRange(helpArgs);
                return await ctx.RunProcess(execArgs.ToArray());
            };
            ctx.HelpDumpDir = Path.Combine(ctx.OutputDir, "help", ctx.Version);
            try
            {
                await next(ctx);
            }
            finally
            {
                try { await ctx.RunProcess([ctx.RuntimeBinary, "rm", "-f", cid]); } catch { }
            }
        });
    }

    /// <summary>
    /// Reads previously-dumped help text from disk instead of running containers.
    /// Sets ctx.RunHelp to a file-reading callback. Leaves ctx.HelpDumpDir null (no dump IO).
    /// </summary>
    public static DesignPipeline UseCachedHelp(this DesignPipeline pipeline)
    {
        return pipeline.Use(next => async ctx =>
        {
            ctx.Progress?.SetStage("Loading");
            var helpDir = Path.Combine(ctx.OutputDir, "help", ctx.Version);
            ctx.RunHelp = async helpArgs =>
            {
                var commandPath = string.Join("_", helpArgs[..^1]);
                var filePath = Path.Combine(helpDir, $"{commandPath}.help.txt");
                return await File.ReadAllTextAsync(filePath);
            };
            await next(ctx);
        });
    }

    /// <summary>
    /// Scrapes help output using ctx.RunHelp (set by UseContainer or UseCachedHelp).
    /// </summary>
    public static DesignPipeline UseScraper(
        this DesignPipeline pipeline,
        string binaryName,
        Func<string, ILogger, IHelpParser> parserFactory,
        string helpFlag = "--help",
        string? outputFilePattern = null)
    {
        return pipeline.Use(next => async ctx =>
        {
            ctx.Progress?.SetStage("Scraping");

            var pattern = outputFilePattern ?? $"{binaryName}-{{version}}.json";
            var outputPath = Path.Combine(ctx.OutputDir, pattern.Replace("{version}", ctx.Version));

            var p = new ScrapePipeline()
                .Binary(binaryName)
                .UseParser(parserFactory(ctx.Version, ctx.Logger))
                .HelpFlag(helpFlag)
                .WithRunHelp(ctx.RunHelp!)
                .ScrapeParallelism(ctx.ScrapeParallelism)
                .OutputTo(outputPath);

            if (ctx.HelpDumpDir is not null)
                p.DumpHelpTo(ctx.HelpDumpDir);

            if (ctx.Progress is not null)
                p.OnCommandScraped(() => ctx.Progress.IncrementCommandsScraped());

            ctx.Result = await p.ExecuteAsync();
            await next(ctx);
        });
    }

    /// <summary>
    /// Creates a container from a base image and installs the binary inline (no image build step).
    /// Sets ctx.RunHelp and ctx.HelpDumpDir, cleans up the container in finally.
    /// </summary>
    public static DesignPipeline UseInlineContainer(
        this DesignPipeline pipeline,
        string baseImage,
        Func<string, string> installScript,
        string shell = "sh")
    {
        return pipeline.Use(next => async ctx =>
        {
            ctx.Progress?.SetStage("Installing");
            ctx.Logger.LogInformation("[{V}] Creating container with inline install...", ctx.Version);
            var cid = (await ctx.RunProcess([ctx.RuntimeBinary, "run", "-d", baseImage, "sleep", "infinity"])).Trim();
            ctx.ContainerId = cid;
            ctx.ActiveContainers.Add(cid);
            ctx.RunHelp = async helpArgs =>
            {
                var execArgs = new List<string> { ctx.RuntimeBinary, "exec", cid };
                execArgs.AddRange(helpArgs);
                return await ctx.RunProcess(execArgs.ToArray());
            };
            ctx.HelpDumpDir = Path.Combine(ctx.OutputDir, "help", ctx.Version);
            try
            {
                await ctx.RunProcess([ctx.RuntimeBinary, "exec", cid, shell, "-c", installScript(ctx.Version)]);
                await next(ctx);
            }
            finally
            {
                try { await ctx.RunProcess([ctx.RuntimeBinary, "rm", "-f", cid]); } catch { }
            }
        });
    }

    private static async Task<bool> ImageExists(VersionContext ctx, string tag)
    {
        try
        {
            await ctx.RunProcess([ctx.RuntimeBinary, "image", "inspect", tag]);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
