using System.Management.Automation;
using FrenchExDev.Net.Vos;
using FrenchExDev.Net.Vos.Config;

namespace FrenchExDev.Net.Vos.Infra.PowerShell;

[Cmdlet(VerbsCommon.Enter, "VosMachineSsh")]
[Alias("vssh")]
[OutputType(typeof(VosActionResult))]
public sealed class EnterVosMachineSshCmdlet : VosCmdletBase
{
    protected override async Task<VosActionResult> ExecuteActionAsync(
        IVosBackend backend, ResolvedInstance instance, CancellationToken ct)
        => await backend.SshAsync(instance, ct);
}
