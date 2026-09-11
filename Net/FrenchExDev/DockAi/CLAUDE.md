# DockAi — Claude Context

AI-powered document search and knowledge graph system: parses multi-format corpora, extracts entities via ontology-driven recognition, classifies via regex rules, indexes with Lucene, and optionally generates ontologies via LLMs (Claude / OpenAI / Ollama).

## Package docs
- [README](README.md)
- [Architecture](doc/ARCHITECTURE.md)
- [How-To](doc/HOW-TO.md)
- [Philosophy](doc/PHILOSOPHY.md)
- [DSL Reference](doc/DSL_REFERENCE.md)

## Relevant skills
- [DSL Foundations](../../../Skills/Net/Programming/DSL-FOUNDATIONS/PHILOSOPHY.md)
- [SOLID](../../../Skills/Net/Programming/SOLID/PHILOSOPHY.md)
- [Solution Layout](../../../Skills/Net/Programming/SOLUTION-LAYOUT/ARCHITECTURE.md)
- [Central Package Management](../../../Skills/Net/Programming/CENTRAL-PACKAGE-MANAGEMENT/ARCHITECTURE.md)

## Solution
- No `.slnx`; build the two projects directly: `DockAi/src/DockAi.Api`, `DockAi/src/DockAi.Viewer`.

## Notes for Claude
- Two-tier: API on port 5108 owns all logic; Viewer on port 5112 only renders and proxies. Never put logic in Viewer.
- No external database — Lucene is the index, JSON files persist ontology and taxonomy.
- Multi-provider AI behind `IAiProvider` (Claude, OpenAI, Ollama). Add new providers by implementing the interface, not by editing existing ones.
- Custom DSL has *two* modes: schema (`@type`, `@entity`, `@relation`, `@taxonomy`, `@rule`) and query. Don't conflate them.
- Entity recognition is **case-insensitive alias matching** from the ontology — not ML/NER. Keep it simple.
- Document linking uses Jaccard similarity on shared entities with a threshold of 0.15.
