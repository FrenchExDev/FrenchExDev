namespace FrenchExDev.Net.Diem.Search.Lucene;

/// <summary>
/// Lucene.Net implementation of ISearchEngine.
/// </summary>
public sealed class LuceneSearchEngine : ISearchEngine
{
    private readonly string _indexPath;

    public LuceneSearchEngine(string indexPath) { _indexPath = indexPath; }

    public Task IndexAsync(SearchDocument document, CancellationToken ct = default)
        => throw new NotImplementedException("Lucene.Net indexing — will use Lucene.Net SDK");

    public Task DeleteAsync(string documentId, CancellationToken ct = default)
        => throw new NotImplementedException("Lucene.Net delete");

    public Task<SearchResults> SearchAsync(SearchQuery query, CancellationToken ct = default)
        => throw new NotImplementedException("Lucene.Net search");
}
