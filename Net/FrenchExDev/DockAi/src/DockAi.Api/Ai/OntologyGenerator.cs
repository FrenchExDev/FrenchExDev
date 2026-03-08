using DockAi.Api.Dsl;
using DockAi.Api.Models;
using DockAi.Api.Parsing;

namespace DockAi.Api.Ai;

/// <summary>
/// Multi-pass AI pipeline: documents → proposed ontology.
/// Pass 1: Corpus summary
/// Pass 2: Entity type discovery
/// Pass 3: Entity extraction (batched)
/// Pass 4: Relation type discovery
/// Pass 5: Relation extraction (batched)
/// Pass 6: Taxonomy generation
/// Pass 7: Classification rule generation
/// </summary>
public sealed class OntologyGenerator
{
    private readonly IAiProvider _ai;
    private readonly ParserFactory _parsers;
    private readonly ILogger<OntologyGenerator> _logger;

    private const string SystemJson = "Return valid JSON only. No markdown wrapping, no explanation text before or after.";
    private const string SystemAnalysis = "You are analyzing a document corpus to design a knowledge graph ontology. Be precise and concise.";

    public OntologyGenerator(IAiProvider ai, ParserFactory parsers, ILogger<OntologyGenerator> logger)
    {
        _ai = ai;
        _parsers = parsers;
        _logger = logger;
    }

    public async Task<GenerationResult> GenerateAsync(
        string sourceDir,
        GenerationOptions? options = null,
        CancellationToken ct = default)
    {
        options ??= new();
        var result = new GenerationResult();

        // Step 0: Sample documents
        _logger.LogInformation("Step 0: Sampling documents from {Dir}", sourceDir);
        var samples = SampleDocuments(sourceDir, options.MaxSampleDocs);
        result.SampledFiles = samples.Select(s => s.FileName).ToList();
        _logger.LogInformation("Sampled {Count} documents", samples.Count);

        // Step 1: Corpus summary
        _logger.LogInformation("Step 1: Summarizing corpus");
        result.CorpusSummary = await SummarizeCorpusAsync(samples, options.DomainHint, ct);

        // Step 2: Discover entity types
        _logger.LogInformation("Step 2: Discovering entity types");
        result.ProposedTypes = await DiscoverEntityTypesAsync(samples, result.CorpusSummary, ct);
        _logger.LogInformation("Discovered {Count} entity types", result.ProposedTypes.Count);

        // Step 3: Extract entities
        _logger.LogInformation("Step 3: Extracting entities");
        result.ProposedEntities = await ExtractEntitiesAsync(samples, result.ProposedTypes, ct);
        _logger.LogInformation("Extracted {Count} entities", result.ProposedEntities.Count);

        if (options.IncludeRelations)
        {
            // Step 4: Discover relation types
            _logger.LogInformation("Step 4: Discovering relation types");
            result.ProposedRelationTypes = await DiscoverRelationTypesAsync(
                result.ProposedTypes, result.ProposedEntities, result.CorpusSummary, ct);
            _logger.LogInformation("Discovered {Count} relation types", result.ProposedRelationTypes.Count);

            // Step 5: Extract relations
            _logger.LogInformation("Step 5: Extracting relations");
            result.ProposedRelations = await ExtractRelationsAsync(
                samples, result.ProposedEntities, result.ProposedRelationTypes, ct);
            _logger.LogInformation("Extracted {Count} relations", result.ProposedRelations.Count);
        }

        if (options.IncludeTaxonomies)
        {
            // Step 6: Generate taxonomies
            _logger.LogInformation("Step 6: Generating taxonomies");
            result.ProposedTaxonomies = await GenerateTaxonomiesAsync(
                samples, result.ProposedTypes, result.CorpusSummary, ct);
            _logger.LogInformation("Generated {Count} taxonomies", result.ProposedTaxonomies.Count);
        }

        if (options.IncludeRules && options.IncludeTaxonomies)
        {
            // Step 7: Generate rules
            _logger.LogInformation("Step 7: Generating classification rules");
            result.ProposedRules = await GenerateRulesAsync(
                samples, result.ProposedTaxonomies, ct);
            _logger.LogInformation("Generated {Count} rules", result.ProposedRules.Count);
        }

        // Step 8: Assemble
        _logger.LogInformation("Step 8: Assembling OntologySchema");
        result.Schema = AssembleSchema(result);

        // Step 9: Emit DSL
        result.DslText = DslEmitter.Emit(result.Schema);
        _logger.LogInformation("Generation complete: {Len} chars of DSL", result.DslText.Length);

        return result;
    }

