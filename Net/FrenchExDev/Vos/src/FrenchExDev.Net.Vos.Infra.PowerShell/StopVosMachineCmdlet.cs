using System.Management.Automation;
using FrenchExDev.Net.Vos;
using FrenchExDev.Net.Vos.Config;

namespace FrenchExDev.Net.Vos.Infra.PowerShell;

[Cmdlet(VerbsLifecycle.Stop, "VosMachine")]
[Alias("vhalt")]
[OutputType(typeof(VosActionResult))]
public sealed class StopVosMachineCmdlet : VosCmdletBase
{
    [Parameter]
    public SwitchParameter Force { get; set; }

    protected override async Task<VosActionResult> ExecuteActionAsync(
        IVosBackend backend, ResolvedInstance instance, CancellationToken ct)
    {
        return await backend.HaltAsync(instance, Force, ct);
    }
}
