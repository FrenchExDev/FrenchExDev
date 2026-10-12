using System.Text.Json.Serialization;

namespace FrenchExDev.Net.QualityGate.Model;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TypeKind
{
    Class,
    Interface,
    Struct,
    Record,
    Enum
}

public class TypeMetrics
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("fullName")]
    public required string FullName { get; init; }

    [JsonPropertyName("filePath")]
    public required string FilePath { get; init; }

    [JsonPropertyName("line")]
    public required int Line { get; init; }

    [JsonPropertyName("kind")]
    public required TypeKind Kind { get; init; }

    [JsonPropertyName("methodCount")]
    public required int MethodCount { get; init; }

    [JsonPropertyName("propertyCount")]
    public required int PropertyCount { get; init; }

    [JsonPropertyName("fieldCount")]
    public required int FieldCount { get; init; }

    [JsonPropertyName("inheritanceDepth")]
    public required int InheritanceDepth { get; init; }

    [JsonPropertyName("lcom4")]
    public required int Lcom4 { get; init; }

    [JsonPropertyName("efferentCoupling")]
    public required int EfferentCoupling { get; init; }

    [JsonPropertyName("cyclomaticComplexity")]
    public required int CyclomaticComplexity { get; init; }

    [JsonPropertyName("cognitiveComplexity")]
    public required int CognitiveComplexity { get; init; }

    [JsonPropertyName("linesOfCode")]
    public required int LinesOfCode { get; init; }

    [JsonPropertyName("methods")]
    public List<MethodMetrics> Methods { get; init; } = [];
}
