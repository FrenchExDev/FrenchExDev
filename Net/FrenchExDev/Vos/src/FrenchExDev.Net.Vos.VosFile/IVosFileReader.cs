namespace FrenchExDev.Net.Vos.VosFile;

public interface IVosFileReader
{
    Task<Res.Result<VosConfig>> ReadAsync(string configPath, CancellationToken ct = default);
    Task<Res.Result<VosConfigLayers>> ReadLayersAsync(string configPath, CancellationToken ct = default);
}

public sealed record VosConfigLayers(
    VosConfig Base,
    VosConfig? Local,
    VosConfig Resolved);
