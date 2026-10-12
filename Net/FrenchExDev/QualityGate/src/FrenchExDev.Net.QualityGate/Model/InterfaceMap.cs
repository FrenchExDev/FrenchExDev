using System.Text.Json.Serialization;

namespace FrenchExDev.Net.QualityGate.Model;

public record InterfaceInfo(
    [property: JsonPropertyName("fullName")] string FullName,
    [property: JsonPropertyName("filePath")] string FilePath,
    [property: JsonPropertyName("line")] int Line,
    [property: JsonPropertyName("members")] List<string> Members);

public record InterfaceImplementation(
    [property: JsonPropertyName("interfaceFullName")] string InterfaceFullName,
    [property: JsonPropertyName("implementingTypeFullName")] string ImplementingTypeFullName,
    [property: JsonPropertyName("filePath")] string FilePath,
    [property: JsonPropertyName("line")] int Line);
