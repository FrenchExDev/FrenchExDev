using DockAi.Api.Models;

namespace DockAi.Api.Ontology;

/// <summary>
/// Stores all taxonomy trees. Provides lookup by taxonomy id and node id.
/// </summary>
public sealed class TaxonomyStore
{
    private readonly Dictionary<string, Taxonomy> _taxonomies = new();

    public IReadOnlyDictionary<string, Taxonomy> Taxonomies => _taxonomies;

    public void Add(Taxonomy taxonomy) => _taxonomies[taxonomy.Id] = taxonomy;

    public Taxonomy? Get(string id) => _taxonomies.GetValueOrDefault(id);

    /// <summary>Find a node across all taxonomies.</summary>
    public (Taxonomy taxonomy, TaxonomyNode node)? FindNode(string nodeId)
    {
        foreach (var tax in _taxonomies.Values)
        {
            var node = tax.Find(nodeId);
            if (node is not null)
                return (tax, node);
        }
        return null;
    }

    /// <summary>
    /// Get all node ids that match a query node id, including descendants.
    /// If the node has children, searching for the parent includes all children.
    /// </summary>
    public HashSet<string> ExpandNode(string taxonomyId, string nodeId)
    {
        var result = new HashSet<string> { nodeId };
        var tax = Get(taxonomyId);
        if (tax is null) return result;

        var node = tax.Find(nodeId);
        if (node is null) return result;

        foreach (var desc in node.DescendantIds())
            result.Add(desc);

        return result;
    }

    /// <summary>All taxonomies marked as facets.</summary>
    public IEnumerable<Taxonomy> FacetTaxonomies() => _taxonomies.Values.Where(t => t.Facet);

    public void Clear() => _taxonomies.Clear();
}
