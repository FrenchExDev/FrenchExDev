using DockAi.Api.Analysis;
using DockAi.Api.Models;
using DockAi.Api.Ontology;
using DockAi.Api.Parsing;

namespace DockAi.Api.Indexing;

/// <summary>
/// Orchestrates the full indexing pipeline: parse → extract entities → classify → build links → index.
/// </summary>
public sealed class IndexManager
{
    private readonly ParserFactory _parsers;
    private readonly EntityExtractor _entityExtractor;
    private readonly RuleEngine _ruleEngine;
    private readonly LinkBuilder _linkBuilder;
    private readonly LuceneIndexer _indexer;
    private readonly ILogger<IndexManager> _logger;

    /// <summary>All documents after last indexing run.</summary>
    public List<Document> Documents { get; private set; } = [];

    public IndexManager(
        ParserFactory parsers,
        EntityExtractor entityExtractor,
        RuleEngine ruleEngine,
        LinkBuilder linkBuilder,
        LuceneIndexer indexer,
        ILogger<IndexManager> logger)
    {
        _parsers = parsers;
        _entityExtractor = entityExtractor;
        _ruleEngine = ruleEngine;
        _linkBuilder = linkBuilder;
        _indexer = indexer;
        _logger = logger;
    }

    /// <summary>
    /// Scan the source directory, parse all supported files, enrich, and index.
    /// </summary>
    public IndexResult Reindex(string sourceDir, int? maxFiles = null)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var docs = new List<Document>();
        var errors = new List<string>();

        var allFiles = Directory.GetFiles(sourceDir, "*.*", SearchOption.AllDirectories);
        var files = maxFiles.HasValue ? allFiles.Take(maxFiles.Value).ToArray() : allFiles;
        _logger.LogInformation("Found {Total} files in {Dir}, processing {Count}", allFiles.Length, sourceDir, files.Length);

        foreach (var file in files)
        {
            if (!_parsers.CanParse(file)) continue;

            try
            {
                var parser = _parsers.GetParser(file)!;
                var content = parser.Parse(file);

                var fi = new FileInfo(file);
                var id = GenerateId(file, sourceDir);
                var doc = new Document
                {
                    Id = id,
                    Name = Path.GetFileNameWithoutExtension(fi.Name),
                    FileName = fi.Name,
                    FilePath = file,
                    FileType = fi.Extension.TrimStart('.').ToLowerInvariant(),
                    FileSize = fi.Length,
                    LastModified = fi.LastWriteTimeUtc,
                    Content = content
                };

                // Entity extraction
                _entityExtractor.Extract(doc);

                // Rule-based classification
                _ruleEngine.Apply(doc);

                docs.Add(doc);
            }
            catch (Exception ex)
            {
                errors.Add($"{Path.GetFileName(file)}: {ex.Message}");
                _logger.LogWarning(ex, "Failed to parse {File}", file);
            }
        }

        // Build symbolic links across all documents
        _linkBuilder.BuildLinks(docs);

        // Index into Lucene
        _indexer.RebuildIndex(docs);
        Documents = docs;

        sw.Stop();
        _logger.LogInformation("Indexed {Count} documents in {Ms}ms ({Errors} errors)",
            docs.Count, sw.ElapsedMilliseconds, errors.Count);

        return new IndexResult
        {
            DocumentCount = docs.Count,
            ErrorCount = errors.Count,
            Errors = errors,
            TookMs = sw.ElapsedMilliseconds
        };
    }

    private static string GenerateId(string filePath, string sourceDir)
    {
        var relative = Path.GetRelativePath(sourceDir, filePath);
        return relative
            .Replace('\\', '_')
            .Replace('/', '_')
            .Replace(' ', '_')
            .Replace("(", "")
            .Replace(")", "")
            .Replace(".", "_")
            .ToLowerInvariant()
            .TrimEnd('_');
    }
}

public sealed class IndexResult
{
    public int DocumentCount { get; init; }
    public int ErrorCount { get; init; }
    public List<string> Errors { get; init; } = [];
    public long TookMs { get; init; }
}
