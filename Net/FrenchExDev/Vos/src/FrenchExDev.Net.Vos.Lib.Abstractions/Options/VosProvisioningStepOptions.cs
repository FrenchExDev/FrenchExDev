using FrenchExDev.Net.Builder.Attributes;

namespace FrenchExDev.Net.Vos.Lib.Abstractions.Options;

[Builder]
public sealed class VosProvisioningStepOptions
{
    public string? Version { get; init; }
    public string? Extension { get; init; }
    public bool? Privileged { get; init; }
    public bool? ReloadBefore { get; init; }
    public bool? ReloadAfter { get; init; }
    public Dictionary<string, string>? Env { get; init; }
}