    // ── Step 0: Intelligent document sampling ──

    private List<DocumentSample> SampleDocuments(string sourceDir, int maxDocs)
    {
        var files = Directory.GetFiles(sourceDir, "*.*", SearchOption.AllDirectories)
            .Where(f => _parsers.CanParse(f))
            .ToList();

        if (files.Count == 0) return [];

        // Sample across file types for representative mix
        var byType = files.GroupBy(f => Path.GetExtension(f).ToLowerInvariant()).ToList();
        var samples = new List<DocumentSample>();

        foreach (var group in byType)
        {
            var take = Math.Max(1, maxDocs * group.Count() / files.Count);
            foreach (var file in group.OrderBy(_ => Random.Shared.Next()).Take(take))
            {
                try
                {
                    var parser = _parsers.GetParser(file);
                    if (parser is null) continue;

                    var content = parser.Parse(file);
                    // Truncate to fit in LLM context (~2000 tokens ≈ 8000 chars)
                    if (content.Length > 8000)
                        content = content[..8000];

                    samples.Add(new DocumentSample(
                        Path.GetFileName(file),
                        Path.GetExtension(file).ToLowerInvariant(),
                        content));

                    if (samples.Count >= maxDocs) return samples;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to sample {File}", file);
                }
            }
        }

        return samples;
    }

    // ── Step 1: Corpus summary ──

    private async Task<string> SummarizeCorpusAsync(
        List<DocumentSample> samples, string? domainHint, CancellationToken ct)
    {
        var fileList = string.Join("\n", samples.Select(s => $"- {s.FileName} ({s.Extension})"));
        var excerpts = string.Join("\n---\n",
            samples.Take(5).Select(s => $"## {s.FileName}\n{s.Content[..Math.Min(s.Content.Length, 2000)]}"));

        var hintLine = domainHint is not null ? $"\nDomain hint: {domainHint}" : "";

        return await _ai.CompleteAsync($"""
            Analyze this document collection.{hintLine}

            File names ({samples.Count} total):
            {fileList}

            Excerpts from representative documents:
            {excerpts}

            Describe in 3-5 sentences:
            1. What is this collection about? (domain, purpose)
            2. What kinds of entities appear? (people, organizations, dates, events...)
            3. What relationships exist between entities?
            4. What categories/themes could organize these documents?

            Be specific and factual based on the content shown.
            """,
            system: SystemAnalysis,
            ct: ct);
    }

    // ── Step 2: Discover entity types ──

    private async Task<List<ProposedEntityType>> DiscoverEntityTypesAsync(
        List<DocumentSample> samples, string corpusSummary, CancellationToken ct)
    {
        var excerpts = BuildExcerpts(samples, 10, 1500);

        return await _ai.CompleteJsonAsync<List<ProposedEntityType>>($$"""
            Based on this document corpus:

            Summary: {{corpusSummary}}

            Document excerpts:
            {{excerpts}}

            Propose entity types (like "person", "organization", "event", "legal_reference").
            For each type, provide:
            - id: snake_case identifier
            - name: human-readable name
            - description: what this type represents
            - properties: list of { "name": "...", "kind": "...", "enum_values": [...] }
              where kind is one of: string, number, date, bool, string[], enum
            - Always include "name" (string) and "aliases" (string[]) properties

            Return a JSON array of entity types. Be comprehensive but not redundant.
            Only propose types that appear multiple times across documents.
            """,
            system: SystemJson,
            ct: ct) ?? [];
    }

