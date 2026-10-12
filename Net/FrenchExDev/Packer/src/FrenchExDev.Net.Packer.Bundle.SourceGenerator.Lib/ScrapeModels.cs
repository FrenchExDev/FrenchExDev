using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace FrenchExDev.Net.Packer.Bundle.SourceGenerator.Lib;

/// <summary>
/// Deserialized from a scraped JSON file (e.g. <c>scrape/virtualbox-iso.json</c>).
/// </summary>
public sealed class ScrapePluginModel
{
    [JsonPropertyName("pluginKind")]
    public string PluginKind { get; set; } = "";

    [JsonPropertyName("typeName")]
    public string TypeName { get; set; } = "";

    [JsonPropertyName("repo")]
    public string Repo { get; set; } = "";

    [JsonPropertyName("sourcePath")]
    public string SourcePath { get; set; } = "";

    [JsonPropertyName("fields")]
    public List<ScrapeFieldModel> Fields { get; set; } = new();

    [JsonPropertyName("nestedBlocks")]
    public List<ScrapeNestedBlockModel> NestedBlocks { get; set; } = new();
}

public sealed class ScrapeFieldModel
{
    [JsonPropertyName("hclName")]
    public string HclName { get; set; } = "";

    [JsonPropertyName("ctyType")]
    public string CtyType { get; set; } = "";

    [JsonPropertyName("isRequired")]
    public bool IsRequired { get; set; }
}

public sealed class ScrapeNestedBlockModel
{
    [JsonPropertyName("hclName")]
    public string HclName { get; set; } = "";

    [JsonPropertyName("nestedTypeName")]
    public string NestedTypeName { get; set; } = "";
}
