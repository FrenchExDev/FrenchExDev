using System.IO.Compression;
using DockAi.Api.Ai;
using DockAi.Api.Analysis;
using DockAi.Api.Dsl;
using DockAi.Api.Indexing;
using DockAi.Api.Ontology;
using DockAi.Api.Parsing;
using DockAi.Api.Search;

var builder = WebApplication.CreateBuilder(args);

// CLI: --input <dir> --output <dir> --max-files <int>
var inputDir = builder.Configuration.GetValue<string>("input");

var outputRoot = builder.Configuration.GetValue<string>("output")
    ?? Path.Combine(Directory.GetCurrentDirectory(), "output");

var maxFiles = builder.Configuration.GetValue<int?>("max-files");
var maxParallel = builder.Configuration.GetValue<int?>("max-parallel") ?? 3;

var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HHmmss");
var dataDir = Path.Combine(outputRoot, timestamp);
var indexDir = Path.Combine(dataDir, "index");

Directory.CreateDirectory(dataDir);
Directory.CreateDirectory(indexDir);

// Register services
builder.Services.AddSingleton<OntologyStore>();
builder.Services.AddSingleton<TaxonomyStore>();
builder.Services.AddSingleton<ParserFactory>();
builder.Services.AddSingleton(sp => new LuceneIndexer(indexDir));
builder.Services.AddSingleton(sp =>
{
    var ontology = sp.GetRequiredService<OntologyStore>();
    var taxonomy = sp.GetRequiredService<TaxonomyStore>();
    var logger = sp.GetRequiredService<ILogger<OntologyManager>>();
    return new OntologyManager(ontology, taxonomy, dataDir, logger);
});
builder.Services.AddSingleton(sp => new EntityExtractor(sp.GetRequiredService<OntologyStore>()));
builder.Services.AddSingleton<RuleEngine>();
builder.Services.AddSingleton<LinkBuilder>();
builder.Services.AddSingleton(sp => new IndexManager(
    sp.GetRequiredService<ParserFactory>(),
    sp.GetRequiredService<EntityExtractor>(),
    sp.GetRequiredService<RuleEngine>(),
    sp.GetRequiredService<LinkBuilder>(),
    sp.GetRequiredService<LuceneIndexer>(),
    sp.GetRequiredService<ILogger<IndexManager>>()));
builder.Services.AddSingleton(sp => new SearchService(
    sp.GetRequiredService<LuceneIndexer>(),
    sp.GetRequiredService<IndexManager>(),
    sp.GetRequiredService<ILogger<SearchService>>()));

// AI provider registration
var aiOptions = builder.Configuration.GetSection("Ai").Get<AiProviderOptions>() ?? new AiProviderOptions();
builder.Services.AddSingleton(aiOptions);

if (!string.IsNullOrEmpty(aiOptions.ApiKey))
{
    switch (aiOptions.Provider.ToLowerInvariant())
    {
        case "openai":
            builder.Services.AddHttpClient<IAiProvider, OpenAiProvider>()
                .ConfigureHttpClient(c => c.Timeout = TimeSpan.FromMinutes(5));
            break;
        case "ollama":
            builder.Services.AddHttpClient<IAiProvider, OllamaProvider>()
                .ConfigureHttpClient(c => c.Timeout = TimeSpan.FromMinutes(5));
            break;
        default: // claude
            builder.Services.AddHttpClient<IAiProvider, ClaudeProvider>()
                .ConfigureHttpClient(c => c.Timeout = TimeSpan.FromMinutes(5));
            break;
    }
    builder.Services.AddSingleton<OntologyGenerator>();
}

// CORS for local dev
builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

var app = builder.Build();
app.UseCors();

// Load ontology at startup
var ontologyManager = app.Services.GetRequiredService<OntologyManager>();
ontologyManager.Load();

// Initialize entity extractor patterns from loaded ontology
var entityExtractor = app.Services.GetRequiredService<EntityExtractor>();
entityExtractor.BuildPatterns();

// Initialize rule engine
var ruleEngine = app.Services.GetRequiredService<RuleEngine>();
ruleEngine.LoadRules(ontologyManager.Rules);

// ────────────────────────────────────────
// Mode: --input provided → batch process and exit
//       no --input        → run as API server
// ────────────────────────────────────────