    // ── Step 3: Extract entities ──

    private async Task<List<ProposedEntity>> ExtractEntitiesAsync(
        List<DocumentSample> samples, List<ProposedEntityType> types, CancellationToken ct)
    {
        var allEntities = new List<ProposedEntity>();
        var typeDesc = string.Join("\n", types.Select(t =>
            $"- {t.Id}: {t.Description} (properties: {string.Join(", ", t.Properties.Select(p => p.Name))})"));

        foreach (var batch in samples.Chunk(5))
        {
            var excerpts = string.Join("\n---\n",
                batch.Select(s => $"## {s.FileName}\n{s.Content}"));

            var extracted = await _ai.CompleteJsonAsync<List<ProposedEntity>>($$"""
                Given these entity types:
                {{typeDesc}}

                Extract all entity instances from these documents:
                {{excerpts}}

                For each entity:
                - id: unique snake_case identifier
                - type_id: which entity type it belongs to
                - properties: { "name": "...", "aliases": [...], ...other properties }

                Deduplicate: if the same entity appears in multiple documents,
                merge into one entry with combined aliases.

                Return JSON array. Only include entities clearly supported by the text.
                """,
                system: SystemJson,
                ct: ct);

            if (extracted is not null) allEntities.AddRange(extracted);
        }

        return DeduplicateEntities(allEntities);
    }

    // ── Step 4: Discover relation types ──

    private async Task<List<ProposedRelationType>> DiscoverRelationTypesAsync(
        List<ProposedEntityType> types, List<ProposedEntity> entities,
        string corpusSummary, CancellationToken ct)
    {
        var entitySample = string.Join(", ",
            entities.Take(20).Select(e => $"{e.Id}:{e.TypeId}"));

        return await _ai.CompleteJsonAsync<List<ProposedRelationType>>($"""
            Corpus: {corpusSummary}

            Entity types: {string.Join(", ", types.Select(t => t.Id))}
            Entity instances (sample): {entitySample}

            Propose relation types that connect entity types.
            For each:
            - id: snake_case verb (e.g. "employed_by", "filed_against")
            - from_type: source entity type id
            - to_types: list of target entity type ids
            - property_names: optional list of relation properties
            - inverse: optional inverse relation name
            - description: what this relation means

            Return JSON array. Only propose relations supported by document content.
            """,
            system: SystemJson,
            ct: ct) ?? [];
    }

    // ── Step 5: Extract relation instances ──

    private async Task<List<ProposedRelation>> ExtractRelationsAsync(
        List<DocumentSample> samples, List<ProposedEntity> entities,
        List<ProposedRelationType> relationTypes, CancellationToken ct)
    {
        var entityIndex = string.Join("\n",
            entities.Select(e => $"  {e.Id} ({e.TypeId}): {e.Properties.GetValueOrDefault("name")}"));
        var relTypeIndex = string.Join("\n",
            relationTypes.Select(r => $"  {r.Id}: {r.FromType} -> [{string.Join(", ", r.ToTypes)}]"));

        var allRelations = new List<ProposedRelation>();

        foreach (var batch in samples.Chunk(5))
        {
            var excerpts = string.Join("\n---\n",
                batch.Select(s => $"## {s.FileName}\n{s.Content}"));

            var extracted = await _ai.CompleteJsonAsync<List<ProposedRelation>>($$"""
                Known entities:
                {{entityIndex}}

                Relation types:
                {{relTypeIndex}}

                Documents:
                {{excerpts}}

                Extract relation instances. For each:
                - from_id: source entity id (must match a known entity)
                - type_id: relation type id (must match a known relation type)
                - to_id: target entity id (must match a known entity)
                - properties: { key: value } optional properties

                Return JSON array. Only include relations clearly stated or implied in text.
                """,
                system: SystemJson,
                ct: ct);

            if (extracted is not null) allRelations.AddRange(extracted);
        }

        return DeduplicateRelations(allRelations);
    }

