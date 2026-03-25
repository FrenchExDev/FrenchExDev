using System.Management.Automation;
using FrenchExDev.Net.Vos;
using FrenchExDev.Net.Vos.Config;

namespace FrenchExDev.Net.Vos.Infra.PowerShell;

[Cmdlet(VerbsCommon.Get, "VosMachineInfo")]
[Alias("vinfo")]
[OutputType(typeof(VosActionResult))]
public sealed class GetVosMachineInfoCmdlet : VosCmdletBase
{
    protected override async Task<VosActionResult> ExecuteActionAsync(
        IVosBackend backend, ResolvedInstance instance, CancellationToken ct)
        => await backend.StatusAsync(instance, ct);
}
