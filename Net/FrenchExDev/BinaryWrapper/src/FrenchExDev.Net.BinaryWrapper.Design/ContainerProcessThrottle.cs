namespace FrenchExDev.Net.BinaryWrapper.Design;

/// <summary>Bounds Windows Podman clients across builds, containers and help traversal in this process.</summary>
public static class ContainerProcessThrottle
{
    // Each Windows client opens SSH connections. Per-version limits multiply during a scrape.
    private static readonly SemaphoreSlim PodmanProcesses = new(4, 4);

    public static async Task<T> RunAsync<T>(string executable, Func<Task<T>> run)
    {
        if (!OperatingSystem.IsWindows() ||
            !Path.GetFileNameWithoutExtension(executable).Equals("podman", StringComparison.OrdinalIgnoreCase))
            return await run();

        await PodmanProcesses.WaitAsync();
        try
        {
            return await run();
        }
        finally
        {
            PodmanProcesses.Release();
        }
    }
}
