using System.Text.Json.Serialization;

namespace FrenchExDev.Net.QualityGate.Model;

public class ApiSurface
{
    [JsonPropertyName("publicTypeCount")]
    public int PublicTypeCount { get; init; }

    [JsonPropertyName("publicMethodCount")]
    public int PublicMethodCount { get; init; }

    [JsonPropertyName("publicPropertyCount")]
    public int PublicPropertyCount { get; init; }

    [JsonPropertyName("publicTypes")]
    public List<string> PublicTypes { get; init; } = [];
}
