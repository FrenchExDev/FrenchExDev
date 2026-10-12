# DockAi -- Philosophy

## Ontology first, search second

Most document search tools index first and let users figure out what's in the corpus. DockAi inverts this: define what matters (entities, relations, taxonomies) first, then index with that knowledge.

An entity-aware index is fundamentally more useful than a keyword index. When you search for "erard", you don't just get documents containing that string -- you get documents linked to the entity Stephane Erard, including documents that mention "serard" or "stephane" through alias resolution. When you filter by `track:criminal`, you get documents classified by semantic rules, not just keyword matching.

The ontology is the lens through which the corpus is understood. Change the ontology, reindex, and the same documents reveal different structure.

---

## DSL over GUI for schema definition

An ontology could be defined through a web interface with forms and dropdowns. DockAi uses a text-based DSL instead, for three reasons:

1. **Version control** -- DSL files are plain text, diffable, mergeable, reviewable in PRs
2. **Composability** -- load multiple `.dsl` files, merge schemas, override definitions
3. **AI generation** -- LLMs produce text naturally; generating DSL is a single structured prompt

A GUI is better for exploration. A DSL is better for definition. DockAi is a definition tool.

---

## AI generates the first draft, humans curate

The AI ontology generation pipeline is not meant to produce a final ontology. It produces a first draft: entity types the LLM noticed, relations it inferred, taxonomies it suggested.

The human reviews the generated DSL, corrects entity names, removes false relations, adjusts taxonomy hierarchies, and tunes classification rules. Then reindexes. The AI saved hours of manual corpus reading; the human ensured correctness.

This is the right division of labor: AI for breadth (read 10,000 documents), human for depth (is this relation actually meaningful?).

---

## Links are computed, not declared

Document links in DockAi are not hyperlinks authored by someone. They are computed from shared semantics: two documents that mention the same entities are linked. Two documents in the same taxonomy node are linked. The link weight reflects the strength of the semantic overlap (Jaccard similarity).

This means the link graph evolves automatically as the ontology changes. Add a new entity type, reindex, and new links appear between documents that share instances of that type. Remove a taxonomy, and those classification-based links disappear.

Computed links are honest: they reflect what the system actually knows about document relationships, not what someone guessed.

---

## Multi-provider AI is a hedge, not a feature

DockAi supports Claude, OpenAI, and Ollama not because users want a provider menu, but because:

- API availability is not guaranteed (rate limits, outages)
- Cost varies 10x between providers for the same task
- Local models (Ollama) work offline and keep data on-premises
- The best model changes every few months

The `IAiProvider` interface makes switching a config change, not a code change. Today Claude gives the best ontology generation results. Tomorrow it might be a fine-tuned local model. The architecture doesn't care.

---

## Two tiers because rendering is not analysis

The API and Viewer are separate processes because they serve different concerns. The API handles computation: parsing, indexing, entity extraction, AI calls. The Viewer handles presentation: converting DOCX to HTML, serving static assets, proxying requests.

Separating them means the API can run headless in a batch pipeline, and the Viewer can be replaced with a different frontend without touching analysis logic.
