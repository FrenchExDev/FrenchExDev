using System.Management.Automation;
using FrenchExDev.Net.Vos;
using FrenchExDev.Net.Vos.Config;

namespace FrenchExDev.Net.Vos.Infra.PowerShell;

[Cmdlet(VerbsCommon.Remove, "VosMachine", SupportsShouldProcess = true)]
[Alias("vdestroy")]
[OutputType(typeof(VosActionResult))]
public sealed class RemoveVosMachineCmdlet : VosCmdletBase
{
    [Parameter]
    public SwitchParameter Force { get; set; }

    protected override async Task<VosActionResult> ExecuteActionAsync(
        IVosBackend backend, ResolvedInstance instance, CancellationToken ct)
    {
        return await backend.DestroyAsync(instance, Force, ct);
    }
}
