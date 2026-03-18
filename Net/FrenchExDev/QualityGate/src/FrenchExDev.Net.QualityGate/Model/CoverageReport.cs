using System.Text.Json.Serialization;

namespace FrenchExDev.Net.QualityGate.Model;

public class CoverageReport
{
    [JsonPropertyName("lineRate")]
    public required double LineRate { get; init; }

    [JsonPropertyName("branchRate")]
    public required double BranchRate { get; init; }

    [JsonPropertyName("classes")]
    public List<CoverageClass> Classes { get; init; } = [];
}

public record CoverageClass(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("fileName")] string FileName,
    [property: JsonPropertyName("lineRate")] double LineRate,
    [property: JsonPropertyName("branchRate")] double BranchRate);
