using FrenchExDev.Net.Vos.Config;

namespace FrenchExDev.Net.Vos.Abstractions;

/// <summary>
/// In-memory config cache with file watcher invalidation.
/// Used by completers (hot path) and read-only services.
/// </summary>
public interface IVosConfigCache
{
    Task<Res.Result<VosConfig>> GetOrLoadAsync(string configPath, CancellationToken ct = default);
    void Invalidate(string configPath);
    void InvalidateAll();
}
