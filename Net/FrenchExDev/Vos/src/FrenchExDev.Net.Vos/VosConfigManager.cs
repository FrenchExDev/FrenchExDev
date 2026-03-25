using FrenchExDev.Net.Vos.Config;

namespace FrenchExDev.Net.Vos;

/// <summary>
/// CRUD operations on <see cref="VosConfig"/>.
/// All operations are in-memory — call <see cref="IVosConfigSerializer.SerializeAsync"/> to persist.
/// </summary>
public sealed class VosConfigManager
{
    private readonly VosConfig _config;

    public VosConfigManager(VosConfig config)
    {
        _config = config;
    }

    public VosConfig Config => _config;

    // ── Machine Type CRUD ───────────────────────────────────────────

    public void AddMachineType(string name, string box, int memory = 2048, int cpus = 2, int videoMemory = 64)
    {
        if (_config.MachineTypes.ContainsKey(name))
            throw new InvalidOperationException($"Machine type '{name}' already exists.");

        _config.MachineTypes[name] = new VosMachineType
        {
            Box = box,
            Provider = new VosProviderConfig { Memory = memory, Cpus = cpus, VideoMemory = videoMemory }
        };
    }

    public void RemoveMachineType(string name)
    {
        if (!_config.MachineTypes.Remove(name))
            throw new InvalidOperationException($"Machine type '{name}' not found.");

        // Check if any machines reference this type
        var referencing = _config.Machines
            .Where(m => m.Value.MachineTypeName == name)
            .Select(m => m.Key)
            .ToList();

        if (referencing.Count > 0)
            throw new InvalidOperationException(
                $"Cannot remove machine type '{name}': referenced by machines: {string.Join(", ", referencing)}");
    }

    public void SetMachineTypeProperty(string name, Action<VosMachineType> configure)
    {
        if (!_config.MachineTypes.TryGetValue(name, out var machineType))
            throw new InvalidOperationException($"Machine type '{name}' not found.");

        configure(machineType);
    }

    // ── Machine CRUD ────────────────────────────────────────────────

    public void AddMachine(string name, string machineTypeName, int instanceCount = 1)
    {
        if (_config.Machines.ContainsKey(name))
            throw new InvalidOperationException($"Machine '{name}' already exists.");

        if (!_config.MachineTypes.ContainsKey(machineTypeName))
            throw new InvalidOperationException($"Machine type '{machineTypeName}' not found.");

        var machine = new VosMachine
        {
            MachineTypeName = machineTypeName,
            Instances = new List<VosInstance>()
        };

        for (var i = 1; i <= instanceCount; i++)
        {
            machine.Instances.Add(new VosInstance
            {
                Name = $"{name}-{i:D2}"
            });
        }

        _config.Machines[name] = machine;
    }

    public void RemoveMachine(string name)
    {
        if (!_config.Machines.Remove(name))
            throw new InvalidOperationException($"Machine '{name}' not found.");
    }

    public void EnableMachine(string name)
    {
        if (!_config.Machines.TryGetValue(name, out var machine))
            throw new InvalidOperationException($"Machine '{name}' not found.");
        machine.IsEnabled = true;
    }

    public void DisableMachine(string name)
    {
        if (!_config.Machines.TryGetValue(name, out var machine))
            throw new InvalidOperationException($"Machine '{name}' not found.");
        machine.IsEnabled = false;
    }

    // ── Instance CRUD ───────────────────────────────────────────────

    public void AddInstance(string machineName, string instanceName, string? ip = null, int? memory = null, int? cpus = null)
    {
        if (!_config.Machines.TryGetValue(machineName, out var machine))
            throw new InvalidOperationException($"Machine '{machineName}' not found.");

        if (machine.Instances.Any(i => i.Name == instanceName))
            throw new InvalidOperationException($"Instance '{instanceName}' already exists in machine '{machineName}'.");

        machine.Instances.Add(new VosInstance
        {
            Name = instanceName,
            Ip = ip,
            Memory = memory,
            Cpus = cpus
        });
    }

    public void RemoveInstance(string machineName, string instanceName)
    {
        if (!_config.Machines.TryGetValue(machineName, out var machine))
            throw new InvalidOperationException($"Machine '{machineName}' not found.");

        var removed = machine.Instances.RemoveAll(i => i.Name == instanceName);
        if (removed == 0)
            throw new InvalidOperationException($"Instance '{instanceName}' not found in machine '{machineName}'.");
    }

    // ── VBoxManage CRUD ─────────────────────────────────────────────

    public void AddVboxManageCommand(string machineTypeName, List<string> command)
    {
        var mt = GetMachineType(machineTypeName);
        mt.Provider ??= new VosProviderConfig();
        mt.Provider.VboxManage.Add(command);
    }

    public void RemoveVboxManageCommand(string machineTypeName, int index)
    {
        var mt = GetMachineType(machineTypeName);
        if (mt.Provider?.VboxManage is null || index < 0 || index >= mt.Provider.VboxManage.Count)
            throw new InvalidOperationException($"VBoxManage command at index {index} not found.");
        mt.Provider.VboxManage.RemoveAt(index);
    }

    public void ClearVboxManageCommands(string machineTypeName)
    {
        var mt = GetMachineType(machineTypeName);
        if (mt.Provider is not null)
            mt.Provider.VboxManage.Clear();
    }

    public IReadOnlyList<List<string>> ListVboxManageCommands(string machineTypeName)
    {
        var mt = GetMachineType(machineTypeName);
        return mt.Provider?.VboxManage ?? new List<List<string>>();
    }

    // ── Shared folders ──────────────────────────────────────────────

    public void AddSharedFolder(string machineTypeName, string hostPath, string guestPath, string? type = null)
    {
        var mt = GetMachineType(machineTypeName);
        mt.SharedFolders.Add(new VosSharedFolder { HostPath = hostPath, GuestPath = guestPath, Type = type });
    }

    // ── Plugins ─────────────────────────────────────────────────────

    public void AddPlugin(string machineTypeName, string pluginName)
    {
        var mt = GetMachineType(machineTypeName);
        if (!mt.Plugins.Contains(pluginName))
            mt.Plugins.Add(pluginName);
    }

    // ── Provisioning ────────────────────────────────────────────────

    public void AddProvisioningStep(string machineTypeName, string key, string? version = null, bool privileged = true)
    {
        var mt = GetMachineType(machineTypeName);
        mt.Provisioning.Add(new VosProvisioningStep { Key = key, Version = version, Privileged = privileged });
    }

    // ── Helpers ─────────────────────────────────────────────────────

    private VosMachineType GetMachineType(string name)
    {
        if (!_config.MachineTypes.TryGetValue(name, out var mt))
            throw new InvalidOperationException($"Machine type '{name}' not found.");
        return mt;
    }
}
