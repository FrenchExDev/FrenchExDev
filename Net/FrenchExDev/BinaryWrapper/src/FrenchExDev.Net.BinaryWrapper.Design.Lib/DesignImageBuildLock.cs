using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;

namespace FrenchExDev.Net.BinaryWrapper.Design.Lib;

/// <summary>Coordinates image builders in separate processes belonging to the same local user.</summary>
internal static class DesignImageBuildLock
{
    public static async Task<FileStream> AcquireAsync(string tag, ILogger logger)
    {
        var path = LockPath(tag);
        var reportedWait = false;

        while (true)
        {
            try
            {
                // Keep the file: unlinking it could let Unix callers lock different inodes.
                // The OS releases the exclusive handle even if the owner process is killed.
                return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException ex) when (IsSharingViolation(ex))
            {
                if (!reportedWait)
                {
                    logger.LogInformation("Waiting for image build lock: {Tag}", tag);
                    reportedWait = true;
                }
                await Task.Delay(200);
            }
        }
    }

    // Call while holding the build lock, so a new reader cannot race image removal.
    public static FileStream AcquireUsage(string tag) =>
        new(UsagePath(tag), FileMode.Open, FileAccess.Read, FileShare.Read);

    public static FileStream? TryAcquireUnused(string tag)
    {
        try { return new FileStream(UsagePath(tag), FileMode.Open, FileAccess.Read, FileShare.None); }
        catch (IOException ex) when (IsSharingViolation(ex)) { return null; }
    }

    private static string UsagePath(string tag)
    {
        var path = LockPath(tag) + ".usage";
        if (!File.Exists(path)) File.WriteAllBytes(path, []);
        return path;
    }

    private static string LockPath(string tag)
    {
        // Output directories and checkouts may differ while the container engine is shared.
        var localData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.DoNotVerify);
        if (string.IsNullOrEmpty(localData))
            throw new InvalidOperationException("A local application-data directory is required for image build locks.");
        var directory = Path.Combine(localData, "FrenchExDev", "BinaryWrapper", "image-locks");
        Directory.CreateDirectory(directory);
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(tag))).ToLowerInvariant();
        return Path.Combine(directory, key + ".lock");
    }

    private static bool IsSharingViolation(IOException exception) => OperatingSystem.IsWindows()
        ? exception.HResult == unchecked((int)0x80070020) // ERROR_SHARING_VIOLATION
        : exception.HResult is 11 or 35; // EWOULDBLOCK on Linux and macOS/BSD.
}
