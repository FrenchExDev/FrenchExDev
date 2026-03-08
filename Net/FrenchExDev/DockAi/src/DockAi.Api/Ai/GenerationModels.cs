using System.Text.Json.Serialization;

namespace DockAi.Api.Ai;

// ── Result ──

public sealed class GenerationResult
{
    public string CorpusSummary { get; set; } = "";
    public List<string> SampledFiles { get; set; } = [];
    public List<ProposedEntityType> ProposedTypes { get; set; } = [];
    public List<ProposedEntity> ProposedEntities { get; set; } = [];
    public List<ProposedRelationType> ProposedRelationTypes { get; set; } = [];
    public List<ProposedRelation> ProposedRelations { get; set; } = [];
    public List<ProposedTaxonomy> ProposedTaxonomies { get; set; } = [];
    public List<ProposedRule> ProposedRules { get; set; } = [];
    public Dsl.OntologySchema? Schema { get; set; }
    public string DslText { get; set; } = "";
}

// ── Options ──

public sealed class GenerationOptions
{
    public int MaxSampleDocs { get; set; } = 30;
    public string? DomainHint { get; set; }
    public bool IncludeRelations { get; set; } = true;
    public bool IncludeTaxonomies { get; set; } = true;
    public bool IncludeRules { get; set; } = true;
    public string? ExistingDslPath { get; set; }
}

// ── Internal sample model ──

public sealed record DocumentSample(string FileName, string Extension, string Content);

// ── Proposed models (deserialized from LLM JSON responses) ──

public sealed class ProposedEntityType
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("properties")]
    public List<ProposedProperty> Properties { get; set; } = [];
}

public sealed class ProposedProperty
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("kind")]
    public string Kind { get; set; } = "string";

    [JsonPropertyName("enum_values")]
    public List<string>? EnumValues { get; set; }
}

public sealed class ProposedEntity
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("type_id")]
    public string TypeId { get; set; } = "";

    [JsonPropertyName("properties")]
    public Dictionary<string, object?> Properties { get; set; } = new();
}

public sealed class ProposedRelationType
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("from_type")]
    public string FromType { get; set; } = "any";

    [JsonPropertyName("to_types")]
    public List<string> ToTypes { get; set; } = ["any"];

    [JsonPropertyName("property_names")]
    public List<string>? PropertyNames { get; set; }

    [JsonPropertyName("inverse")]
    public string? Inverse { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }
}

public sealed class ProposedRelation
{
    [JsonPropertyName("from_id")]
    public string FromId { get; set; } = "";

    [JsonPropertyName("type_id")]
    public string TypeId { get; set; } = "";

    [JsonPropertyName("to_id")]
    public string ToId { get; set; } = "";

    [JsonPropertyName("properties")]
    public Dictionary<string, string> Properties { get; set; } = new();
}

public sealed class ProposedTaxonomy
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("label")]
    public string Label { get; set; } = "";

    [JsonPropertyName("facet")]
    public bool Facet { get; set; } = true;

    [JsonPropertyName("nodes")]
    public List<ProposedTaxonomyNode> Nodes { get; set; } = [];
}

public sealed class ProposedTaxonomyNode
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("label")]
    public string Label { get; set; } = "";

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("color")]
    public string? Color { get; set; }

    [JsonPropertyName("children")]
    public List<ProposedTaxonomyNode>? Children { get; set; }
}

public sealed class ProposedRule
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("pattern")]
    public string Pattern { get; set; } = "";

    [JsonPropertyName("taxonomy_id")]
    public string TaxonomyId { get; set; } = "";

    [JsonPropertyName("node_id")]
    public string NodeId { get; set; } = "";

    [JsonPropertyName("confidence")]
    public float Confidence { get; set; } = 0.5f;
}
