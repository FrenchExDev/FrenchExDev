using FrenchExDev.Net.Builder.Attributes;

namespace FrenchExDev.Net.Vos.Lib.Abstractions.Options;

[Builder]
public sealed class VosWinrmOptions
{
    public bool? Elevated { get; init; }
    public string? Shell { get; init; }
}
