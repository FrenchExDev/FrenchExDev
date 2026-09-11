using System.Management.Automation;
using FrenchExDev.Net.Vos;
using FrenchExDev.Net.Vos.Config;

namespace FrenchExDev.Net.Vos.Infra.PowerShell;

[Cmdlet(VerbsLifecycle.Resume, "VosMachine")]
[OutputType(typeof(VosActionResult))]
public sealed class ResumeVosMachineCmdlet : VosCmdletBase
{
    protected override async Task<VosActionResult> ExecuteActionAsync(
        IVosBackend backend, ResolvedInstance instance, CancellationToken ct)
        => await backend.ResumeAsync(instance, ct);
}
