using System.Management.Automation;
using FrenchExDev.Net.Vos;
using FrenchExDev.Net.Vos.Config;

namespace FrenchExDev.Net.Vos.Infra.PowerShell;

[Cmdlet(VerbsLifecycle.Invoke, "VosMachineProvision")]
[OutputType(typeof(VosActionResult))]
public sealed class InvokeVosMachineProvisionCmdlet : VosCmdletBase
{
    protected override async Task<VosActionResult> ExecuteActionAsync(
        IVosBackend backend, ResolvedInstance instance, CancellationToken ct)
        => await backend.ProvisionAsync(instance, ct);
}