    // ── Step 6: Generate taxonomies ──

    private async Task<List<ProposedTaxonomy>> GenerateTaxonomiesAsync(
        List<DocumentSample> samples, List<ProposedEntityType> types,
        string corpusSummary, CancellationToken ct)
    {
        var fileNames = string.Join("\n", samples.Select(s => $"- {s.FileName}"));

        return await _ai.CompleteJsonAsync<List<ProposedTaxonomy>>($$"""
            Corpus: {{corpusSummary}}
            Entity types: {{string.Join(", ", types.Select(t => t.Id))}}

            Documents:
            {{fileNames}}

            Design taxonomy trees for classifying these documents.
            A taxonomy is a hierarchical categorization scheme with faceted navigation.

            For each taxonomy:
            - id: snake_case identifier
            - label: human-readable name
            - facet: true (always, for search filtering)
            - nodes: array of { "id": "...", "label": "...", "description": "...", "color": "#hex", "children": [...] }

            Propose 3-6 useful taxonomies. Examples:
            - Document type/category (legal, financial, technical, correspondence...)
            - Subject matter / theme
            - Time period / phase
            - Urgency / priority

            Use distinct hex colors for sibling nodes. Return JSON array.
            """,
            system: SystemJson,
            ct: ct) ?? [];
    }

    // ── Step 7: Generate classification rules ──

    private async Task<List<ProposedRule>> GenerateRulesAsync(
        List<DocumentSample> samples, List<ProposedTaxonomy> taxonomies, CancellationToken ct)
    {
        var taxDesc = string.Join("\n",
            taxonomies.SelectMany(t =>
                FlattenProposedNodes(t.Nodes, t.Id)
                    .Select(n => $"  {t.Id}:{n.Id} - {n.Label}: {n.Description}")));

        var patterns = string.Join("\n",
            samples.Select(s => $"- {s.FileName}: {s.Content[..Math.Min(200, s.Content.Length)]}..."));

        return await _ai.CompleteJsonAsync<List<ProposedRule>>($"""
            Taxonomy nodes:
            {taxDesc}

            Sample documents (name + first 200 chars):
            {patterns}

            Generate regex-based classification rules.
            Each rule: when a document's content matches a regex pattern,
            assign it to a taxonomy node.

            For each rule:
            - id: unique snake_case name (e.g. "auto_track_penal")
            - pattern: regex pattern (case-insensitive, use | for alternation)
            - taxonomy_id: which taxonomy to assign
            - node_id: which node in that taxonomy
            - confidence: float 0.0-1.0 (how reliable is this pattern?)

            Patterns should be specific enough to avoid false positives.
            Return JSON array.
            """,
            system: SystemJson,
            ct: ct) ?? [];
    }

    // ── Step 8: Assemble into OntologySchema ──

    private OntologySchema AssembleSchema(GenerationResult result)
    {
        var builder = new OntologyBuilder();

        foreach (var t in result.ProposedTypes)
        {
            builder.Type(t.Id, b =>
            {
                foreach (var p in t.Properties)
                    b.Field(p.Name, MapKind(p.Kind), p.EnumValues);
            });
        }

        foreach (var e in result.ProposedEntities)
        {
            builder.Entity(e.Id, e.TypeId, b =>
            {
                foreach (var kv in e.Properties)
                    b.Set(kv.Key, kv.Value);
            });
        }

        foreach (var rt in result.ProposedRelationTypes)
        {
            builder.RelationType(rt.Id, b =>
            {
                b.From(rt.FromType).To(rt.ToTypes.ToArray());
                if (rt.Inverse is not null) b.Inverse(rt.Inverse);
                if (rt.PropertyNames is { Count: > 0 }) b.Properties(rt.PropertyNames.ToArray());
            });
        }

        foreach (var r in result.ProposedRelations)
        {
            builder.Relation(r.FromId, r.TypeId, r.ToId, b =>
            {
                foreach (var kv in r.Properties)
                    b.Set(kv.Key, kv.Value);
            });
        }

        foreach (var tax in result.ProposedTaxonomies)
        {
            builder.Taxonomy(tax.Id, tax.Label, b =>
            {
                if (tax.Facet) b.Facet();
                foreach (var node in tax.Nodes)
                    AddNodeRecursive(b, node);
            });
        }

        foreach (var rule in result.ProposedRules)
        {
            builder.Rule(rule.Id, b => b
                .When(rule.Pattern)
                .ThenAssign(rule.TaxonomyId, rule.NodeId)
                .Confidence(rule.Confidence));
        }

        return builder.Build();
    }

