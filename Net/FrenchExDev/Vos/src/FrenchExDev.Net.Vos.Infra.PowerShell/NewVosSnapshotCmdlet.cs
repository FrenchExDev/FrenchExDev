using System.Management.Automation;
using FrenchExDev.Net.Vos;
using FrenchExDev.Net.Vos.Config;

namespace FrenchExDev.Net.Vos.Infra.PowerShell;

[Cmdlet(VerbsCommon.New, "VosSnapshot")]
[OutputType(typeof(VosActionResult))]
public sealed class NewVosSnapshotCmdlet : VosCmdletBase
{
    [Parameter(Mandatory = true, Position = 1)]
    public string SnapshotName { get; set; } = "";

    protected override async Task<VosActionResult> ExecuteActionAsync(
        IVosBackend backend, ResolvedInstance instance, CancellationToken ct)
        => await backend.SnapshotSaveAsync(instance, SnapshotName, ct);
}
