using FrenchExDev.Net.Builder.Attributes;

namespace FrenchExDev.Net.Vos.Lib.Abstractions.Options;

[Builder]
public sealed class VosSnapshotRestoreOptions
{
    public bool? NoStart { get; init; }
    public bool? Provision { get; init; }
    public string[]? ProvisionWith { get; init; }
}
