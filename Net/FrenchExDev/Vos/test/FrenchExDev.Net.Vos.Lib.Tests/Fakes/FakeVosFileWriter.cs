using FrenchExDev.Net.Vos.VosFile;

namespace FrenchExDev.Net.Vos.Lib.Tests.Fakes;

public sealed class FakeVosFileWriter : IVosFileWriter
{
    public Dictionary<string, VosConfig> Written { get; } = new();
    public Dictionary<string, VosConfig> LocalWritten { get; } = new();

    public Task WriteAsync(VosConfig config, string path, CancellationToken ct = default)
    {
        Written[path] = config;
        return Task.CompletedTask;
    }

    public Task WriteLocalAsync(VosConfig localOverrides, string configPath, CancellationToken ct = default)
    {
        LocalWritten[configPath] = localOverrides;
        return Task.CompletedTask;
    }
}
