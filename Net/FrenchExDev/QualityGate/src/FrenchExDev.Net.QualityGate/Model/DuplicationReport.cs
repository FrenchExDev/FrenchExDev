using System.Text.Json.Serialization;

namespace FrenchExDev.Net.QualityGate.Model;

public class DuplicationReport
{
    [JsonPropertyName("duplicationPercent")]
    public required double DuplicationPercent { get; init; }

    [JsonPropertyName("clones")]
    public List<CloneGroup> Clones { get; init; } = [];
}

public record CloneGroup(
    [property: JsonPropertyName("instances")] List<CloneInstance> Instances,
    [property: JsonPropertyName("tokenCount")] int TokenCount);

public record CloneInstance(
    [property: JsonPropertyName("filePath")] string FilePath,
    [property: JsonPropertyName("startLine")] int StartLine,
    [property: JsonPropertyName("endLine")] int EndLine);
