using System.Text.Json.Serialization;

namespace FrenchExDev.Net.QualityGate.Model;

public class MethodMetrics
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("fullName")]
    public required string FullName { get; init; }

    [JsonPropertyName("line")]
    public required int Line { get; init; }

    [JsonPropertyName("cyclomaticComplexity")]
    public required int CyclomaticComplexity { get; init; }

    [JsonPropertyName("cognitiveComplexity")]
    public required int CognitiveComplexity { get; init; }

    [JsonPropertyName("linesOfCode")]
    public required int LinesOfCode { get; init; }

    [JsonPropertyName("parameterCount")]
    public required int ParameterCount { get; init; }

    [JsonPropertyName("maintainabilityIndex")]
    public double? MaintainabilityIndex { get; init; }
}
