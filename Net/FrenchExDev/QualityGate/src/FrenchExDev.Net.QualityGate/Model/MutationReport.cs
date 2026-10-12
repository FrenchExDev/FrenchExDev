using System.Text.Json.Serialization;

namespace FrenchExDev.Net.QualityGate.Model;

public class MutationReport
{
    [JsonPropertyName("mutationScore")]
    public required double MutationScore { get; init; }

    [JsonPropertyName("totalMutants")]
    public required int TotalMutants { get; init; }

    [JsonPropertyName("killed")]
    public required int Killed { get; init; }

    [JsonPropertyName("survived")]
    public required int Survived { get; init; }

    [JsonPropertyName("noCoverage")]
    public required int NoCoverage { get; init; }

    [JsonPropertyName("timeout")]
    public required int Timeout { get; init; }

    [JsonPropertyName("files")]
    public List<MutationFileReport> Files { get; init; } = [];
}

public record MutationFileReport(
    [property: JsonPropertyName("path")] string Path,
    [property: JsonPropertyName("mutationScore")] double MutationScore,
    [property: JsonPropertyName("killed")] int Killed,
    [property: JsonPropertyName("survived")] int Survived,
    [property: JsonPropertyName("noCoverage")] int NoCoverage);
