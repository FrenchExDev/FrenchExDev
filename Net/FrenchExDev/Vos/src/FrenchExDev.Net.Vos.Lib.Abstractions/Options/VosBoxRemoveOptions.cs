using FrenchExDev.Net.Builder.Attributes;

namespace FrenchExDev.Net.Vos.Lib.Abstractions.Options;

[Builder]
public sealed class VosBoxRemoveOptions
{
    public bool? Force { get; init; }
    public bool? All { get; init; }
    public bool? AllProviders { get; init; }
    public bool? AllArchitectures { get; init; }
    public string? Provider { get; init; }
    public string? BoxVersion { get; init; }
    public string? Architecture { get; init; }
}
