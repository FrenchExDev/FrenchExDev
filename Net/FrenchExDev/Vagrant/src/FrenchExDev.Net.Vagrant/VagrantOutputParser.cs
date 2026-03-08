using System.Text.RegularExpressions;
using FrenchExDev.Net.BinaryWrapper;

namespace FrenchExDev.Net.Vagrant;

/// <summary>
/// Parses standard Vagrant output into typed events.
/// </summary>
public sealed partial class VagrantOutputParser : IOutputParser<VagrantEvent>
{
    // ==> default: Importing base box...
    [GeneratedRegex(@"^==> (\S+?):\s+(.+)$")]
    private static partial Regex MachineOutputPattern();

    // ==> default (error): Something went wrong
    [GeneratedRegex(@"^==> (\S+?)\s+\(error\):\s+(.+)$")]
    private static partial Regex MachineErrorPattern();

    //     default: Provisioning with shell script
    [GeneratedRegex(@"^    (\S+?):\s+(.+)$")]
    private static partial Regex ProvisionerOutputPattern();

    // ==> default: Machine booted and ready!
    [GeneratedRegex(@"^==> (\S+?):\s+Machine booted and ready!$")]
    private static partial Regex MachineReadyPattern();

    public IEnumerable<VagrantEvent> ParseLine(OutputLine line)
    {
        var text = line.Text;

        if (string.IsNullOrWhiteSpace(text))
            yield break;

        var errorMatch = MachineErrorPattern().Match(text);
        if (errorMatch.Success)
        {
            yield return new VagrantMachineError(errorMatch.Groups[1].Value, errorMatch.Groups[2].Value);
            yield break;
        }

        var readyMatch = MachineReadyPattern().Match(text);
        if (readyMatch.Success)
        {
            yield return new VagrantActionCompleted(readyMatch.Groups[1].Value, Success: true);
            yield break;
        }

        var outputMatch = MachineOutputPattern().Match(text);
        if (outputMatch.Success)
        {
            yield return new VagrantMachineOutput(outputMatch.Groups[1].Value, outputMatch.Groups[2].Value);
            yield break;
        }

        var provisionerMatch = ProvisionerOutputPattern().Match(text);
        if (provisionerMatch.Success)
        {
            yield return new VagrantProvisionerOutput(
                provisionerMatch.Groups[1].Value, provisionerMatch.Groups[2].Value);
            yield break;
        }

        yield return new VagrantOutputLine(text, line.Source);
    }

    public IEnumerable<VagrantEvent> Complete(int exitCode)
    {
        if (exitCode != 0)
            yield return new VagrantMachineError("vagrant", $"Process exited with code {exitCode}");
    }
}
