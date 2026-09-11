using System.Text.Json.Serialization;

namespace FrenchExDev.Net.QualityGate.Model;

public record ProjectDependency(
    [property: JsonPropertyName("fromProject")] string FromProject,
    [property: JsonPropertyName("toProject")] string ToProject);
