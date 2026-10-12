using FrenchExDev.Net.Vos.Config;

namespace FrenchExDev.Net.Vos.Lib.Abstractions;

public interface IVosMachineService
{
    Task<Res.Result> AddAsync(string configPath, string name, string machineType, int instances = 1, bool local = false, CancellationToken ct = default);
    Task<Res.Result> RemoveAsync(string configPath, string name, bool local = false, CancellationToken ct = default);
    Task<Res.Result<IReadOnlyDictionary<string, VosMachine>>> ListAsync(string configPath, CancellationToken ct = default);
    Task<Res.Result> EnableAsync(string configPath, string name, bool local = false, CancellationToken ct = default);
    Task<Res.Result> DisableAsync(string configPath, string name, bool local = false, CancellationToken ct = default);
}
