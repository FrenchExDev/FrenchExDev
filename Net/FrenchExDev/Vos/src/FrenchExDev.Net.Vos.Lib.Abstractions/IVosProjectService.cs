using FrenchExDev.Net.Vos.Config;

namespace FrenchExDev.Net.Vos.Lib.Abstractions;

public interface IVosProjectService
{
    Task<Res.Result<VosConfig>> InitAsync(string outputDir, CancellationToken ct = default);
    Task<Res.Result<IReadOnlyList<(string MachineName, ResolvedInstance Instance)>>> ShowConfigAsync(string configPath, CancellationToken ct = default);
    Task<Res.Result<IReadOnlyList<(string MachineName, ResolvedInstance Instance)>>> ValidateAsync(string configPath, CancellationToken ct = default);
    Task<Res.Result<IReadOnlyList<(string MachineName, ResolvedInstance Instance)>>> ResolveAsync(string configPath, string? instanceName = null, bool all = true, CancellationToken ct = default);
    string GetVersion();
    Task<Res.Result> CreateProvisioningScriptAsync(string configPath, string key, string? version = null, string? extension = null, string? templatePath = null, CancellationToken ct = default);
    Task<Res.Result<IReadOnlyList<string>>> ValidateProvisioningScriptsAsync(string configPath, CancellationToken ct = default);
}
