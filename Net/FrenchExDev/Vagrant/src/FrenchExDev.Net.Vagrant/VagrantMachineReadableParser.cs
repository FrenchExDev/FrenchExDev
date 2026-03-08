using FrenchExDev.Net.BinaryWrapper;

namespace FrenchExDev.Net.Vagrant;

/// <summary>
/// Parses Vagrant machine-readable format: timestamp,target,type,data...
/// The <c>%!(VAGRANT_COMMA)</c> escape is unescaped to commas.
/// </summary>
public sealed class VagrantMachineReadableParser : IOutputParser<VagrantEvent>
{
    private const string CommaEscape = "%!(VAGRANT_COMMA)";

    public IEnumerable<VagrantEvent> ParseLine(OutputLine line)
    {
        if (line.Source == OutputSource.StdErr)
        {
            yield return new VagrantOutputLine(line.Text, line.Source);
            yield break;
        }

        var text = line.Text;
        if (string.IsNullOrWhiteSpace(text))
            yield break;

        // Format: timestamp,target,type,data[,data...]
        var parts = text.Split(',');
        if (parts.Length < 3)
        {
            yield return new VagrantOutputLine(text, line.Source);
            yield break;
        }

        if (!long.TryParse(parts[0], out var timestamp))
        {
            yield return new VagrantOutputLine(text, line.Source);
            yield break;
        }

        var target = Unescape(parts[1]);
        var eventType = Unescape(parts[2]);
        var data = new string[parts.Length - 3];
        for (var i = 3; i < parts.Length; i++)
            data[i - 3] = Unescape(parts[i]);

        yield return new VagrantMachineReadableEvent(timestamp, target, eventType, data);
    }

    public IEnumerable<VagrantEvent> Complete(int exitCode)
    {
        if (exitCode != 0)
            yield return new VagrantMachineError("vagrant", $"Process exited with code {exitCode}");
    }

    private static string Unescape(string value) =>
        value.Replace(CommaEscape, ",");
}
