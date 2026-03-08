using System.Text.RegularExpressions;
using FrenchExDev.Net.BinaryWrapper;

namespace FrenchExDev.Net.Packer;

/// <summary>
/// Parses standard Packer build output into typed events.
/// </summary>
public sealed partial class PackerBuildParser : IOutputParser<PackerEvent>
{
    // ==> amazon-ebs: Creating AMI...
    [GeneratedRegex(@"^==> (\S+?):\s+(.+)$")]
    private static partial Regex BuildOutputPattern();

    // ==> amazon-ebs (error): Something went wrong
    [GeneratedRegex(@"^==> (\S+?)\s+\(error\):\s+(.+)$")]
    private static partial Regex BuildErrorPattern();

    //     amazon-ebs: Provisioning with shell script
    [GeneratedRegex(@"^    (\S+?):\s+(.+)$")]
    private static partial Regex ProvisionerOutputPattern();

    // Build 'amazon-ebs' finished.
    [GeneratedRegex(@"^Build '(\S+?)' finished\.$")]
    private static partial Regex BuildFinishedPattern();

    // Build 'amazon-ebs' errored after 2m3s: ...
    [GeneratedRegex(@"^Build '(\S+?)' errored")]
    private static partial Regex BuildErroredPattern();

    // ==> Builds finished. The artifacts of successful builds are:
    // --> amazon-ebs: AMIs were created: us-east-1: ami-12345678
    [GeneratedRegex(@"^--> (\S+?):\s+(.+)$")]
    private static partial Regex ArtifactPattern();

    public IEnumerable<PackerEvent> ParseLine(OutputLine line)
    {
        var text = line.Text;

        if (string.IsNullOrWhiteSpace(text))
            yield break;

        var errorMatch = BuildErrorPattern().Match(text);
        if (errorMatch.Success)
        {
            yield return new PackerBuildError(errorMatch.Groups[1].Value, errorMatch.Groups[2].Value);
            yield break;
        }

        var outputMatch = BuildOutputPattern().Match(text);
        if (outputMatch.Success)
        {
            yield return new PackerBuildOutput(outputMatch.Groups[1].Value, outputMatch.Groups[2].Value);
            yield break;
        }

        var provisionerMatch = ProvisionerOutputPattern().Match(text);
        if (provisionerMatch.Success)
        {
            yield return new PackerProvisionerOutput(
                provisionerMatch.Groups[1].Value, "shell", provisionerMatch.Groups[2].Value);
            yield break;
        }

        var finishedMatch = BuildFinishedPattern().Match(text);
        if (finishedMatch.Success)
        {
            yield return new PackerBuildFinished(finishedMatch.Groups[1].Value, Success: true, ArtifactId: null);
            yield break;
        }

        var erroredMatch = BuildErroredPattern().Match(text);
        if (erroredMatch.Success)
        {
            yield return new PackerBuildFinished(erroredMatch.Groups[1].Value, Success: false, ArtifactId: null);
            yield break;
        }

        var artifactMatch = ArtifactPattern().Match(text);
        if (artifactMatch.Success)
        {
            yield return new PackerBuildFinished(
                artifactMatch.Groups[1].Value, Success: true, ArtifactId: artifactMatch.Groups[2].Value);
            yield break;
        }

        yield return new PackerOutputLine(text, line.Source);
    }

    public IEnumerable<PackerEvent> Complete(int exitCode)
    {
        if (exitCode != 0)
            yield return new PackerBuildError("packer", $"Process exited with code {exitCode}");
    }
}
