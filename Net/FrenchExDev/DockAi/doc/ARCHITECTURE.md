# DockAi -- Architecture

## 1. Overview

DockAi is a two-tier document analysis platform: an API backend (DockAi.Api) handles parsing, indexing, entity extraction, ontology management, and AI-powered generation; a Viewer frontend (DockAi.Viewer) handles document rendering and proxies API calls.

The system processes document collections through a pipeline: parse -> extract entities -> classify -> build links -> index. It supports a custom DSL for defining ontology schemas and querying the index.

---

## 2. Project Structure

```
DockAi/
  src/
    DockAi.Api/                    (Backend — port 5108)
      Program.cs                   ASP.NET Core setup, endpoints, batch mode
      Models/                      Domain models (Document, Entity, Relation, etc.)
      Parsing/                     Multi-format text extraction
      Dsl/                         Lexer, schema parser, query parser
      Indexing/                    Lucene indexing pipeline
      Analysis/                   Entity extraction, link building, rule engine
      Ontology/                   In-memory graph stores + persistence
      Ai/                         LLM providers + ontology generation
      Search/                     DSL query execution via Lucene
    DockAi.Viewer/                 (Frontend — port 5112)
      Program.cs                   Rendering endpoints, API proxy
      Rendering/                   Format-specific HTML renderers
  doc/
    DSL_REFERENCE.md               Complete DSL syntax specification
```

---

## 3. Processing Pipeline

```
1. Document Upload     POST /api/upload (ZIP archive)
2. Text Extraction     ParserFactory → IDocumentParser per format
                       (PDF, DOCX, XLSX, CSV, MD, HTML, TXT)
3. Entity Extraction   EntityExtractor: compiled regexes from ontology aliases
4. Classification      RuleEngine: regex patterns → taxonomy node assignments
5. Link Building       LinkBuilder: Jaccard similarity on shared entities (≥ 0.15)
                       + shared taxonomy assignments
6. Lucene Indexing     LuceneIndexer: full-text + faceted fields
7. Search Ready        SearchService: DSL query → Lucene → results + facets
```

---

## 4. Component Architecture

### Parsing Layer

| Component | Purpose |
|-----------|---------|
| `IDocumentParser` | Interface: `Task<string> ParseAsync(Stream)` |
| `ParserFactory` | Selects parser by file extension |
| `PdfParser` | PdfPig-based extraction |
| `DocxParser` | OpenXml-based extraction |
| `XlsxParser` | ClosedXML-based extraction |
| `CsvParser` | CsvHelper-based extraction |
| `MarkdownParser` | Markdig-based extraction |
| `HtmlParser` | HtmlAgilityPack-based extraction |
| `TextParser` | Direct read |

### DSL Layer

| Component | Purpose |
|-----------|---------|
| `DslTokens` | Token type definitions |
| `DslLexer` | Tokenizer for DSL source |
| `DslSchemaParser` | Parses @type, @entity, @relation, @taxonomy, @rule |
| `DslQueryParser` | Parses search queries with filters and traversal |
| `OntologySchema` | Unified container for all parsed DSL components |
| `OntologyBuilder` | Programmatic schema construction API |

### Analysis Layer

| Component | Purpose |
|-----------|---------|
| `EntityExtractor` | Pre-compiled regex matching on entity aliases |
| `LinkBuilder` | Jaccard similarity + shared taxonomy → weighted edges |
| `RuleEngine` | Regex classification rules with confidence scores |

### Ontology Layer

| Component | Purpose |
|-----------|---------|
| `OntologyStore` | In-memory graph: types, entities, relations (thread-safe) |
| `TaxonomyStore` | Hierarchical taxonomy nodes with lookup |
| `OntologyManager` | DSL file loading, schema merging, JSON persistence |

### AI Layer

| Component | Purpose |
|-----------|---------|
| `IAiProvider` | Interface for LLM calls |
| `ClaudeProvider` | Anthropic Claude API |
| `OpenAiProvider` | OpenAI/GPT API |
| `OllamaProvider` | Local Ollama models |
| `OntologyGenerator` | Multi-pass pipeline: summarize → discover types → extract entities → discover relations → extract relations → generate taxonomies → generate rules → emit DSL |
| `DslEmitter` | Converts generation results to DSL syntax |

