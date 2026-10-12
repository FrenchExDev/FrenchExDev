namespace FrenchExDev.Net.Diem.Search;

public interface ISearchEngine
{
    Task IndexAsync(SearchDocument document, CancellationToken ct = default);
    Task DeleteAsync(string documentId, CancellationToken ct = default);
    Task<SearchResults> SearchAsync(SearchQuery query, CancellationToken ct = default);
}

public sealed class SearchDocument
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string Body { get; init; }
    public string? EntityType { get; init; }
    public string? Url { get; init; }
    public IDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>();
}

public sealed class SearchQuery
{
    public required string Text { get; init; }
    public int MaxResults { get; init; } = 20;
    public int Skip { get; init; }
    public string? EntityTypeFilter { get; init; }
}

public sealed class SearchResults
{
    public required IReadOnlyList<SearchHit> Hits { get; init; }
    public required int TotalCount { get; init; }
}

public sealed class SearchHit
{
    public required string DocumentId { get; init; }
    public required string Title { get; init; }
    public required string Snippet { get; init; }
    public required float Score { get; init; }
    public string? Url { get; init; }
}
