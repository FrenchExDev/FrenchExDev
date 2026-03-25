namespace FrenchExDev.Net.Vos.Config;

/// <summary>
/// Deep-merges machine instance ← machine ← machine_type to produce a resolved configuration.
/// Pure function — no side effects.
/// </summary>
public static class VosConfigMerger
{
    /// <summary>
    /// Resolves the effective configuration for a given instance,
    /// applying overrides: instance ← machine ← machine_type.
    /// </summary>
    public static ResolvedInstance Resolve(VosConfig config, string machineName, VosInstance instance)
    {
        if (!config.Machines.TryGetValue(machineName, out var machine))
            throw new InvalidOperationException($"Machine '{machineName}' not found in config.");

        if (!config.MachineTypes.TryGetValue(machine.MachineTypeName, out var machineType))
            throw new InvalidOperationException(
                $"Machine type '{machine.MachineTypeName}' referenced by machine '{machineName}' not found.");

        var provider = ResolveProvider(machine.Provider, machineType.Provider);

        return new ResolvedInstance
        {
            Name = instance.Name,
            Hostname = instance.Hostname ?? instance.Name,
            Box = machine.Box ?? machineType.Box,
            Memory = instance.Memory ?? provider.Memory,
            Cpus = instance.Cpus ?? provider.Cpus,
            VideoMemory = provider.VideoMemory,
            ProviderType = provider.Type,
            Ip = instance.Ip ?? machineType.Network?.Private?.Ip,
            Mac = instance.Mac ?? machineType.Network?.Private?.Mac,
            Provisioning = machine.Provisioning ?? machineType.Provisioning,
            Variables = MergeDictionaries(machineType.Variables, machine.Variables),
            SharedFolders = machine.SharedFolders ?? machineType.SharedFolders,
            Plugins = machineType.Plugins,
            VboxManage = provider.VboxManage,
            Gui = provider.Gui,
            LinkedClones = provider.LinkedClones,
        };
    }

    /// <summary>
    /// Resolves all instances across all enabled machines.
    /// </summary>
    public static IReadOnlyList<(string MachineName, ResolvedInstance Instance)> ResolveAll(VosConfig config)
    {
        var results = new List<(string, ResolvedInstance)>();

        foreach (var (machineName, machine) in config.Machines)
        {
            if (!machine.IsEnabled) continue;

            foreach (var instance in machine.Instances)
            {
                results.Add((machineName, Resolve(config, machineName, instance)));
            }
        }

        return results;
    }

    private static VosProviderConfig ResolveProvider(VosProviderConfig? machineProvider, VosProviderConfig? typeProvider)
    {
        var fallback = new VosProviderConfig();
        var mp = machineProvider ?? fallback;
        var tp = typeProvider ?? fallback;

        return new VosProviderConfig
        {
            Type = mp.Type != fallback.Type ? mp.Type : tp.Type,
            Memory = mp.Memory != fallback.Memory ? mp.Memory : tp.Memory,
            Cpus = mp.Cpus != fallback.Cpus ? mp.Cpus : tp.Cpus,
            VideoMemory = mp.VideoMemory != fallback.VideoMemory ? mp.VideoMemory : tp.VideoMemory,
            Gui = mp.Gui || tp.Gui,
            LinkedClones = mp.LinkedClones && tp.LinkedClones,
            VboxManage = mp.VboxManage.Count > 0 ? mp.VboxManage : tp.VboxManage
        };
    }

    private static Dictionary<string, string> MergeDictionaries(
        Dictionary<string, string>? baseDict,
        Dictionary<string, string>? overrideDict)
    {
        var result = new Dictionary<string, string>();
        if (baseDict is not null)
            foreach (var kvp in baseDict) result[kvp.Key] = kvp.Value;
        if (overrideDict is not null)
            foreach (var kvp in overrideDict) result[kvp.Key] = kvp.Value;
        return result;
    }
}

/// <summary>
/// A fully resolved instance configuration after deep-merge.
/// </summary>
public sealed class ResolvedInstance
{
    public required string Name { get; init; }
    public required string Hostname { get; init; }
    public string? Box { get; init; }
public int Memory { get; init; }
    public int Cpus { get; init; }
    public int VideoMemory { get; init; }
    public string ProviderType { get; init; } = "virtualbox";
    public string? Ip { get; init; }
    public string? Mac { get; init; }
    public List<VosProvisioningStep> Provisioning { get; init; } = new();
    public Dictionary<string, string> Variables { get; init; } = new();
    public List<VosSharedFolder> SharedFolders { get; init; } = new();
    public List<string> Plugins { get; init; } = new();
    public List<List<string>> VboxManage { get; init; } = new();
    public bool Gui { get; init; }
    public bool LinkedClones { get; init; }
}
