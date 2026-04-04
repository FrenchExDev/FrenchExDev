using FrenchExDev.Net.Builder.Attributes;

namespace FrenchExDev.Net.Vos.Lib.Abstractions.Options;

[Builder]
public sealed class VosBoxOutdatedOptions
{
    public bool? Global { get; init; }
    public bool? Force { get; init; }
    public bool? Insecure { get; init; }
    public string? Cacert { get; init; }
    public string? Capath { get; init; }
    public string? Cert { get; init; }
}
