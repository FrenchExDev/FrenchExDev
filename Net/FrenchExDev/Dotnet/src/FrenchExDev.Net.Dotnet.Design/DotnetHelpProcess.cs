using System.Diagnostics;
using System.Text.RegularExpressions;
using FrenchExDev.Net.BinaryWrapper.Design;

namespace FrenchExDev.Net.Dotnet.Design;

/// <summary>Some bundled tools print valid help on stderr or exit 1 for --help.</summary>
public static class DotnetHelpProcess
{
    public static Task<string> RunAsync(string runtime, string containerId, string[] command) =>
        RunAsync(runtime,
            () => ContainerProcessThrottle.RunAsync(runtime, () => RunOnceAsync(runtime, containerId, command)),
            delay => Task.Delay(delay));

    internal static async Task<string> RunAsync(string runtime,
        Func<Task<(string Stdout, string Stderr, int ExitCode)>> run,
        Func<TimeSpan, Task> delay)
    {
        for (var attempt = 0; ; attempt++)
        {
            var (stdout, stderr, exitCode) = await run();
            if (attempt < 2 && exitCode == 125 &&
                Path.GetFileNameWithoutExtension(runtime).Equals("podman", StringComparison.OrdinalIgnoreCase) &&
                (stderr.Contains("Cannot connect to Podman", StringComparison.OrdinalIgnoreCase) ||
                 stderr.Contains("unable to connect to Podman socket", StringComparison.OrdinalIgnoreCase) ||
                 stderr.Contains("ssh: handshake failed", StringComparison.OrdinalIgnoreCase)))
            {
                // Release the process slot before backing off. Only read-only help is retried.
                await delay(TimeSpan.FromSeconds(1 << attempt));
                continue;
            }
            return SelectHelp(stdout, stderr, exitCode);
        }
    }

    private static async Task<(string Stdout, string Stderr, int ExitCode)> RunOnceAsync(
        string runtime, string containerId, string[] command)
    {
        var start = new ProcessStartInfo(runtime)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        foreach (var argument in new[] { "exec", containerId, "env" }.Concat(DotnetSdk.EnvironmentVariables).Concat(command))
            start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new InvalidOperationException($"Cannot start {runtime}.");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException($"Help timed out: {string.Join(' ', command)}");
        }
        return (await stdout, await stderr, process.ExitCode);
    }

    public static string SelectHelp(string stdout, string stderr, int exitCode)
    {
        // Runtime diagnostics may themselves include a Usage section; never cache them as SDK help.
        if (exitCode == 125)
            throw new ContainerRuntimeException($"Container runtime failed (exit {exitCode}): {stderr}");
        var help = string.IsNullOrWhiteSpace(stdout) ? stderr : stdout;
        // Prefer whichever stream actually contains the usage, even with exit code 1.
        if (!HasUsage(help) && HasUsage(stderr)) help = stderr;
        if (string.IsNullOrWhiteSpace(help) || (exitCode != 0 && !HasUsage(help)))
            throw new InvalidOperationException($"dotnet help failed (exit {exitCode}): {stderr}");
        return help;
    }

    private static bool HasUsage(string text) =>
        Regex.IsMatch(text, @"(?im)^\s*(?:usage|utilisation|syntax)\s*:");
}
