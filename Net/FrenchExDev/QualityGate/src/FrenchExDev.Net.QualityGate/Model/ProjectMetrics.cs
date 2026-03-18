using System.Text.Json.Serialization;

namespace FrenchExDev.Net.QualityGate.Model;

public class ProjectMetrics
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("filePath")]
    public required string FilePath { get; init; }

    [JsonPropertyName("namespaces")]
    public List<NamespaceMetrics> Namespaces { get; init; } = [];

    [JsonPropertyName("interfaces")]
    public List<InterfaceInfo> Interfaces { get; init; } = [];

    [JsonPropertyName("implementations")]
    public List<InterfaceImplementation> Implementations { get; init; } = [];

    [JsonPropertyName("orphanInterfaces")]
    public List<string> OrphanInterfaces { get; init; } = [];

    [JsonPropertyName("dependencies")]
    public List<ProjectDependency> Dependencies { get; init; } = [];

    [JsonPropertyName("publicApi")]
    public ApiSurface PublicApi { get; init; } = new();
}
