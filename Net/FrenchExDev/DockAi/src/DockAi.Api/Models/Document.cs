namespace DockAi.Api.Models;

/// <summary>
/// Represents a parsed and indexed document.
/// </summary>
public sealed class Document
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string FileName { get; init; }
    public required string FilePath { get; init; }
    public required string FileType { get; init; }
    public long FileSize { get; init; }
    public DateTime LastModified { get; init; }

    /// <summary>Raw text content extracted by the parser.</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>Entity ids detected in content.</summary>
    public HashSet<string> Entities { get; init; } = [];

    /// <summary>Taxonomy assignments: taxonomy_id -> set of node_ids.</summary>
    public Dictionary<string, HashSet<string>> TaxonomyAssignments { get; init; } = new();

    /// <summary>Free-form tags.</summary>
    public HashSet<string> Tags { get; init; } = [];

    /// <summary>Symbolic links to other documents / entities.</summary>
    public List<SymbolicLink> Links { get; init; } = [];
}
