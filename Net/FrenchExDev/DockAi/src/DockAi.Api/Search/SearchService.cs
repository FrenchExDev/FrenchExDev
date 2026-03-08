using DockAi.Api.Dsl;
using DockAi.Api.Indexing;
using DockAi.Api.Models;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Index;
using Lucene.Net.Search;
using Lucene.Net.Search.Highlight;
using Lucene.Net.Util;

namespace DockAi.Api.Search;

/// <summary>
/// Orchestrates DSL query parsing, Lucene search, and result assembly.
/// </summary>
public sealed class SearchService
{
    private const LuceneVersion AppVersion = LuceneVersion.LUCENE_48;
    private readonly LuceneIndexer _indexer;
    private readonly IndexManager _indexManager;
    private readonly ILogger<SearchService> _logger;

    public SearchService(LuceneIndexer indexer, IndexManager indexManager, ILogger<SearchService> logger)
    {
        _indexer = indexer;
        _indexManager = indexManager;
        _logger = logger;
    }

    public SearchResponse Search(string queryString, int maxResults = 50)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        // Parse DSL
        var dslParser = new DslQueryParser();
        dslParser.Parse(queryString);

        using var reader = DirectoryReader.Open(_indexer.LuceneDirectory);
        var searcher = new IndexSearcher(reader);

        // Handle link-based queries (post-filter)
        var linkedDocIds = ResolveLinkedDocs(dslParser);

        var luceneQuery = dslParser.LuceneQuery ?? new MatchAllDocsQuery();
        var topDocs = searcher.Search(luceneQuery, maxResults * 3); // over-fetch for post-filtering

        // Build highlighter
        var formatter = new SimpleHTMLFormatter("<em>", "</em>");
        var scorer = new QueryScorer(luceneQuery);
        var highlighter = new Highlighter(formatter, scorer) { TextFragmenter = new SimpleFragmenter(150) };

        var hits = new List<SearchHit>();
        foreach (var scoreDoc in topDocs.ScoreDocs)
        {
            var doc = searcher.Doc(scoreDoc.Doc);
            var id = doc.Get(IndexSchema.Id);

            // Post-filter by linked docs
            if (linkedDocIds is not null && !linkedDocIds.Contains(id))
                continue;

            // Build snippet
            string? snippet = null;
            var content = doc.Get(IndexSchema.Content);
            if (content is not null)
            {
                try
                {
                    var tokenStream = _indexer.Analyzer.GetTokenStream(IndexSchema.Content, content);
                    snippet = highlighter.GetBestFragment(tokenStream, content);
                }
                catch
                {
                    snippet = content.Length > 200 ? content[..200] + "..." : content;
                }
            }

            var hit = new SearchHit
            {
                Id = id,
                Name = doc.Get(IndexSchema.Name) ?? id,
                Score = scoreDoc.Score,
                FileType = doc.Get(IndexSchema.FileType),
                Date = DateTime.TryParse(doc.Get(IndexSchema.Date), out var d) ? d : null,
                Snippet = snippet,
                Entities = doc.GetValues(IndexSchema.Entities)?.ToList() ?? [],
                Tracks = doc.GetValues(IndexSchema.Tracks)?.ToList() ?? [],
                Themes = doc.GetValues(IndexSchema.Themes)?.ToList() ?? []
            };

            // Attach links from in-memory model
            var memDoc = _indexManager.Documents.FirstOrDefault(md => md.Id == id);
            if (memDoc is not null)
            {
                hit.Category = memDoc.TaxonomyAssignments
                    .GetValueOrDefault("category")?.FirstOrDefault();

                hit.Links = memDoc.Links
                    .OrderByDescending(l => l.Strength)
                    .Take(5)
                    .Select(l => new SearchHitLink
                    {
                        TargetId = l.TargetId,
                        LinkType = l.LinkType,
                        Strength = l.Strength,
                        Via = l.Via,
                        Reason = l.Reason
                    })
                    .ToList();
            }

            hits.Add(hit);
            if (hits.Count >= maxResults) break;
        }

        // Build facets from results
        var facets = BuildFacets(hits, dslParser.RequestedFacets, dslParser.FacetTop);

        sw.Stop();

        return new SearchResponse
        {
            Query = queryString,
            TotalHits = topDocs.TotalHits,
            TookMs = sw.ElapsedMilliseconds,
            Results = hits,
            Facets = facets
        };
    }

    private HashSet<string>? ResolveLinkedDocs(DslQueryParser dsl)
    {
        if (dsl.LinkedDocId is null && dsl.NeighborsOf is null) return null;

        var targetId = dsl.LinkedDocId ?? dsl.NeighborsOf!;
        var memDoc = _indexManager.Documents.FirstOrDefault(d => d.Id == targetId);
        if (memDoc is null) return [];

        var result = new HashSet<string>();
        var depth = dsl.NeighborsOf is not null ? dsl.NeighborDepth : 1;
        CollectNeighbors(targetId, depth, result);

        // Apply link type/via/strength filters
        if (dsl.LinkType is not null || dsl.LinkVia is not null || dsl.MinLinkStrength is not null)
        {
            var filtered = new HashSet<string>();
            foreach (var doc in _indexManager.Documents.Where(d => result.Contains(d.Id)))
            {
                foreach (var link in doc.Links.Where(l => l.TargetId == targetId || l.SourceId == targetId))
                {
                    if (dsl.LinkType is not null && link.LinkType != dsl.LinkType) continue;
                    if (dsl.LinkVia is not null && !link.Via.Contains(dsl.LinkVia)) continue;
                    if (dsl.MinLinkStrength is not null && link.Strength < dsl.MinLinkStrength.Value) continue;
                    filtered.Add(doc.Id);
                }
            }
            return filtered;
        }

        return result;
    }

    private void CollectNeighbors(string docId, int depth, HashSet<string> result)
    {
        if (depth <= 0) return;
        var doc = _indexManager.Documents.FirstOrDefault(d => d.Id == docId);
        if (doc is null) return;

        foreach (var link in doc.Links)
        {
            var neighborId = link.TargetId == docId ? link.SourceId : link.TargetId;
            if (result.Add(neighborId))
                CollectNeighbors(neighborId, depth - 1, result);
        }
    }

    private static Dictionary<string, List<FacetValue>> BuildFacets(
        List<SearchHit> hits,
        List<string> requestedFacets,
        int top)
    {
        // Default facets if none requested
        var facetFields = requestedFacets.Count > 0
            ? requestedFacets
            : new List<string> { "entity", "track", "theme" };

        var facets = new Dictionary<string, List<FacetValue>>();

        foreach (var field in facetFields)
        {
            var counts = new Dictionary<string, int>();
            foreach (var hit in hits)
            {
                var values = field.ToLowerInvariant() switch
                {
                    "entity" => hit.Entities,
                    "track" => hit.Tracks,
                    "theme" => hit.Themes,
                    "category" => hit.Category is not null ? [hit.Category] : [],
                    "type" => hit.FileType is not null ? [hit.FileType] : [],
                    _ => new List<string>()
                };

                foreach (var v in values)
                    counts[v] = counts.GetValueOrDefault(v) + 1;
            }

            facets[field] = counts
                .OrderByDescending(kv => kv.Value)
                .Take(top)
                .Select(kv => new FacetValue { Id = kv.Key, Count = kv.Value })
                .ToList();
        }

        return facets;
    }
}