    // ── Helpers ──

    private static string BuildExcerpts(List<DocumentSample> samples, int maxDocs, int maxCharsPerDoc)
    {
        return string.Join("\n---\n",
            samples.Take(maxDocs)
                .Select(s => $"## {s.FileName}\n{s.Content[..Math.Min(s.Content.Length, maxCharsPerDoc)]}"));
    }

    private static List<ProposedEntity> DeduplicateEntities(List<ProposedEntity> entities)
    {
        var byId = new Dictionary<string, ProposedEntity>();
        foreach (var e in entities)
        {
            if (!byId.TryGetValue(e.Id, out var existing))
            {
                byId[e.Id] = e;
                continue;
            }

            // Merge properties (keep existing, add new)
            foreach (var kv in e.Properties)
            {
                if (!existing.Properties.ContainsKey(kv.Key))
                    existing.Properties[kv.Key] = kv.Value;
            }
        }
        return byId.Values.ToList();
    }

    private static List<ProposedRelation> DeduplicateRelations(List<ProposedRelation> relations)
    {
        var seen = new HashSet<string>();
        var unique = new List<ProposedRelation>();
        foreach (var r in relations)
        {
            var key = $"{r.FromId}|{r.TypeId}|{r.ToId}";
            if (seen.Add(key))
                unique.Add(r);
        }
        return unique;
    }

    private static PropertyKind MapKind(string kind) => kind.ToLowerInvariant() switch
    {
        "string" => PropertyKind.String,
        "number" => PropertyKind.Number,
        "date" => PropertyKind.Date,
        "bool" or "boolean" => PropertyKind.Bool,
        "string[]" or "array" => PropertyKind.StringArray,
        "enum" => PropertyKind.Enum,
        _ => PropertyKind.String
    };

    private static void AddNodeRecursive(TaxonomyBuilder tb, ProposedTaxonomyNode node)
    {
        tb.Node(node.Id, node.Label, nb =>
        {
            if (node.Description is not null) nb.Desc(node.Description);
            if (node.Color is not null) nb.Color(node.Color);
            if (node.Children is not null)
            {
                foreach (var child in node.Children)
                    AddChildRecursive(nb, child);
            }
        });
    }

    private static void AddChildRecursive(TaxonomyNodeBuilder nb, ProposedTaxonomyNode node)
    {
        nb.Child(node.Id, node.Label, cnb =>
        {
            if (node.Description is not null) cnb.Desc(node.Description);
            if (node.Color is not null) cnb.Color(node.Color);
            if (node.Children is not null)
            {
                foreach (var child in node.Children)
                    AddChildRecursive(cnb, child);
            }
        });
    }

    private static IEnumerable<ProposedTaxonomyNode> FlattenProposedNodes(
        List<ProposedTaxonomyNode> nodes, string prefix)
    {
        foreach (var n in nodes)
        {
            yield return n;
            if (n.Children is not null)
            {
                foreach (var child in FlattenProposedNodes(n.Children, prefix))
                    yield return child;
            }
        }
    }
}
