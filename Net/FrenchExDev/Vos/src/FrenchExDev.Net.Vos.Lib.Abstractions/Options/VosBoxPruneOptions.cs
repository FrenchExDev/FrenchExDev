using FrenchExDev.Net.Builder.Attributes;

namespace FrenchExDev.Net.Vos.Lib.Abstractions.Options;

[Builder]
public sealed class VosBoxPruneOptions
{
    public bool? Force { get; init; }
    public bool? DryRun { get; init; }
    public bool? KeepActiveBoxes { get; init; }
    public string? Name { get; init; }
    public string? Provider { get; init; }
}
