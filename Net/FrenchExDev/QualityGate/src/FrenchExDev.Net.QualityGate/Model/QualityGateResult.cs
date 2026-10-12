using System.Text.Json.Serialization;

namespace FrenchExDev.Net.QualityGate.Model;

public class QualityGateResult
{
    [JsonPropertyName("gateName")]
    public required string GateName { get; init; }

    [JsonPropertyName("description")]
    public required string Description { get; init; }

    [JsonPropertyName("threshold")]
    public required double Threshold { get; init; }

    [JsonPropertyName("actualValue")]
    public required double ActualValue { get; init; }

    [JsonPropertyName("passed")]
    public required bool Passed { get; init; }

    [JsonPropertyName("violatingElement")]
    public string? ViolatingElement { get; init; }
}
