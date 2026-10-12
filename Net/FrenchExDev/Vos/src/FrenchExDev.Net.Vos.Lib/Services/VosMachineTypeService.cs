using FrenchExDev.Net.Injectable.Attributes;
using Microsoft.Extensions.Logging;

namespace FrenchExDev.Net.Vos.Lib.Services;

[Injectable(Scope = Scope.Transient, As = new[] { typeof(IVosMachineTypeService) })]
public sealed class VosMachineTypeService(
    IVosFileReader fileReader,
    IVosFileWriter fileWriter,
    IVosEventEmitter emitter,
    ILogger<VosMachineTypeService> logger) : IVosMachineTypeService
{
    public async Task<Res.Result> AddAsync(string configPath, string name, string box, int memory = 2048, int cpus = 2, bool local = false, CancellationToken ct = default)
    {
        emitter.Emit(new MachineTypeAdding(name, box));
        var loadResult = await fileReader.ReadAsync(configPath, ct);
        if (loadResult.IsFailure) return Res.Result.Failure();

        var mgr = new VosConfigManager(loadResult.Value!);
        mgr.AddMachineType(name, box, memory, cpus);

        await (local ? fileWriter.WriteLocalAsync(loadResult.Value!, configPath, ct) : fileWriter.WriteAsync(loadResult.Value!, configPath, ct));
        emitter.Emit(new MachineTypeAdded(name, box));
        logger.LogInformation("Added machine type '{Name}' with box '{Box}'", name, box);
        return Res.Result.Success();
    }

    public async Task<Res.Result> RemoveAsync(string configPath, string name, bool local = false, CancellationToken ct = default)
    {
        emitter.Emit(new MachineTypeRemoving(name));
        var loadResult = await fileReader.ReadAsync(configPath, ct);
        if (loadResult.IsFailure) return Res.Result.Failure();

        var mgr = new VosConfigManager(loadResult.Value!);
        mgr.RemoveMachineType(name);

        await (local ? fileWriter.WriteLocalAsync(loadResult.Value!, configPath, ct) : fileWriter.WriteAsync(loadResult.Value!, configPath, ct));
        emitter.Emit(new MachineTypeRemoved(name));
        return Res.Result.Success();
    }

    public async Task<Res.Result<IReadOnlyDictionary<string, VosMachineType>>> ListAsync(string configPath, CancellationToken ct = default)
    {
        var loadResult = await fileReader.ReadAsync(configPath, ct);
        if (loadResult.IsFailure) return Res.Result<IReadOnlyDictionary<string, VosMachineType>>.Failure(loadResult.ValidationResult!);

        emitter.Emit(new MachineTypeListed(loadResult.Value!.MachineTypes.Count));
        return Res.Result<IReadOnlyDictionary<string, VosMachineType>>.Success(loadResult.Value!.MachineTypes);
    }

    public async Task<Res.Result<VosMachineType>> ShowAsync(string configPath, string name, CancellationToken ct = default)
    {
        var loadResult = await fileReader.ReadAsync(configPath, ct);
        if (loadResult.IsFailure) return Res.Result<VosMachineType>.Failure(loadResult.ValidationResult!);

        if (!loadResult.Value!.MachineTypes.TryGetValue(name, out var mt))
            return Res.Result<VosMachineType>.Failure(new System.ComponentModel.DataAnnotations.ValidationResult($"Machine type '{name}' not found."));

        emitter.Emit(new MachineTypeShown(name));
        return Res.Result<VosMachineType>.Success(mt);
    }

    public async Task<Res.Result> SetAsync(string configPath, string name, Abstractions.Options.MachineTypeSettings settings, bool local = false, CancellationToken ct = default)
    {
        emitter.Emit(new MachineTypeUpdating(name));
        var loadResult = await fileReader.ReadAsync(configPath, ct);
        if (loadResult.IsFailure) return Res.Result.Failure();

        var mgr = new VosConfigManager(loadResult.Value!);
        mgr.SetMachineTypeProperty(name, mt =>
        {
            mt.Provider ??= new VosProviderConfig();
            if (settings.Memory is { } mem) mt.Provider.Memory = mem;
            if (settings.Cpus is { } cpu) mt.Provider.Cpus = cpu;
            if (settings.VideoMemory is { } vid) mt.Provider.VideoMemory = vid;
            if (settings.Gui == true) mt.Provider.Gui = true;
            if (settings.NoLinkedClones == true) mt.Provider.LinkedClones = false;
            if (settings.NestedVirt == true)
            {
                mt.Provider.VboxManage.Add(["modifyvm", "{{ .Name }}", "--nested-hw-virt", "on"]);
                mt.Provider.VboxManage.Add(["modifyvm", "{{ .Name }}", "--nestedpaging", "on"]);
            }
            if (settings.SataSsd == true)
            {
                mt.Provider.VboxManage.Add(["storagectl", "{{ .Name }}", "--name=SATA Controller", "--hostiocache", "on"]);
                mt.Provider.VboxManage.Add(["storageattach", "{{ .Name }}", "--storagectl=SATA Controller", "--port=0", "--nonrotational", "on"]);
            }
            if (settings.NicPromisc is { } promisc)
                mt.Provider.VboxManage.Add(["modifyvm", "{{ .Name }}", "--nicpromisc2", promisc]);
        });

        await (local ? fileWriter.WriteLocalAsync(loadResult.Value!, configPath, ct) : fileWriter.WriteAsync(loadResult.Value!, configPath, ct));
        emitter.Emit(new MachineTypeUpdated(name));
        return Res.Result.Success();
    }

    // VBoxManage
    public async Task<Res.Result> AddVboxManageAsync(string configPath, string name, IReadOnlyList<string> args, bool local = false, CancellationToken ct = default)
    {
        emitter.Emit(new VboxManageCommandAdding(name, args));
        var loadResult = await fileReader.ReadAsync(configPath, ct);
        if (loadResult.IsFailure) return Res.Result.Failure();

        var mgr = new VosConfigManager(loadResult.Value!);
        mgr.AddVboxManageCommand(name, args.ToList());
        await (local ? fileWriter.WriteLocalAsync(loadResult.Value!, configPath, ct) : fileWriter.WriteAsync(loadResult.Value!, configPath, ct));
        emitter.Emit(new VboxManageCommandAdded(name));
        return Res.Result.Success();
    }

    public async Task<Res.Result<IReadOnlyList<List<string>>>> ListVboxManageAsync(string configPath, string name, CancellationToken ct = default)
    {
        var loadResult = await fileReader.ReadAsync(configPath, ct);
        if (loadResult.IsFailure) return Res.Result<IReadOnlyList<List<string>>>.Failure(loadResult.ValidationResult!);

        var mgr = new VosConfigManager(loadResult.Value!);
        var cmds = mgr.ListVboxManageCommands(name);
        emitter.Emit(new VboxManageCommandsListed(name, cmds.Count));
        return Res.Result<IReadOnlyList<List<string>>>.Success(cmds);
    }

    public async Task<Res.Result> RemoveVboxManageAsync(string configPath, string name, int index, bool local = false, CancellationToken ct = default)
    {
        emitter.Emit(new VboxManageCommandRemoving(name, index));
        var loadResult = await fileReader.ReadAsync(configPath, ct);
        if (loadResult.IsFailure) return Res.Result.Failure();

        var mgr = new VosConfigManager(loadResult.Value!);
        mgr.RemoveVboxManageCommand(name, index);
        await (local ? fileWriter.WriteLocalAsync(loadResult.Value!, configPath, ct) : fileWriter.WriteAsync(loadResult.Value!, configPath, ct));
        emitter.Emit(new VboxManageCommandRemoved(name, index));
        return Res.Result.Success();
    }

    public async Task<Res.Result> ClearVboxManageAsync(string configPath, string name, bool local = false, CancellationToken ct = default)
    {
        emitter.Emit(new VboxManageCommandsClearing(name));
        var loadResult = await fileReader.ReadAsync(configPath, ct);
        if (loadResult.IsFailure) return Res.Result.Failure();

        var mgr = new VosConfigManager(loadResult.Value!);
        mgr.ClearVboxManageCommands(name);
        await (local ? fileWriter.WriteLocalAsync(loadResult.Value!, configPath, ct) : fileWriter.WriteAsync(loadResult.Value!, configPath, ct));
        emitter.Emit(new VboxManageCommandsCleared(name));
        return Res.Result.Success();
    }

    // Provisioning — delegate to VosConfigManager + emit events
    public async Task<Res.Result> AddProvisioningStepAsync(string configPath, string typeName, string key, Abstractions.Options.VosProvisioningStepOptions? options = null, bool local = false, CancellationToken ct = default)
    {
        emitter.Emit(new ProvisioningStepAdding(typeName, key));
        var loadResult = await fileReader.ReadAsync(configPath, ct);
        if (loadResult.IsFailure) return Res.Result.Failure();

        var mgr = new VosConfigManager(loadResult.Value!);
        mgr.AddProvisioningStep(typeName, key, options?.Version, options?.Privileged ?? true);
        await (local ? fileWriter.WriteLocalAsync(loadResult.Value!, configPath, ct) : fileWriter.WriteAsync(loadResult.Value!, configPath, ct));
        emitter.Emit(new ProvisioningStepAdded(typeName, key));
        return Res.Result.Success();
    }

    public async Task<Res.Result> RemoveProvisioningStepAsync(string configPath, string typeName, string key, bool local = false, CancellationToken ct = default)
    {
        emitter.Emit(new ProvisioningStepRemoving(typeName, key));
        var loadResult = await fileReader.ReadAsync(configPath, ct);
        if (loadResult.IsFailure) return Res.Result.Failure();

        if (!loadResult.Value!.MachineTypes.TryGetValue(typeName, out var mt))
            return Res.Result.Failure();
        mt.Provisioning.RemoveAll(s => s.Key == key);

        await (local ? fileWriter.WriteLocalAsync(loadResult.Value!, configPath, ct) : fileWriter.WriteAsync(loadResult.Value!, configPath, ct));
        emitter.Emit(new ProvisioningStepRemoved(typeName, key));
        return Res.Result.Success();
    }

    public async Task<Res.Result<IReadOnlyList<VosProvisioningStep>>> ListProvisioningStepsAsync(string configPath, string typeName, CancellationToken ct = default)
    {
        var loadResult = await fileReader.ReadAsync(configPath, ct);
        if (loadResult.IsFailure) return Res.Result<IReadOnlyList<VosProvisioningStep>>.Failure(loadResult.ValidationResult!);

        if (!loadResult.Value!.MachineTypes.TryGetValue(typeName, out var mt))
            return Res.Result<IReadOnlyList<VosProvisioningStep>>.Failure(new System.ComponentModel.DataAnnotations.ValidationResult($"Machine type '{typeName}' not found."));

        emitter.Emit(new ProvisioningStepsListed(typeName, mt.Provisioning.Count));
        return Res.Result<IReadOnlyList<VosProvisioningStep>>.Success(mt.Provisioning);
    }

    public async Task<Res.Result> EnableProvisioningStepAsync(string configPath, string typeName, string key, bool local = false, CancellationToken ct = default)
    {
        emitter.Emit(new ProvisioningStepEnabling(typeName, key));
        var loadResult = await fileReader.ReadAsync(configPath, ct);
        if (loadResult.IsFailure) return Res.Result.Failure();

        if (loadResult.Value!.MachineTypes.TryGetValue(typeName, out var mt))
        {
            var step = mt.Provisioning.FirstOrDefault(s => s.Key == key);
            if (step is not null) step.Enabled = true;
        }
        await (local ? fileWriter.WriteLocalAsync(loadResult.Value!, configPath, ct) : fileWriter.WriteAsync(loadResult.Value!, configPath, ct));
        emitter.Emit(new ProvisioningStepEnabled(typeName, key));
        return Res.Result.Success();
    }

    public async Task<Res.Result> DisableProvisioningStepAsync(string configPath, string typeName, string key, bool local = false, CancellationToken ct = default)
    {
        emitter.Emit(new ProvisioningStepDisabling(typeName, key));
        var loadResult = await fileReader.ReadAsync(configPath, ct);
        if (loadResult.IsFailure) return Res.Result.Failure();

        if (loadResult.Value!.MachineTypes.TryGetValue(typeName, out var mt))
        {
            var step = mt.Provisioning.FirstOrDefault(s => s.Key == key);
            if (step is not null) step.Enabled = false;
        }
        await (local ? fileWriter.WriteLocalAsync(loadResult.Value!, configPath, ct) : fileWriter.WriteAsync(loadResult.Value!, configPath, ct));
        emitter.Emit(new ProvisioningStepDisabled(typeName, key));
        return Res.Result.Success();
    }

    public async Task<Res.Result> MoveProvisioningStepAsync(string configPath, string typeName, string key, string? before = null, string? after = null, bool local = false, CancellationToken ct = default)
    {
        emitter.Emit(new ProvisioningStepMoving(typeName, key));
        var loadResult = await fileReader.ReadAsync(configPath, ct);
        if (loadResult.IsFailure) return Res.Result.Failure();

        if (loadResult.Value!.MachineTypes.TryGetValue(typeName, out var mt))
        {
            var step = mt.Provisioning.FirstOrDefault(s => s.Key == key);
            if (step is not null)
            {
                mt.Provisioning.Remove(step);
                var targetIdx = before is not null
                    ? mt.Provisioning.FindIndex(s => s.Key == before)
                    : after is not null
                        ? mt.Provisioning.FindIndex(s => s.Key == after) + 1
                        : mt.Provisioning.Count;
                if (targetIdx < 0) targetIdx = mt.Provisioning.Count;
                mt.Provisioning.Insert(targetIdx, step);
            }
        }
        await (local ? fileWriter.WriteLocalAsync(loadResult.Value!, configPath, ct) : fileWriter.WriteAsync(loadResult.Value!, configPath, ct));
        emitter.Emit(new ProvisioningStepMoved(typeName, key));
        return Res.Result.Success();
    }

    // Shared folders
    public async Task<Res.Result> AddSharedFolderAsync(string configPath, string typeName, string hostPath, string guestPath, string? sfType = null, bool disabled = false, bool local = false, CancellationToken ct = default)
    {
        emitter.Emit(new SharedFolderAdding(typeName, hostPath, guestPath));
        var loadResult = await fileReader.ReadAsync(configPath, ct);
        if (loadResult.IsFailure) return Res.Result.Failure();

        var mgr = new VosConfigManager(loadResult.Value!);
        mgr.AddSharedFolder(typeName, hostPath, guestPath, sfType);
        await (local ? fileWriter.WriteLocalAsync(loadResult.Value!, configPath, ct) : fileWriter.WriteAsync(loadResult.Value!, configPath, ct));
        emitter.Emit(new SharedFolderAdded(typeName, hostPath, guestPath));
        return Res.Result.Success();
    }

    public async Task<Res.Result> RemoveSharedFolderAsync(string configPath, string typeName, int index, bool local = false, CancellationToken ct = default)
    {
        emitter.Emit(new SharedFolderRemoving(typeName, index));
        var loadResult = await fileReader.ReadAsync(configPath, ct);
        if (loadResult.IsFailure) return Res.Result.Failure();

        if (loadResult.Value!.MachineTypes.TryGetValue(typeName, out var mt) && index >= 0 && index < mt.SharedFolders.Count)
            mt.SharedFolders.RemoveAt(index);

        await (local ? fileWriter.WriteLocalAsync(loadResult.Value!, configPath, ct) : fileWriter.WriteAsync(loadResult.Value!, configPath, ct));
        emitter.Emit(new SharedFolderRemoved(typeName, index));
        return Res.Result.Success();
    }

    public async Task<Res.Result<IReadOnlyList<VosSharedFolder>>> ListSharedFoldersAsync(string configPath, string typeName, CancellationToken ct = default)
    {
        var loadResult = await fileReader.ReadAsync(configPath, ct);
        if (loadResult.IsFailure) return Res.Result<IReadOnlyList<VosSharedFolder>>.Failure(loadResult.ValidationResult!);

        if (!loadResult.Value!.MachineTypes.TryGetValue(typeName, out var mt))
            return Res.Result<IReadOnlyList<VosSharedFolder>>.Failure(new System.ComponentModel.DataAnnotations.ValidationResult($"Machine type '{typeName}' not found."));

        emitter.Emit(new SharedFoldersListed(typeName, mt.SharedFolders.Count));
        return Res.Result<IReadOnlyList<VosSharedFolder>>.Success(mt.SharedFolders);
    }

    // Plugins
    public async Task<Res.Result> AddPluginAsync(string configPath, string typeName, string name, bool local = false, CancellationToken ct = default)
    {
        emitter.Emit(new PluginAdding(typeName, name));
        var loadResult = await fileReader.ReadAsync(configPath, ct);
        if (loadResult.IsFailure) return Res.Result.Failure();

        var mgr = new VosConfigManager(loadResult.Value!);
        mgr.AddPlugin(typeName, name);
        await (local ? fileWriter.WriteLocalAsync(loadResult.Value!, configPath, ct) : fileWriter.WriteAsync(loadResult.Value!, configPath, ct));
        emitter.Emit(new PluginAdded(typeName, name));
        return Res.Result.Success();
    }

    public async Task<Res.Result> RemovePluginAsync(string configPath, string typeName, string name, bool local = false, CancellationToken ct = default)
    {
        emitter.Emit(new PluginRemoving(typeName, name));
        var loadResult = await fileReader.ReadAsync(configPath, ct);
        if (loadResult.IsFailure) return Res.Result.Failure();

        if (loadResult.Value!.MachineTypes.TryGetValue(typeName, out var mt))
            mt.Plugins.Remove(name);

        await (local ? fileWriter.WriteLocalAsync(loadResult.Value!, configPath, ct) : fileWriter.WriteAsync(loadResult.Value!, configPath, ct));
        emitter.Emit(new PluginRemoved(typeName, name));
        return Res.Result.Success();
    }

    public async Task<Res.Result<IReadOnlyList<string>>> ListPluginsAsync(string configPath, string typeName, CancellationToken ct = default)
    {
        var loadResult = await fileReader.ReadAsync(configPath, ct);
        if (loadResult.IsFailure) return Res.Result<IReadOnlyList<string>>.Failure(loadResult.ValidationResult!);

        if (!loadResult.Value!.MachineTypes.TryGetValue(typeName, out var mt))
            return Res.Result<IReadOnlyList<string>>.Failure(new System.ComponentModel.DataAnnotations.ValidationResult($"Machine type '{typeName}' not found."));

        emitter.Emit(new PluginsListed(typeName, mt.Plugins.Count));
        return Res.Result<IReadOnlyList<string>>.Success(mt.Plugins);
    }
}
