using Lucene.Net.Analysis.Standard;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.Store;
using Lucene.Net.Util;
using AppDocument = DockAi.Api.Models.Document;

namespace DockAi.Api.Indexing;

/// <summary>
/// Writes documents into a Lucene index.
/// </summary>
public sealed class LuceneIndexer : IDisposable
{
    private const LuceneVersion AppVersion = LuceneVersion.LUCENE_48;
    private readonly FSDirectory _directory;
    private readonly StandardAnalyzer _analyzer;
    // Writer is created on-demand in RebuildIndex

    public Lucene.Net.Store.Directory LuceneDirectory => _directory;
    public StandardAnalyzer Analyzer => _analyzer;

    public LuceneIndexer(string indexPath)
    {
        _directory = FSDirectory.Open(indexPath);
        _analyzer = new StandardAnalyzer(AppVersion);
    }

    public void RebuildIndex(IEnumerable<AppDocument> documents)
    {
        var config = new IndexWriterConfig(AppVersion, _analyzer)
        {
            OpenMode = OpenMode.CREATE // Wipe and recreate
        };

        using var writer = new IndexWriter(_directory, config);

        foreach (var doc in documents)
        {
            var lucDoc = new Lucene.Net.Documents.Document();

            // Stored fields
            lucDoc.Add(new StringField(IndexSchema.Id, doc.Id, Field.Store.YES));
            lucDoc.Add(new StringField(IndexSchema.Name, doc.Name, Field.Store.YES));
            lucDoc.Add(new StringField(IndexSchema.FileName, doc.FileName, Field.Store.YES));
            lucDoc.Add(new StringField(IndexSchema.FilePath, doc.FilePath, Field.Store.YES));
            lucDoc.Add(new StringField(IndexSchema.FileType, doc.FileType, Field.Store.YES));
            lucDoc.Add(new Int64Field(IndexSchema.FileSize, doc.FileSize, Field.Store.YES));
            lucDoc.Add(new StringField(IndexSchema.Date, doc.LastModified.ToString("yyyy-MM-dd"), Field.Store.YES));

            // Full-text searchable content
            lucDoc.Add(new TextField(IndexSchema.Content, doc.Content, Field.Store.YES));

            // Multi-valued facet/filter fields
            foreach (var entity in doc.Entities)
                lucDoc.Add(new StringField(IndexSchema.Entities, entity, Field.Store.YES));

            foreach (var tag in doc.Tags)
                lucDoc.Add(new StringField(IndexSchema.Tags, tag, Field.Store.YES));

            // Taxonomy assignments
            if (doc.TaxonomyAssignments.TryGetValue("track", out var tracks))
                foreach (var t in tracks)
                    lucDoc.Add(new StringField(IndexSchema.Tracks, t, Field.Store.YES));

            if (doc.TaxonomyAssignments.TryGetValue("theme", out var themes))
                foreach (var t in themes)
                    lucDoc.Add(new StringField(IndexSchema.Themes, t, Field.Store.YES));

            if (doc.TaxonomyAssignments.TryGetValue("category", out var cats))
                foreach (var c in cats)
                    lucDoc.Add(new StringField(IndexSchema.Categories, c, Field.Store.YES));

            // Symbolic link targets
            foreach (var link in doc.Links)
            {
                lucDoc.Add(new StringField(IndexSchema.LinkedDocs, link.TargetId, Field.Store.YES));
                lucDoc.Add(new StringField(IndexSchema.LinkTypes, link.LinkType, Field.Store.YES));
            }

            writer.AddDocument(lucDoc);
        }

        writer.Commit();
    }

    public void Dispose()
    {
        _analyzer.Dispose();
        _directory.Dispose();
    }
}
