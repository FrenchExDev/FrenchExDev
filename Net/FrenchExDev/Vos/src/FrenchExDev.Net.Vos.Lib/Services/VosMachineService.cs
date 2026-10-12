using FrenchExDev.Net.Injectable.Attributes;
using Microsoft.Extensions.Logging;

namespace FrenchExDev.Net.Vos.Lib.Services;

[Injectable(Scope = Scope.Transient, As = new[] { typeof(IVosMachineService) })]
public sealed class VosMachineService(
    IVosFileReader fileReader,
    IVosFileWriter fileWriter,
    IVosEventEmitter emitter,
    ILogger<VosMachineService> logger) : IVosMachineService
{
    public async Task<Res.Result> AddAsync(string configPath, string name, string machineType, int instances = 1, bool local = false, CancellationToken ct = default)
    {
        _ = logger;
        emitter.Emit(new MachineAdding(name, machineType, instances));
        var loadResult = await fileReader.ReadAsync(configPath, ct);
        if (loadResult.IsFailure) return Res.Result.Failure();

        var mgr = new VosConfigManager(loadResult.Value!);
        mgr.AddMachine(name, machineType, instances);
        await (local ? fileWriter.WriteLocalAsync(loadResult.Value!, configPath, ct) : fileWriter.WriteAsync(loadResult.Value!, configPath, ct));
        emitter.Emit(new MachineAdded(name, machineType, instances));
        return Res.Result.Success();
    }

    public async Task<Res.Result> RemoveAsync(string configPath, string name, bool local = false, CancellationToken ct = default)
    {
        emitter.Emit(new MachineRemoving(name));
        var loadResult = await fileReader.ReadAsync(configPath, ct);
        if (loadResult.IsFailure) return Res.Result.Failure();

        var mgr = new VosConfigManager(loadResult.Value!);
        mgr.RemoveMachine(name);
        await (local ? fileWriter.WriteLocalAsync(loadResult.Value!, configPath, ct) : fileWriter.WriteAsync(loadResult.Value!, configPath, ct));
        emitter.Emit(new MachineRemoved(name));
        return Res.Result.Success();
    }

    public async Task<Res.Result<IReadOnlyDictionary<string, VosMachine>>> ListAsync(string configPath, CancellationToken ct = default)
    {
        var loadResult = await fileReader.ReadAsync(configPath, ct);
        if (loadResult.IsFailure) return Res.Result<IReadOnlyDictionary<string, VosMachine>>.Failure(loadResult.ValidationResult!);
        emitter.Emit(new MachineListed(loadResult.Value!.Machines.Count));
        return Res.Result<IReadOnlyDictionary<string, VosMachine>>.Success(loadResult.Value!.Machines);
    }

    public async Task<Res.Result> EnableAsync(string configPath, string name, bool local = false, CancellationToken ct = default)
    {
        emitter.Emit(new MachineEnabling(name));
        var loadResult = await fileReader.ReadAsync(configPath, ct);
        if (loadResult.IsFailure) return Res.Result.Failure();

        var mgr = new VosConfigManager(loadResult.Value!);
        mgr.EnableMachine(name);
        await (local ? fileWriter.WriteLocalAsync(loadResult.Value!, configPath, ct) : fileWriter.WriteAsync(loadResult.Value!, configPath, ct));
        emitter.Emit(new MachineEnabled(name));
        return Res.Result.Success();
    }

    public async Task<Res.Result> DisableAsync(string configPath, string name, bool local = false, CancellationToken ct = default)
    {
        emitter.Emit(new MachineDisabling(name));
        var loadResult = await fileReader.ReadAsync(configPath, ct);
        if (loadResult.IsFailure) return Res.Result.Failure();

        var mgr = new VosConfigManager(loadResult.Value!);
        mgr.DisableMachine(name);
        await (local ? fileWriter.WriteLocalAsync(loadResult.Value!, configPath, ct) : fileWriter.WriteAsync(loadResult.Value!, configPath, ct));
        emitter.Emit(new MachineDisabled(name));
        return Res.Result.Success();
    }
}
