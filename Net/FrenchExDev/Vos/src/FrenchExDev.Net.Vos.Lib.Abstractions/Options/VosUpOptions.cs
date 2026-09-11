using FrenchExDev.Net.Builder.Attributes;

namespace FrenchExDev.Net.Vos.Lib.Abstractions.Options;

[Builder]
public sealed class VosUpOptions
{
    public bool? Provision { get; init; }
    public string[]? ProvisionWith { get; init; }
    public bool? DestroyOnError { get; init; }
    public bool? Parallel { get; init; }
    public string? Provider { get; init; }
    public bool? InstallProvider { get; init; }
}