if (inputDir is not null)
{
    var resolvedInput = Path.GetFullPath(inputDir);
    if (!Directory.Exists(resolvedInput))
    {
        Console.Error.WriteLine($"Input directory not found: {resolvedInput}");
        return;
    }

    Console.WriteLine($"Processing: {resolvedInput}");
    Console.WriteLine($"Output:     {dataDir}");
    if (maxFiles.HasValue) Console.WriteLine($"Max files:  {maxFiles.Value}");
    Console.WriteLine($"Parallel:   {maxParallel}");

    // Step 1: Index documents (parse, extract entities, build links)
    var indexManager = app.Services.GetRequiredService<IndexManager>();
    var result = indexManager.Reindex(resolvedInput, maxFiles);
    Console.WriteLine($"Indexed {result.DocumentCount} documents in {result.TookMs}ms ({result.ErrorCount} errors)");
    foreach (var err in result.Errors) Console.WriteLine($"  ERROR: {err}");

    // Step 2: AI — ontology generation + per-document narratives
    var generator = app.Services.GetService<OntologyGenerator>();
    var aiProvider = app.Services.GetService<IAiProvider>();

    if (generator is null || aiProvider is null)
    {
        Console.Error.WriteLine("AI provider not configured — skipping narrative generation. Set Ai:ApiKey in appsettings.");
    }
    else
    {
        // 2a: Ontology generation
        Console.WriteLine("Generating ontology...");
        var genResult = await generator.GenerateAsync(resolvedInput);
        if (genResult.DslText is not null)
        {
            var dslPath = Path.Combine(dataDir, "ontology.dsl");
            await File.WriteAllTextAsync(dslPath, genResult.DslText);
            Console.WriteLine($"Ontology saved to {dslPath}");
        }

        var summaryPath = Path.Combine(dataDir, "corpus_summary.md");
        await File.WriteAllTextAsync(summaryPath, $"""
            # Corpus Summary

            {genResult.CorpusSummary}

            ## Sampled Files
            {string.Join("\n", genResult.SampledFiles.Select(f => $"- {f}"))}

            ## Entity Types ({genResult.ProposedTypes.Count})
            {string.Join("\n", genResult.ProposedTypes.Select(t => $"- **{t.Name}** ({t.Id}): {t.Description}"))}

            ## Entities ({genResult.ProposedEntities.Count})
            {string.Join("\n", genResult.ProposedEntities.Select(e => $"- {e.Id} ({e.TypeId})"))}

            ## Relation Types ({genResult.ProposedRelationTypes.Count})
            {string.Join("\n", genResult.ProposedRelationTypes.Select(r => $"- {r.Id}: {r.FromType} → [{string.Join(", ", r.ToTypes)}]"))}

            ## Taxonomies ({genResult.ProposedTaxonomies.Count})
            {string.Join("\n", genResult.ProposedTaxonomies.Select(t => $"- **{t.Label}** ({t.Id})"))}
            """);
        Console.WriteLine($"Corpus summary saved to {summaryPath}");

        // 2b: Per-document narratives (parallel)
        Console.WriteLine($"Generating per-document narratives ({maxParallel} parallel)...");
        var narrativesDir = Path.Combine(dataDir, "narratives");
        Directory.CreateDirectory(narrativesDir);

        await Parallel.ForEachAsync(
            indexManager.Documents,
            new ParallelOptions { MaxDegreeOfParallelism = maxParallel },
            async (doc, ct) =>
        {
            try
            {
                Console.WriteLine($"  Sending: {doc.FileName} ({doc.Content.Length} chars)");
                var content = doc.Content;
                if (content.Length > 8000)
                    content = content[..8000];

                var narrative = await aiProvider.CompleteAsync($"""
                    Analyze this document and write a narrative summary.

                    Document: {doc.FileName}
                    Content:
                    {content}

                    Write a clear, structured summary covering:
                    1. What this document is about (purpose, context)
                    2. Key facts, entities, and dates mentioned
                    3. Important relationships or connections to other topics
                    4. Notable observations or insights

                    Write in flowing prose, not bullet points. Be factual and precise.
                    """,
                    system: "You are a document analyst. Write concise, factual narrative summaries.",
                    ct: ct);

                Console.WriteLine($"  Received: {doc.FileName} ({narrative.Length} chars)");
                var safeName = doc.Id + ".md";
                var narrativePath = Path.Combine(narrativesDir, safeName);
                await File.WriteAllTextAsync(narrativePath, $"""
                    # {doc.FileName}

                    {narrative}
                    """, ct);
                Console.WriteLine($"  Written:  {narrativePath}");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"  ERROR ({doc.FileName}): {ex.Message}");
            }
        });

        Console.WriteLine($"Narratives saved to {narrativesDir}");
    }

    Console.WriteLine("Done.");
    return;
}

