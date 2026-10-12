# DockAi

AI-powered document search and knowledge graph management system. Parses multi-format document collections, extracts entities via ontology-driven recognition, classifies documents with regex rules, builds semantic links, indexes with Lucene, and optionally generates ontologies via LLM (Claude, OpenAI, Ollama).

## Quick Start

```bash
# API server mode (default)
dotnet run --project DockAi/src/DockAi.Api

# Batch mode — process a directory of documents
dotnet run --project DockAi/src/DockAi.Api -- --input ./docs --output ./output

# Viewer (document rendering + proxy)
dotnet run --project DockAi/src/DockAi.Viewer
```

**API**: http://localhost:5108 | **Viewer**: http://localhost:5112

## Projects

| Project | TFM | Purpose |
|---------|-----|---------|
| `DockAi.Api` | net10.0 | Backend: parsing, indexing, entity extraction, ontology, AI, search |
| `DockAi.Viewer` | net10.0 | Frontend proxy: document rendering, API forwarding |

## Supported Formats

PDF, DOCX, XLSX, CSV, Markdown, HTML, plain text.

## Key Features

- **Ontology-driven entity recognition** -- case-insensitive alias matching from DSL-defined entity types
- **Regex-based auto-classification** -- rules assign taxonomy nodes with confidence scores
- **Semantic document linking** -- Jaccard similarity on shared entities (threshold >= 0.15)
- **Full-text search** -- Lucene with faceted search, highlighting, and DSL query language
- **AI ontology generation** -- multi-pass LLM pipeline discovers entity types, relations, taxonomies from corpus
- **Custom DSL** -- schema mode (@type, @entity, @relation, @taxonomy, @rule) + query mode

## Key Design Decisions

- **Two-tier architecture** -- API (port 5108) handles all logic; Viewer (port 5112) handles rendering and proxying
- **Factory pattern for parsers/renderers** -- `ParserFactory` and `RendererFactory` select by file extension
- **In-memory stores** -- `OntologyStore` and `TaxonomyStore` with JSON persistence
- **Multi-provider AI** -- `IAiProvider` with Claude, OpenAI, and Ollama implementations
- **No external database** -- Lucene for indexing, JSON for ontology persistence

## Documentation

- [ARCHITECTURE.md](doc/ARCHITECTURE.md) -- project structure, pipeline, DSL, AI generation, API endpoints
- [HOW-TO.md](doc/HOW-TO.md) -- developer guide: uploading, searching, DSL authoring, AI generation
- [PHILOSOPHY.md](doc/PHILOSOPHY.md) -- why ontology-first, why DSL, why multi-provider AI
- [DSL_REFERENCE.md](doc/DSL_REFERENCE.md) -- complete DSL syntax specification

## Building

```bash
dotnet build DockAi/src/DockAi.Api
dotnet build DockAi/src/DockAi.Viewer
```
