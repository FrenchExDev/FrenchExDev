using System.Management.Automation;
using FrenchExDev.Net.Vos;
using FrenchExDev.Net.Vos.Config;

namespace FrenchExDev.Net.Vos.Infra.PowerShell;

[Cmdlet(VerbsLifecycle.Invoke, "VosMachineSshCommand")]
[OutputType(typeof(VosActionResult))]
public sealed class InvokeVosMachineSshCommandCmdlet : VosCmdletBase
{
    [Parameter(Mandatory = true, Position = 1)]
    public string Command { get; set; } = "";

    protected override async Task<VosActionResult> ExecuteActionAsync(
        IVosBackend backend, ResolvedInstance instance, CancellationToken ct)
        => await backend.SshCommandAsync(instance, Command, ct);
}
