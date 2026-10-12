namespace FrenchExDev.Net.Vos.VosFile;

public interface IVosFileWriter
{
    Task WriteAsync(VosConfig config, string path, CancellationToken ct = default);
    Task WriteLocalAsync(VosConfig localOverrides, string configPath, CancellationToken ct = default);
}
