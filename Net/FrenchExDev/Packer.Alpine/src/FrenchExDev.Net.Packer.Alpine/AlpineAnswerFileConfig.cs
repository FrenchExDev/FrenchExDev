namespace FrenchExDev.Net.Packer.Alpine;

/// <summary>
/// Configuration for the Alpine <c>setup-alpine</c> answer file.
/// Maps to the PowerShell <c>New-PackerAlpineAnswerFileContent</c> function.
/// </summary>
public sealed class AlpineAnswerFileConfig
{
    public string KeyMap { get; set; } = "us us";
    public string HostName { get; set; } = "alpine";
    public string DevDevice { get; set; } = "mdev";
    public string Interfaces { get; set; } = "auto lo\niface lo inet loopback\nauto eth0\niface eth0 inet dhcp\n    hostname alpine\n    domain local\n";
    public string TimeZone { get; set; } = "UTC";
    public string Proxy { get; set; } = "none";
    public string ApkRepos { get; set; } = "-1";
    public string Sshd { get; set; } = "-c openssh";
    public string Ntp { get; set; } = "-c openntpd";
    public string Disk { get; set; } = "-m sys /dev/sda";
    public string Lbu { get; set; } = "none";
    public string ApkCache { get; set; } = "none";
    public string EraseDisks { get; set; } = "/dev/sda";
}
