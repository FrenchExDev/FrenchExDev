using System.Text.Json.Serialization;

namespace FrenchExDev.Net.QualityGate.Model;

public class NamespaceMetrics
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("typeCount")]
    public required int TypeCount { get; init; }

    [JsonPropertyName("abstractTypeCount")]
    public required int AbstractTypeCount { get; init; }

    [JsonPropertyName("abstractness")]
    public double Abstractness => TypeCount == 0 ? 0 : (double)AbstractTypeCount / TypeCount;

    [JsonPropertyName("afferentCoupling")]
    public required int AfferentCoupling { get; init; }

    [JsonPropertyName("efferentCoupling")]
    public required int EfferentCoupling { get; init; }

    [JsonPropertyName("instability")]
    public double Instability =>
        AfferentCoupling + EfferentCoupling == 0
            ? 0
            : (double)EfferentCoupling / (AfferentCoupling + EfferentCoupling);

    [JsonPropertyName("distanceFromMainSequence")]
    public double DistanceFromMainSequence => Math.Abs(Abstractness + Instability - 1);

    [JsonPropertyName("types")]
    public List<TypeMetrics> Types { get; init; } = [];
}
