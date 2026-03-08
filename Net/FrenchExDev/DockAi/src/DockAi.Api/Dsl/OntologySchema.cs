using DockAi.Api.Models;

namespace DockAi.Api.Dsl;

/// <summary>
/// Shared output model produced by both DslSchemaParser and OntologyBuilder.
/// Contains the 6 collections that fully describe an ontology.
/// </summary>
public sealed class OntologySchema
{
    public Dictionary<string, EntityType> EntityTypes { get; init; } = new();
    public Dictionary<string, Entity> Entities { get; init; } = new();
    public Dictionary<string, RelationType> RelationTypes { get; init; } = new();
    public List<Relation> Relations { get; init; } = [];
    public Dictionary<string, Taxonomy> Taxonomies { get; init; } = new();
    public List<ClassificationRule> Rules { get; init; } = [];

    /// <summary>Merge another schema into this one (additive, last-write-wins for keyed items).</summary>
    public void Merge(OntologySchema other)
    {
        foreach (var kv in other.EntityTypes) EntityTypes[kv.Key] = kv.Value;
        foreach (var kv in other.Entities) Entities[kv.Key] = kv.Value;
        foreach (var kv in other.RelationTypes) RelationTypes[kv.Key] = kv.Value;
        Relations.AddRange(other.Relations);
        foreach (var kv in other.Taxonomies) Taxonomies[kv.Key] = kv.Value;
        Rules.AddRange(other.Rules);
    }
}
