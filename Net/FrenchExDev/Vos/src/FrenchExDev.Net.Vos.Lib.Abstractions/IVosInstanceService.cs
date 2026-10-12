using FrenchExDev.Net.Vos.Config;

namespace FrenchExDev.Net.Vos.Lib.Abstractions;

public interface IVosInstanceService
{
    Task<Res.Result> AddAsync(string configPath, string machineName, string instanceName, string? ip = null, int? memory = null, int? cpus = null, bool local = false, CancellationToken ct = default);
    Task<Res.Result> RemoveAsync(string configPath, string machineName, string instanceName, bool local = false, CancellationToken ct = default);
    Task<Res.Result<IReadOnlyList<(string MachineName, VosInstance Instance)>>> ListAsync(string configPath, CancellationToken ct = default);
}
