using System.Management.Automation;
using FrenchExDev.Net.Vos;
using FrenchExDev.Net.Vos.Config;

namespace FrenchExDev.Net.Vos.Infra.PowerShell;

[Cmdlet(VerbsCommon.Get, "VosMachineStatus")]
[Alias("vst")]
[OutputType(typeof(VosActionResult))]
public sealed class GetVosMachineStatusCmdlet : VosCmdletBase
{
    protected override async Task<VosActionResult> ExecuteActionAsync(
        IVosBackend backend, ResolvedInstance instance, CancellationToken ct)
    {
        return await backend.StatusAsync(instance, ct);
    }
}
