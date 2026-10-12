using System.Collections;
using System.Management.Automation;
using System.Management.Automation.Language;
using FrenchExDev.Net.Vos.Config;
using FrenchExDev.Net.Vos.Infra.FileSystem;

namespace FrenchExDev.Net.Vos.Infra.PowerShell;

/// <summary>
/// Provides tab completion for VM instance names by reading <c>config-vos.yaml</c>.
/// </summary>
public sealed class VosMachineNameCompleter : IArgumentCompleter
{
    public IEnumerable<CompletionResult> CompleteArgument(
        string commandName,
        string parameterName,
        string wordToComplete,
        CommandAst commandAst,
        IDictionary fakeBoundParameters)
    {
        var configPath = "config-vos.yaml";
        if (!File.Exists(configPath))
            yield break;

        VosConfig config;
        try
        {
            config = new VosConfigSerializer()
                .DeserializeAsync(configPath)
                .GetAwaiter()
                .GetResult();
        }
        catch
        {
            yield break;
        }

        var resolved = VosConfigMerger.ResolveAll(config);
        foreach (var (_, inst) in resolved)
        {
            if (string.IsNullOrEmpty(wordToComplete) ||
                inst.Name.StartsWith(wordToComplete, StringComparison.OrdinalIgnoreCase))
            {
                yield return new CompletionResult(
                    inst.Name,
                    inst.Name,
                    CompletionResultType.ParameterValue,
                    $"{inst.Name} ({inst.Box ?? "no box"}, {inst.Memory}MB)");
            }
        }
    }
}
