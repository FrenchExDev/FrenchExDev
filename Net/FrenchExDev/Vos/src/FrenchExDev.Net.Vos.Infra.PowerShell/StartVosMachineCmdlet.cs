using System.Management.Automation;
using FrenchExDev.Net.Vos;
using FrenchExDev.Net.Vos.Config;

namespace FrenchExDev.Net.Vos.Infra.PowerShell;

/// <summary>
/// <c>Start-VosMachine</c> — starts one or more VMs via the configured backend.
/// Alias: <c>vup</c>.
/// </summary>
[Cmdlet(VerbsLifecycle.Start, "VosMachine")]
[Alias("vup")]
[OutputType(typeof(VosActionResult))]
public sealed class StartVosMachineCmdlet : VosCmdletBase
{
    protected override async Task<VosActionResult> ExecuteActionAsync(
        IVosBackend backend, ResolvedInstance instance, CancellationToken ct)
    {
        return await backend.UpAsync(instance, ct);
    }
}
