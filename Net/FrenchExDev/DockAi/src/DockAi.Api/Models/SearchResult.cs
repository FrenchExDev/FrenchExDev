namespace DockAi.Api.Models;

/// <summary>
/// Full search response returned by the API.
/// </summary>
public sealed class SearchResponse
{
    public required string Query { get; init; }
    public int TotalHits { get; init; }
    public long TookMs { get; init; }
    public List<SearchHit> Results { get; init; } = [];
    public Dictionary<string, List<FacetValue>> Facets { get; init; } = new();
}

/// <summary>
/// A single hit in search results.
/// </summary>
public sealed class SearchHit
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public float Score { get; init; }
    public string? FileType { get; init; }
    public string? Category { get; set; }
    public DateTime? Date { get; init; }
    public string? Snippet { get; init; }
    public List<string> Entities { get; init; } = [];
    public List<string> Tracks { get; init; } = [];
    public List<string> Themes { get; init; } = [];
    public List<SearchHitLink> Links { get; set; } = [];
}

/// <summary>
/// A link reference inside a search hit.
/// </summary>
public sealed class SearchHitLink
{
    public required string TargetId { get; init; }
    public required string LinkType { get; init; }
    public float Strength { get; init; }
    public List<string> Via { get; init; } = [];
    public string? Reason { get; init; }
}

/// <summary>
/// A facet count entry.
/// </summary>
public sealed class FacetValue
{
    public required string Id { get; init; }
    public string? Label { get; init; }
    public int Count { get; init; }
}