// ────────────────────────────────────────
// API Endpoints (server mode)
// ────────────────────────────────────────

// POST /api/upload — Upload a zip of documents, extract, and index
app.MapPost("/api/upload", async (IFormFile file, IndexManager indexManager) =>
{
    if (file is null || file.Length == 0)
        return Results.BadRequest(new { error = "No file uploaded" });

    if (!file.FileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        return Results.BadRequest(new { error = "Only .zip files are accepted" });

    var uploadDir = Path.Combine(dataDir, "uploads", Path.GetFileNameWithoutExtension(file.FileName));
    if (Directory.Exists(uploadDir))
        Directory.Delete(uploadDir, true);
    Directory.CreateDirectory(uploadDir);

    var zipPath = Path.Combine(dataDir, "uploads", file.FileName);
    await using (var stream = new FileStream(zipPath, FileMode.Create))
        await file.CopyToAsync(stream);

    ZipFile.ExtractToDirectory(zipPath, uploadDir, overwriteFiles: true);
    File.Delete(zipPath);

    var result = indexManager.Reindex(uploadDir, maxFiles);
    return Results.Ok(result);
}).DisableAntiforgery();

// GET /api/search?q={dsl}&max={n}
app.MapGet("/api/search", (string? q, int? max, SearchService searchService) =>
{
    var query = q ?? "";
    var maxResults = max ?? 50;
    var result = searchService.Search(query, maxResults);
    return Results.Ok(result);
});

// GET /api/document/{id}
app.MapGet("/api/document/{id}", (string id, IndexManager indexManager) =>
{
    var doc = indexManager.Documents.FirstOrDefault(d => d.Id == id);
    if (doc is null) return Results.NotFound();
    return Results.Ok(new
    {
        doc.Id,
        doc.Name,
        doc.FileName,
        doc.FilePath,
        doc.FileType,
        doc.FileSize,
        doc.LastModified,
        doc.Entities,
        doc.TaxonomyAssignments,
        doc.Tags,
        links = doc.Links.Select(l => new
        {
            l.TargetId,
            l.LinkType,
            l.LinkName,
            l.Strength,
            l.Via,
            l.Reason
        }),
        contentPreview = doc.Content.Length > 1000 ? doc.Content[..1000] + "..." : doc.Content
    });
});

// GET /api/links/{id}
app.MapGet("/api/links/{id}", (string id, IndexManager indexManager) =>
{
    var doc = indexManager.Documents.FirstOrDefault(d => d.Id == id);
    if (doc is null) return Results.NotFound();
    return Results.Ok(doc.Links.OrderByDescending(l => l.Strength));
});

// GET /api/ontology — Full ontology snapshot
app.MapGet("/api/ontology", (OntologyManager mgr) => Results.Ok(mgr.GetOntologySnapshot()));

// GET /api/ontology/entities
app.MapGet("/api/ontology/entities", (OntologyStore store) =>
    Results.Ok(store.Entities.Values.Select(e => new { e.Id, e.TypeId, e.Name, e.Aliases })));

// GET /api/ontology/relations
app.MapGet("/api/ontology/relations", (OntologyStore store) => Results.Ok(store.Relations));

// GET /api/taxonomy/{name}
app.MapGet("/api/taxonomy/{name}", (string name, TaxonomyStore store) =>
{
    var tax = store.Get(name);
    if (tax is null) return Results.NotFound();
    return Results.Ok(new
    {
        tax.Id,
        tax.Label,
        tax.Facet,
        nodes = tax.AllNodes().Select(n => new
        {
            n.Id,
            n.Label,
            n.Description,
            n.Color,
            n.Icon,
            parentId = n.Parent?.Id,
            children = n.Children.Select(c => c.Id)
        })
    });
});

// GET /api/suggest?q={prefix} — Simple auto-complete from entity names
app.MapGet("/api/suggest", (string? q, OntologyStore ontology, TaxonomyStore taxonomy) =>
{
    if (string.IsNullOrWhiteSpace(q)) return Results.Ok(Array.Empty<object>());
    var prefix = q.ToLowerInvariant();

    var suggestions = new List<object>();

    // Entity suggestions
    foreach (var entity in ontology.Entities.Values)
    {
        if (entity.Id.Contains(prefix, StringComparison.OrdinalIgnoreCase) ||
            entity.Name.Contains(prefix, StringComparison.OrdinalIgnoreCase))
        {
            suggestions.Add(new { type = "entity", value = $"entity:{entity.Id}", label = entity.Name });
        }
    }

    // Taxonomy node suggestions
    foreach (var tax in taxonomy.Taxonomies.Values)
    {
        foreach (var node in tax.AllNodes())
        {
            if (node.Id.Contains(prefix, StringComparison.OrdinalIgnoreCase) ||
                node.Label.Contains(prefix, StringComparison.OrdinalIgnoreCase))
            {
                suggestions.Add(new { type = tax.Id, value = $"{tax.Id}:{node.Id}", label = node.Label });
            }
        }
    }

    return Results.Ok(suggestions.Take(20));
});

app.MapGet("/", () => Results.Content(
    "<h1>DockAi Search Engine</h1><p>Upload a .zip via POST /api/upload, then GET /api/search?q=your+query</p>",
    "text/html"));

// ────────────────────────────────────────
// AI Ontology Generation Endpoints
// ────────────────────────────────────────

// POST /api/ai/generate — Generate ontology from uploaded documents
app.MapPost("/api/ai/generate", async (OntologyGenerator? generator, string? hint) =>
{
    if (generator is null)
        return Results.Problem("AI provider not configured. Set Ai:ApiKey in appsettings or environment.");

    var uploadsDir = Path.Combine(dataDir, "uploads");
    if (!Directory.Exists(uploadsDir))
        return Results.BadRequest(new { error = "No documents uploaded yet" });

    var options = new GenerationOptions { DomainHint = hint };
    var result = await generator.GenerateAsync(uploadsDir, options);

    return Results.Ok(new
    {
        summary = result.CorpusSummary,
        sampledFiles = result.SampledFiles,
        stats = new
        {
            entityTypes = result.ProposedTypes.Count,
            entities = result.ProposedEntities.Count,
            relationTypes = result.ProposedRelationTypes.Count,
            relations = result.ProposedRelations.Count,
            taxonomies = result.ProposedTaxonomies.Count,
            rules = result.ProposedRules.Count
        },
        dsl = result.DslText
    });
});

// GET /api/ai/generate/preview — Preview DSL without applying
app.MapGet("/api/ai/generate/preview", async (OntologyGenerator? generator, string? hint) =>
{
    if (generator is null)
        return Results.Problem("AI provider not configured. Set Ai:ApiKey in appsettings or environment.");

    var uploadsDir = Path.Combine(dataDir, "uploads");
    if (!Directory.Exists(uploadsDir))
        return Results.BadRequest(new { error = "No documents uploaded yet" });

    var options = new GenerationOptions { DomainHint = hint };
    var result = await generator.GenerateAsync(uploadsDir, options);
    return Results.Content(result.DslText, "text/plain");
});

// POST /api/ai/generate/apply — Generate + save DSL + reindex
app.MapPost("/api/ai/generate/apply", async (
    OntologyGenerator? generator,
    OntologyManager mgr,
    EntityExtractor extractor,
    RuleEngine rules,
    IndexManager indexMgr) =>
{
    if (generator is null)
        return Results.Problem("AI provider not configured. Set Ai:ApiKey in appsettings or environment.");

    var uploadsDir = Path.Combine(dataDir, "uploads");
    if (!Directory.Exists(uploadsDir))
        return Results.BadRequest(new { error = "No documents uploaded yet" });

    var result = await generator.GenerateAsync(uploadsDir);
    if (result.Schema is null)
        return Results.Problem("Generation failed — no schema produced");

    // Save generated DSL file
    var dslPath = Path.Combine(dataDir, "ai_generated.dsl");
    await File.WriteAllTextAsync(dslPath, result.DslText);

    // Reload ontology (picks up new .dsl file)
    mgr.Load();
    extractor.BuildPatterns();
    rules.LoadRules(mgr.Rules);

    // Re-index from uploads
    var indexResult = indexMgr.Reindex(uploadsDir, maxFiles);

    return Results.Ok(new
    {
        saved = dslPath,
        stats = new
        {
            entityTypes = result.ProposedTypes.Count,
            entities = result.ProposedEntities.Count,
            taxonomies = result.ProposedTaxonomies.Count,
            rules = result.ProposedRules.Count
        },
        indexResult
    });
});

app.Run();
