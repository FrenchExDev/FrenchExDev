namespace FrenchExDev.Net.Packer.Bundle;

/// <summary>
/// Configuration model for a Vagrantfile embedded in a Packer box.
/// Mutable — contributors modify in-place.
/// </summary>
public sealed class VagrantfileConfig
{
    public int Cpus { get; set; } = 2;
    public int Memory { get; set; } = 256;
    public int VideoMemory { get; set; } = 64;
    public string Provider { get; set; } = "virtualbox";
    public bool Enable3D { get; set; }
    public List<VagrantSyncedFolder> SyncedFolders { get; set; } = new();
    public List<VagrantNetwork> Networks { get; set; } = new();
    public Dictionary<string, string> CustomConfig { get; set; } = new();
}

public sealed record VagrantSyncedFolder(string HostPath, string GuestPath, string? Type = null, bool Disabled = false);

public sealed record VagrantNetwork(string Type, string? Ip = null, string? Mac = null);
