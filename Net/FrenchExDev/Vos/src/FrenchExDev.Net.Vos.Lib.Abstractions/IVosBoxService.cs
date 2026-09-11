namespace FrenchExDev.Net.Vos.Lib.Abstractions;

public interface IVosBoxService
{
    Task<Res.Result<VosActionResult>> ListAsync(bool boxInfo = false, CancellationToken ct = default);
    Task<Res.Result<VosActionResult>> AddAsync(string name, Options.VosBoxAddOptions? options = null, CancellationToken ct = default);
    Task<Res.Result<VosActionResult>> RemoveAsync(string name, Options.VosBoxRemoveOptions? options = null, CancellationToken ct = default);
    Task<Res.Result<IReadOnlyList<(string Name, VosActionResult Result)>>> UpdateAsync(string configPath, string instanceName, Options.VosBoxUpdateOptions? options = null, CancellationToken ct = default);
    Task<Res.Result<VosActionResult>> PruneAsync(Options.VosBoxPruneOptions? options = null, CancellationToken ct = default);
    Task<Res.Result<IReadOnlyList<(string Name, VosActionResult Result)>>> OutdatedAsync(string configPath, string instanceName, Options.VosBoxOutdatedOptions? options = null, CancellationToken ct = default);
    Task<Res.Result<VosActionResult>> RepackageAsync(string name, string provider, string version, CancellationToken ct = default);
}
