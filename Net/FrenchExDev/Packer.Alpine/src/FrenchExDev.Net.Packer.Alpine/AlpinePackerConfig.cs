namespace FrenchExDev.Net.Packer.Alpine;

/// <summary>
/// Default configuration for an Alpine Packer build.
/// Maps to the PowerShell <c>New-PackerAlpineConfigObject</c> function.
/// </summary>
public sealed class AlpinePackerConfig
{
    public string AlpineVersion { get; set; } = "3.21";
    public string Arch { get; set; } = "x86_64";
    public string Flavor { get; set; } = "virt";
    public int Cpus { get; set; } = 4;
    public int Memory { get; set; } = 256;
    public int VideoMemory { get; set; } = 64;
    public int DiskSize { get; set; } = 20480;
    public string BootWait { get; set; } = "10s";
    public string SshTimeout { get; set; } = "10m";
    public string SshUsername { get; set; } = "vagrant";
    public string SshPassword { get; set; } = "vagrant";
    public string RootPassword { get; set; } = "vagrant";
    public string BoxVersion { get; set; } = "1.0.0";
    public string Author { get; set; } = "FrenchExDev";
    public string Description { get; set; } = "";
    public string OutputDirectory { get; set; } = "output-vagrant";

    /// <summary>Computes the ISO download URL for the given version/arch/flavor.</summary>
    public string IsoDownloadUrl =>
        $"http://dl-cdn.alpinelinux.org/alpine/v{AlpineVersion}/releases/{Arch}/alpine-{Flavor}-{AlpineVersion}.0-{Arch}.iso";

    /// <summary>Computes the local ISO cache path.</summary>
    public string IsoLocalUrl =>
        $"./cache/iso/alpine-{Flavor}-{AlpineVersion}.0-{Arch}.iso";

    /// <summary>Computes the VM name.</summary>
    public string VmName =>
        $"alpine-{AlpineVersion}-{Flavor}";
}
