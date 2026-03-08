using System.Text.Json;
using DockAi.Api.Dsl;
using DockAi.Api.Models;

namespace DockAi.Api.Ontology;

/// <summary>
/// Manages loading the ontology from DSL files and persisting to JSON cache.
/// </summary>
public sealed class OntologyManager
{
    private readonly OntologyStore _ontology;
    private readonly TaxonomyStore _taxonomy;
    private readonly string _dataDir;
    private readonly ILogger<OntologyManager> _logger;

    public OntologyStore Ontology => _ontology;
    public TaxonomyStore Taxonomy => _taxonomy;
    public List<ClassificationRule> Rules { get; private set; } = [];

    public OntologyManager(OntologyStore ontology, TaxonomyStore taxonomy, string dataDir, ILogger<OntologyManager> logger)
    {
        _ontology = ontology;
        _taxonomy = taxonomy;
        _dataDir = dataDir;
        _logger = logger;
    }

    /// <summary>Load ontology from DSL file(s) in the data directory.</summary>
    public void Load() => Load(Array.Empty<OntologySchema>());

    /// <summary>Load ontology from DSL files + additional programmatic schemas.</summary>
    public void Load(params OntologySchema[] additionalSchemas)
    {
        _ontology.Clear();
        _taxonomy.Clear();
        Rules = [];

        // Merge all DSL files into one schema
        var merged = new OntologySchema();

        var dslFiles = Directory.GetFiles(_dataDir, "*.dsl");
        if (dslFiles.Length == 0 && additionalSchemas.Length == 0)
        {
            _logger.LogWarning("No .dsl files found in {DataDir} and no additional schemas", _dataDir);
            return;
        }

        foreach (var file in dslFiles)
        {
            _logger.LogInformation("Parsing DSL file: {File}", Path.GetFileName(file));
            var source = File.ReadAllText(file);
            var parser = new DslSchemaParser();
            parser.Parse(source);
            merged.Merge(parser.ToSchema());
        }

        // Merge additional programmatic schemas
        foreach (var schema in additionalSchemas)
            merged.Merge(schema);

        // Populate stores from merged schema
        foreach (var et in merged.EntityTypes.Values) _ontology.AddEntityType(et);
        foreach (var e in merged.Entities.Values) _ontology.AddEntity(e);
        foreach (var rt in merged.RelationTypes.Values) _ontology.AddRelationType(rt);
        foreach (var r in merged.Relations) _ontology.AddRelation(r);
        foreach (var t in merged.Taxonomies.Values) _taxonomy.Add(t);
        Rules.AddRange(merged.Rules);

        _logger.LogInformation(
            "Ontology loaded: {Types} types, {Entities} entities, {RelTypes} relation types, {Rels} relations, {Taxes} taxonomies, {Rules} rules",
            _ontology.EntityTypes.Count,
            _ontology.Entities.Count,
            _ontology.RelationTypes.Count,
            _ontology.Relations.Count,
            _taxonomy.Taxonomies.Count,
            Rules.Count);

        // Persist compiled JSON cache
        SaveJsonCache();
    }

    private void SaveJsonCache()
    {
        var opts = new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        try
        {
            var ontologyPath = Path.Combine(_dataDir, "ontology.json");
            var data = new
            {
                entityTypes = _ontology.EntityTypes,
                entities = _ontology.Entities,
                relationTypes = _ontology.RelationTypes,
                relations = _ontology.Relations
            };
            File.WriteAllText(ontologyPath, JsonSerializer.Serialize(data, opts));

            var taxonomyPath = Path.Combine(_dataDir, "taxonomies.json");
            File.WriteAllText(taxonomyPath, JsonSerializer.Serialize(_taxonomy.Taxonomies, opts));

            _logger.LogInformation("JSON cache saved to {DataDir}", _dataDir);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save JSON cache");
        }
    }

    /// <summary>Get the full ontology as a serializable object for the API.</summary>
    public object GetOntologySnapshot() => new
    {
        entityTypes = _ontology.EntityTypes,
        entities = _ontology.Entities.Values.Select(e => new { e.Id, e.TypeId, e.Name, e.Aliases, e.Properties }),
        relationTypes = _ontology.RelationTypes,
        relations = _ontology.Relations,
        taxonomies = _taxonomy.Taxonomies.Values.Select(t => new
        {
            t.Id,
            t.Label,
            t.Facet,
            nodes = t.AllNodes().Select(n => new { n.Id, n.Label, n.Description, n.Color, n.Icon, parentId = n.Parent?.Id })
        })
    };
}
