using FrenchExDev.Net.Builder.Attributes;

namespace FrenchExDev.Net.Vos.Lib.Abstractions.Options;

[Builder]
public sealed class VosPackageOptions
{
    public string? Output { get; init; }
    public string? Include { get; init; }
    public string? Vagrantfile { get; init; }
    public string? Info { get; init; }
    public string? Base { get; init; }
}
