using FrenchExDev.Net.Injectable.Attributes;
using Microsoft.Extensions.Logging;

namespace FrenchExDev.Net.Vos.Lib.Services;

[Injectable(Scope = Scope.Transient, As = new[] { typeof(IVosInstanceService) })]
public sealed class VosInstanceService(
    IVosFileReader fileReader,
    IVosFileWriter fileWriter,
    IVosEventEmitter emitter,
    ILogger<VosInstanceService> logger) : IVosInstanceService
{
    public async Task<Res.Result> AddAsync(string configPath, string machineName, string instanceName, string? ip = null, int? memory = null, int? cpus = null, bool local = false, CancellationToken ct = default)
    {
        _ = logger;
        emitter.Emit(new InstanceAdding(machineName, instanceName));
        var loadResult = await fileReader.ReadAsync(configPath, ct);
        if (loadResult.IsFailure) return Res.Result.Failure();

        var mgr = new VosConfigManager(loadResult.Value!);
        mgr.AddInstance(machineName, instanceName, ip, memory, cpus);
        await (local ? fileWriter.WriteLocalAsync(loadResult.Value!, configPath, ct) : fileWriter.WriteAsync(loadResult.Value!, configPath, ct));
        emitter.Emit(new InstanceAdded(machineName, instanceName));
        return Res.Result.Success();
    }

    public async Task<Res.Result> RemoveAsync(string configPath, string machineName, string instanceName, bool local = false, CancellationToken ct = default)
    {
        emitter.Emit(new InstanceRemoving(machineName, instanceName));
        var loadResult = await fileReader.ReadAsync(configPath, ct);
        if (loadResult.IsFailure) return Res.Result.Failure();

        var mgr = new VosConfigManager(loadResult.Value!);
        mgr.RemoveInstance(machineName, instanceName);
        await (local ? fileWriter.WriteLocalAsync(loadResult.Value!, configPath, ct) : fileWriter.WriteAsync(loadResult.Value!, configPath, ct));
        emitter.Emit(new InstanceRemoved(machineName, instanceName));
        return Res.Result.Success();
    }

    public async Task<Res.Result<IReadOnlyList<(string MachineName, VosInstance Instance)>>> ListAsync(string configPath, CancellationToken ct = default)
    {
        var loadResult = await fileReader.ReadAsync(configPath, ct);
        if (loadResult.IsFailure) return Res.Result<IReadOnlyList<(string, VosInstance)>>.Failure(loadResult.ValidationResult!);

        var result = new List<(string, VosInstance)>();
        foreach (var (name, m) in loadResult.Value!.Machines)
            foreach (var inst in m.Instances)
                result.Add((name, inst));

        emitter.Emit(new InstanceListed(result.Count));
        return Res.Result<IReadOnlyList<(string MachineName, VosInstance Instance)>>.Success(result);
    }
}
