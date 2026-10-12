using FrenchExDev.Net.Builder.Attributes;

namespace FrenchExDev.Net.Vos.Lib.Abstractions.Options;

[Builder]
public sealed class VosPackerBuildOptions
{
    public bool? Force { get; init; }
    public Dictionary<string, string>? Vars { get; init; }
    public string? VarFile { get; init; }
    public string? Only { get; init; }
    public string? Except { get; init; }
    public string? OnError { get; init; }
    public int? ParallelBuilds { get; init; }
}
