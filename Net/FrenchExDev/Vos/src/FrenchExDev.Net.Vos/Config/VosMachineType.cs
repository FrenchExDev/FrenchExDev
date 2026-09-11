namespace FrenchExDev.Net.Vos.Config;

/// <summary>
/// A reusable machine type template.
/// Machines reference a machine type by name and inherit its configuration.
/// </summary>
public sealed class VosMachineType
{
    /// <summary>Vagrant box name (vagrant backend).</summary>
    public string? Box { get; set; }

    /// <summary>Vagrant box version.</summary>
    public string? BoxVersion { get; set; }

    /// <summary>Vagrant box URL.</summary>
    public string? BoxUrl { get; set; }

    /// <summary>Provider configuration.</summary>
    public VosProviderConfig? Provider { get; set; }

    /// <summary>Network configuration.</summary>
    public VosNetworkConfig? Network { get; set; }

    /// <summary>Ordered provisioning steps.</summary>
    public List<VosProvisioningStep> Provisioning { get; set; } = new();

    /// <summary>Machine type variables (key-value).</summary>
    public Dictionary<string, string> Variables { get; set; } = new();

    /// <summary>Shared folders.</summary>
    public List<VosSharedFolder> SharedFolders { get; set; } = new();

    /// <summary>Vagrant plugins to install.</summary>
    public List<string> Plugins { get; set; } = new();

    /// <summary>Base path for provisioning scripts (e.g. "provisioning/").</summary>
    public string? ProvisioningPath { get; set; }

    /// <summary>Whether this machine type is enabled.</summary>
    public bool IsEnabled { get; set; } = true;
}

public sealed class VosProviderConfig
{
    public string Type { get; set; } = "virtualbox";
    public int Memory { get; set; } = 1024;
    public int Cpus { get; set; } = 2;
    public int VideoMemory { get; set; } = 64;
    public bool Gui { get; set; }
    public bool LinkedClones { get; set; } = true;
    public List<List<string>> VboxManage { get; set; } = new();
}

public sealed class VosNetworkConfig
{
    public VosPrivateNetwork? Private { get; set; }
    public VosPublicNetwork? Public { get; set; }
}

public sealed class VosPrivateNetwork
{
    public string? Ip { get; set; }
    public string? Mac { get; set; }
}

public sealed class VosPublicNetwork
{
    public string? Bridge { get; set; }
}

public sealed class VosProvisioningStep
{
    public required string Key { get; set; }
    public string? Version { get; set; }
    public string? Extension { get; set; }
    public bool Enabled { get; set; } = true;
    public bool ReloadBefore { get; set; }
    public bool ReloadAfter { get; set; }
    public bool Privileged { get; set; } = true;
    public Dictionary<string, string> Env { get; set; } = new();
}

public sealed class VosSharedFolder
{
    public required string HostPath { get; set; }
    public required string GuestPath { get; set; }
    public string? Type { get; set; }
    public bool Disabled { get; set; }
}
