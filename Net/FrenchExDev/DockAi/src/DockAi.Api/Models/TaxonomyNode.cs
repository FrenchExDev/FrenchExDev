namespace DockAi.Api.Models;

/// <summary>
/// A taxonomy tree defined via @taxonomy.
/// </summary>
public sealed class Taxonomy
{
    public required string Id { get; init; }
    public string? Label { get; init; }
    public bool Facet { get; init; }
    public List<TaxonomyNode> Roots { get; init; } = [];

    /// <summary>Flatten all nodes depth-first.</summary>
    public IEnumerable<TaxonomyNode> AllNodes() => Roots.SelectMany(Flatten);

    private static IEnumerable<TaxonomyNode> Flatten(TaxonomyNode node)
    {
        yield return node;
        foreach (var child in node.Children.SelectMany(Flatten))
            yield return child;
    }

    /// <summary>Find a node by id, searching the full tree.</summary>
    public TaxonomyNode? Find(string nodeId) => AllNodes().FirstOrDefault(n => n.Id == nodeId);
}

/// <summary>
/// A single node in a taxonomy tree.
/// </summary>
public sealed class TaxonomyNode
{
    public required string Id { get; init; }
    public required string Label { get; init; }
    public string? Description { get; init; }
    public string? Color { get; init; }
    public string? Icon { get; init; }
    public TaxonomyNode? Parent { get; set; }
    public List<TaxonomyNode> Children { get; init; } = [];

    /// <summary>All ancestor ids from this node to the root (exclusive of self).</summary>
    public IEnumerable<string> AncestorIds()
    {
        var current = Parent;
        while (current is not null)
        {
            yield return current.Id;
            current = current.Parent;
        }
    }

    /// <summary>All descendant ids (exclusive of self).</summary>
    public IEnumerable<string> DescendantIds() =>
        Children.SelectMany(c => new[] { c.Id }.Concat(c.DescendantIds()));
}
