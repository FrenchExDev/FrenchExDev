namespace FrenchExDev.Net.Vos.Config;

/// <summary>
/// A machine declaration that references a machine type and defines instances.
/// </summary>
public sealed class VosMachine
{
    /// <summary>References a key in <see cref="VosConfig.MachineTypes"/>.</summary>
    public required string MachineTypeName { get; set; }

    /// <summary>Whether this machine is enabled.</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>Instances of this machine.</summary>
    public List<VosInstance> Instances { get; set; } = new();

    // Override fields (nullable — only set if overriding the machine type)
    public string? Box { get; set; }
    public VosProviderConfig? Provider { get; set; }
    public VosNetworkConfig? Network { get; set; }
    public List<VosProvisioningStep>? Provisioning { get; set; }
    public Dictionary<string, string>? Variables { get; set; }
    public List<VosSharedFolder>? SharedFolders { get; set; }
}

/// <summary>
/// A single VM instance with unique name, hostname, and optional overrides.
/// </summary>
public sealed class VosInstance
{
    public required string Name { get; set; }
    public string? Hostname { get; set; }
    public string? Ip { get; set; }
    public string? Mac { get; set; }
    public int? Memory { get; set; }
    public int? Cpus { get; set; }
}
