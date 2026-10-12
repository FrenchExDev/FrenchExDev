using System.Text;

namespace FrenchExDev.Net.Packer.Alpine;

/// <summary>
/// Generates an Alpine Linux <c>setup-alpine</c> answer file from configuration.
/// Maps to the PowerShell <c>New-PackerAlpineAnswerFileContent</c> function.
/// </summary>
public interface IAnswerFileGenerator
{
    string Generate(AlpineAnswerFileConfig config);
}

public sealed class AlpineAnswerFileGenerator : IAnswerFileGenerator
{
    public string Generate(AlpineAnswerFileConfig config)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"KEYMAPOPTS=\"{config.KeyMap}\"");
        sb.AppendLine($"HOSTNAMEOPTS={config.HostName}");
        sb.AppendLine($"DEVDOPTS={config.DevDevice}");
        sb.AppendLine($"INTERFACESOPTS=\"{config.Interfaces}\"");
        sb.AppendLine($"TIMEZONEOPTS=\"{config.TimeZone}\"");
        sb.AppendLine($"PROXYOPTS={config.Proxy}");
        sb.AppendLine($"APKREPOSOPTS=\"{config.ApkRepos}\"");
        sb.AppendLine($"SSHDOPTS=\"{config.Sshd}\"");
        sb.AppendLine($"NTPOPTS=\"{config.Ntp}\"");
        sb.AppendLine($"DISKOPTS=\"{config.Disk}\"");
        sb.AppendLine($"LBUOPTS={config.Lbu}");
        sb.AppendLine($"APKCACHEOPTS={config.ApkCache}");
        sb.AppendLine($"export ERASE_DISKS={config.EraseDisks}");
        return sb.ToString();
    }
}
