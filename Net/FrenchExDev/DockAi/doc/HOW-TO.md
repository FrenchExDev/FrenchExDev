# DockAi -- Developer Guide (HOW-TO)

## Table of Contents

1. [Getting Started](#1-getting-started)
2. [Uploading Documents](#2-uploading-documents)
3. [Writing DSL Schemas](#3-writing-dsl-schemas)
4. [Searching](#4-searching)
5. [AI Ontology Generation](#5-ai-ontology-generation)
6. [Batch Processing](#6-batch-processing)
7. [Configuration](#7-configuration)
8. [Viewing Documents](#8-viewing-documents)
9. [Extending Parsers](#9-extending-parsers)

---

## 1. Getting Started

### Prerequisites

- .NET 10.0 SDK
- (Optional) Anthropic API key, OpenAI API key, or local Ollama for AI features

### Running the API

```bash
dotnet run --project DockAi/src/DockAi.Api
# Listening on http://localhost:5108
```

### Running the Viewer

```bash
dotnet run --project DockAi/src/DockAi.Viewer
# Listening on http://localhost:5112
```

---

## 2. Uploading Documents

Upload a ZIP archive containing documents:

```bash
curl -X POST http://localhost:5108/api/upload \
  -F "file=@documents.zip"
```

The API will:
1. Extract all files from the ZIP
2. Parse each document (PDF, DOCX, XLSX, CSV, MD, HTML, TXT)
3. Extract entities based on loaded ontology
4. Apply classification rules
5. Build semantic links between documents
6. Index everything in Lucene

---

## 3. Writing DSL Schemas

Create `.dsl` files to define your ontology:

### Entity types

```
@type person {
    name: string
    role: enum [plaintiff, defendant, witness]
    aliases: string[]
}

@type organization {
    name: string
    sector: string
}
```

### Entity instances

```
@entity erard : person {
    name: "Stephane Erard"
    role: plaintiff
    aliases: ["erard", "serard", "stephane"]
}
```

### Relations

```
@relation.type employs {
    from: organization
    to: person
    properties: [start_date, end_date, position]
    inverse: employed_by
}

@relation erard -[employed_by]-> qwant {
    start_date: "2017-01"
    position: "Senior R&D Engineer"
}
```

### Taxonomies and rules

```
@taxonomy track {
    label: "Legal Track"
    facet: true
    criminal "Criminal Track" { color: "#E74C3C" }
    labor "Labor Track" { color: "#3498DB" }
}

@rule auto_criminal {
    when content matches "plainte|escroquerie|abus de confiance"
    then assign track: criminal
    confidence: 0.7
}
```

See [DSL_REFERENCE.md](DSL_REFERENCE.md) for the complete syntax.

---

## 4. Searching

### Basic full-text search

```
GET /api/search?q=contract
```

### Field filters

```
GET /api/search?q=entity:erard
GET /api/search?q=track:criminal
```

### Boolean operators

```
GET /api/search?q=contract AND (erard OR qwant)
```

### Limiting results

```
GET /api/search?q=contract&max=20
```

### Auto-complete

```
GET /api/suggest?q=era
# Returns matching entities and taxonomy nodes
```

---

## 5. AI Ontology Generation

### Configure the AI provider

In `appsettings.json`:

```json
{
  "Ai": {
    "Provider": "claude",
    "Model": "claude-sonnet-4-20250514",
    "ApiKey": "sk-ant-...",
    "Temperature": 0.3,
    "MaxOutputTokens": 8192
  }
}
```

Supported providers: `claude`, `openai`, `ollama`.

### Generate ontology from uploaded documents

```bash
# Generate (returns DSL preview)
curl -X POST http://localhost:5108/api/ai/generate

# Preview the generated DSL
curl http://localhost:5108/api/ai/generate/preview

# Apply: save DSL files and reindex
curl -X POST http://localhost:5108/api/ai/generate/apply
```

The multi-pass pipeline discovers entity types, extracts instances, discovers relations, generates taxonomies and classification rules.

---

## 6. Batch Processing

Process a directory of documents offline:

```bash
dotnet run --project DockAi/src/DockAi.Api -- \
  --input ./documents \
  --output ./output \
  --max-files 100 \
  --max-parallel 3
```

| Flag | Default | Purpose |
|------|---------|---------|
| `--input <dir>` | (required) | Source document directory |
| `--output <dir>` | `./output` | Output directory |
| `--max-files <n>` | unlimited | Limit document processing |
| `--max-parallel <n>` | 3 | Parallel AI narrative threads |

---

## 7. Configuration

### appsettings.json

```json
{
  "Ai": {
    "Provider": "claude",
    "Model": "claude-sonnet-4-20250514",
    "ApiKey": "",
    "Temperature": 0.3,
    "MaxOutputTokens": 8192
  }
}
```

### Environment variables

The API key can also be set via environment variable to avoid committing secrets:

```bash
export AI__APIKEY=sk-ant-...
```

---

## 8. Viewing Documents

The Viewer renders documents as HTML for browser viewing:

```
http://localhost:5112/render/{documentId}    # Rendered HTML
http://localhost:5112/raw/{documentId}       # Raw file download
```

The Viewer also proxies all API calls:

```
http://localhost:5112/proxy/search?q=...
http://localhost:5112/proxy/ontology
```

---

## 9. Extending Parsers

To add support for a new document format:

1. Implement `IDocumentParser`:

```csharp
public class RtfParser : IDocumentParser
{
    public Task<string> ParseAsync(Stream stream)
    {
        // Extract plain text from RTF
    }
}
```

2. Register in `ParserFactory`:

```csharp
".rtf" => new RtfParser(),
```

3. (Optional) Add a renderer in the Viewer:

```csharp
public class RtfRenderer : IDocumentRenderer
{
    public Task<string> RenderAsync(Stream stream)
    {
        // Convert RTF to HTML
    }
}
```

4. Register in `RendererFactory`.
