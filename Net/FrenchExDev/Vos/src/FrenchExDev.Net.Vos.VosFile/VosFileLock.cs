namespace FrenchExDev.Net.Vos.VosFile;

/// <summary>
/// File-based lock for concurrent config access safety.
/// Creates configPath.lock, holds exclusive access.
/// </summary>
public sealed class VosFileLock : IDisposable
{
    private readonly FileStream _lockStream;
    private bool _disposed;

    private VosFileLock(FileStream lockStream) => _lockStream = lockStream;

    public static VosFileLock Acquire(string configPath, TimeSpan? timeout = null)
    {
        var lockPath = configPath + ".lock";
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(5));

        while (true)
        {
            try
            {
                var stream = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                return new VosFileLock(stream);
            }
            catch (IOException) when (DateTime.UtcNow < deadline)
            {
                Thread.Sleep(100);
            }
            catch (IOException)
            {
                throw new TimeoutException($"Could not acquire lock on '{lockPath}' within {timeout?.TotalSeconds ?? 5}s. Another process may be modifying the config.");
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _lockStream.Dispose();
    }
}
