#if NET10_0_OR_GREATER
using System.Runtime.InteropServices;

namespace FrenchExDev.Net.Packer.Bundle;

/// <summary>
/// Manages the hcl2json binary — checks for availability, downloads if needed, caches locally.
/// </summary>
public static class Hcl2JsonTool
{
    private const string Version = "0.6.8";
    private const string Repo = "tmccombs/hcl2json";

    private static readonly string CacheDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".frenchexdev", "tools", "hcl2json");

    /// <summary>
    /// Returns the path to the hcl2json binary, downloading it if not cached.
    /// </summary>
    public static async Task<string> EnsureAvailableAsync(CancellationToken ct = default)
    {
        var binaryName = GetBinaryName();
        var cachedPath = Path.Combine(CacheDir, binaryName);

        if (File.Exists(cachedPath))
            return cachedPath;

        // Try PATH first
        var onPath = FindOnPath(binaryName);
        if (onPath is not null)
            return onPath;

        // Download
        Directory.CreateDirectory(CacheDir);
        var url = GetDownloadUrl();

        using var http = new HttpClient();
        http.DefaultRequestHeaders.UserAgent.ParseAdd("FrenchExDev-Hcl2JsonTool/1.0");

        Console.WriteLine($"Downloading hcl2json v{Version} from {url}...");
        var bytes = await http.GetByteArrayAsync(url, ct);
        await File.WriteAllBytesAsync(cachedPath, bytes, ct);

        // Make executable on Unix
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var chmod = System.Diagnostics.Process.Start("chmod", $"+x \"{cachedPath}\"");
            chmod?.WaitForExit();
        }

        Console.WriteLine($"Cached: {cachedPath}");
        return cachedPath;
    }

    /// <summary>Checks if hcl2json is available (cached or on PATH).</summary>
    public static bool IsAvailable()
    {
        var binaryName = GetBinaryName();
        return File.Exists(Path.Combine(CacheDir, binaryName)) || FindOnPath(binaryName) is not null;
    }

    private static string GetBinaryName()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return "hcl2json.exe";
        return "hcl2json";
    }

    private static string GetDownloadUrl()
    {
        var os = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "windows"
            : RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "darwin"
            : "linux";

        var arch = RuntimeInformation.OSArchitecture switch
        {
            Architecture.X64 => "amd64",
            Architecture.Arm64 => "arm64",
            _ => "amd64"
        };

        var ext = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".exe" : "";
        return $"https://github.com/{Repo}/releases/download/v{Version}/hcl2json_{os}_{arch}{ext}";
    }

    private static string? FindOnPath(string binaryName)
    {
        var pathVar = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in pathVar.Split(Path.PathSeparator))
        {
            var fullPath = Path.Combine(dir, binaryName);
            if (File.Exists(fullPath))
                return fullPath;
        }
        return null;
    }
}
#endif