### Search Layer

| Component | Purpose |
|-----------|---------|
| `SearchService` | DSL query parsing → Lucene execution → result assembly with highlighting and facets |

---

## 5. Domain Models

```
Document
  |-- id, name, path, content (plain text)
  |-- entities: List<EntityMention>
  |-- taxonomyAssignments: List<TaxonomyAssignment>
  |-- tags: List<string>
  |-- links: List<SymbolicLink>

Entity
  |-- id, name, type (ref → EntityType)
  |-- aliases: string[]
  |-- properties: Dictionary<string, object>

EntityType
  |-- name, properties (schema)

Relation
  |-- from: Entity, to: Entity
  |-- type: RelationType
  |-- properties: Dictionary<string, object>

RelationType
  |-- name, from/to type constraints
  |-- inverse, symmetric, auto flags

Taxonomy
  |-- name, label, facet flag
  |-- nodes: tree of TaxonomyNode (label, color, icon)

ClassificationRule
  |-- name, regex pattern, action (assign/extract)
  |-- target taxonomy + node, confidence

SymbolicLink
  |-- source, target (document or entity)
  |-- weight (Jaccard), type, properties
```

---

## 6. API Endpoints

### Documents
- `POST /api/upload` -- upload ZIP, extract, and index all documents
- `GET /api/document/{id}` -- metadata, entities, taxonomy assignments
- `GET /api/links/{id}` -- symbolic links for a document

### Search
- `GET /api/search?q={dsl}&max={n}` -- DSL-based full-text search with facets

### Ontology
- `GET /api/ontology` -- full snapshot
- `GET /api/ontology/entities` -- entity list with types and aliases
- `GET /api/ontology/relations` -- all relations

### Taxonomy
- `GET /api/taxonomy/{name}` -- taxonomy tree with nodes and colors

### Auto-Complete
- `GET /api/suggest?q={prefix}` -- entity and taxonomy suggestions

### AI Generation
- `POST /api/ai/generate` -- generate ontology from corpus
- `GET /api/ai/generate/preview` -- preview generated DSL
- `POST /api/ai/generate/apply` -- generate, save DSL, reindex

### Viewer
- `GET /render/{id}` -- HTML rendering of document
- `GET /raw/{id}` -- raw file download
- `GET /proxy/{endpoint}` -- API forwarding

---

## 7. AI Ontology Generation Pipeline

Multi-pass LLM pipeline (each pass is a separate structured prompt):

```
Pass 1: Corpus Summary        → high-level description of document collection
Pass 2: Entity Type Discovery → proposed entity types with properties
Pass 3: Entity Extraction     → entity instances from documents
Pass 4: Relation Discovery    → relation types between entity types
Pass 5: Relation Extraction   → relation instances between entities
Pass 6: Taxonomy Generation   → hierarchical classification trees
Pass 7: Rule Generation       → regex classification rules
Pass 8: DSL Emission          → complete DSL output
```

Supports Claude (default), OpenAI, and Ollama providers via `IAiProvider`.

---

## 8. DSL Architecture

Two modes:

**Schema mode** (.dsl files): `@type`, `@entity`, `@relation.type`, `@relation`, `@taxonomy`, `@rule`

**Query mode**: full-text search with boolean operators, field filters (`entity:erard`), taxonomy filters (`track:voie_a`), graph traversal with depth control.

See [DSL_REFERENCE.md](DSL_REFERENCE.md) for complete syntax.

---

## 9. Key Dependencies

| Package | Purpose |
|---------|---------|
| Lucene.Net 4.8 | Full-text search with faceting |
| DocumentFormat.OpenXml | DOCX parsing |
| ClosedXML | XLSX parsing |
| CsvHelper | CSV parsing |
| Markdig | Markdown parsing |
| HtmlAgilityPack | HTML parsing |
| PdfPig | PDF text extraction |
