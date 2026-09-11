# DIEM-CMF — Architecture

A declaration-first CMF is structured as a constellation of small sub-DSLs sitting on top of a shared metamodel foundation. This document describes the reusable architecture: layering, sub-DSL decomposition, source-generator pipeline, and customization contract.

## Layering

```
+------------------------------------------------------------------+
|  Application code (developer's partial classes + attributes)    |
+------------------------------------------------------------------+
|  Sub-DSLs:  DDD | Content | Admin | Pages | Workflow | Reqs    |
+------------------------------------------------------------------+
|  Source generators (one per sub-DSL, all incremental)           |
+------------------------------------------------------------------+
|  M3 Metamodel foundation (MetaConcept, ConceptInstance, ...)    |
+------------------------------------------------------------------+
|  Infrastructure: persistence, identity, cache, search, media    |
+------------------------------------------------------------------+
```

Each layer depends only on the layer below it. The metamodel foundation (M3) knows nothing about content, admin, or workflow — it provides the primitives those sub-DSLs are built from.

## Sub-DSL Decomposition

A CMF is **not** one giant DSL. It is a set of small, focused sub-DSLs:

| Sub-DSL    | Purpose                              | Representative attributes                            |
|------------|--------------------------------------|------------------------------------------------------|
| DDD        | Aggregates, entities, value objects  | `[AggregateRoot]`, `[EntityId]`, `[Property]`, `[Invariant]` |
| Content    | Parts, blocks, stream fields         | `[HasPart]`, `[ContentBlock]`, `[StreamField]`       |
| Admin      | Lists, forms, batch actions          | `[AdminModule]`, `[AdminField]`, `[AdminAction]`     |
| Pages      | Widgets, layouts, areas, routing     | `[PageWidget]`, `[WidgetConfig]`                     |
| Workflow   | State machine, gates, scheduling     | `[Workflow]`, `[Stage]`, `[Transition]`, `[RequiresRole]` |
| Requirements | Traceability                       | `[TracedBy]`, `[Requirement]`                        |

Each sub-DSL is its own project with its own attributes, its own concepts, its own source generator, and its own tests. They compose because they share the M3 metamodel.

## Sub-DSL Project Anatomy

Every sub-DSL follows the same project shape:

```
<SubDsl>/
  <Sub>.Attributes/        # Attribute classes + Concept companions (netstandard2.0)
  <Sub>/                   # Runtime types (base classes, services, options)
  <Sub>.Design/            # Optional: design-time tooling
  <Sub>.SourceGenerator/   # Roslyn incremental SG (netstandard2.0)
  <Sub>.Tests/             # xUnit tests
```

Splitting attributes into their own assembly keeps the SG dependency cone small and lets the runtime depend on attributes without pulling in Roslyn.

## The M3 Metamodel Foundation

Every DSL attribute is built on five primitives:

- **`MetaConcept`** — a class declaring "this attribute represents the concept X"
- **`ConceptInstance`** — a captured instance of a concept (a usage in user code)
- **`MetaProperty`** — a typed slot on a concept
- **`MetaConstraint`** — a validation rule referencing a real method
- **`MetamodelRegistry`** — auto-discovery of all concepts at compile time

A new sub-DSL is added by:
1. Defining attribute classes annotated `[MetaConcept(typeof(MyConcept))]`
2. Implementing the `MyConcept : MetaConcept` companion
3. Writing a source generator that walks the registry

There is no central registration step. Concepts find each other.

## The Source-Generator Pipeline

Each sub-DSL ships exactly one incremental source generator. Typical pipeline:

```
SyntaxProvider                             // find candidate attributed types
  -> Where(IsApplicable)                   // filter
  -> Select(BuildModel)                    // pure POCO model
  -> Combine(MetamodelRegistry)            // cross-DSL info
  -> RegisterSourceOutput(Emit)            // string-based emit
```

The model (`<Sub>EmitModel`) is a pure POCO with no Roslyn dependency. The emitter takes the model and produces C# strings. This split is critical: the emitter is unit-testable without Roslyn.

## Composing Sub-DSLs on a Single Type

A single entity can carry attributes from multiple sub-DSLs:

```csharp
[AggregateRoot("Article", BoundedContext = "Editorial")]   // DDD
[HasPart(typeof(RoutablePart))]                            // Content
[HasPart(typeof(SeoablePart))]
[HasPart(typeof(VersionablePart))]
[AdminModule("Articles", typeof(Article))]                 // Admin
[Workflow("Editorial")]                                    // Workflow
[Stage("Draft", IsInitial = true)]
[Stage("Published", IsFinal = true)]
[Transition("Publish", From = "Draft", To = "Published")]
[RequiresRole("Publish", Role = "Editor")]
[TracedBy("REQ-ART-001")]                                  // Requirements
public partial class Article { /* ... */ }
```

Each generator runs independently, reads its own attributes, and emits its own files. They compose because they all read the same metamodel registry.

## Parts: Horizontal Composition

A part is a small reusable entity decoration. The classic catalog:

| Part              | What it adds                               |
|-------------------|--------------------------------------------|
| `RoutablePart`    | URL slug, canonical path                   |
| `SeoablePart`     | Meta title, description, OG tags           |
| `TaggablePart`    | Taxonomy tags                              |
| `AuditablePart`   | CreatedAt, UpdatedAt, CreatedBy, UpdatedBy |
| `VersionablePart` | Temporal versioning (ValidFrom/ValidTo)    |
| `LocalizablePart` | Per-locale content variants                |
| `SortablePart`    | Drag-and-drop ordering (Position)          |
| `SchedulablePart` | PublishAt, UnpublishAt                     |
| `MediablePart`    | Attached media files                       |

Each part is its own project. Each part is **the documentation** for how to write a part — there is no internal API, no framework magic. Built-ins use the same attributes the developer uses.

## Blocks and StreamFields: Vertical Composition

A **Block** is a typed content shape (`HeroBlock`, `RichTextBlock`, `TestimonialBlock`, `ImageBlock`). It implements `IContentBlock` and has a JSON schema, an editor, and a renderer.

A **StreamField** is an ordered, polymorphic sequence of blocks, stored as JSON, with the developer specifying which block types are allowed:

```csharp
[StreamField(typeof(HeroBlock), typeof(RichTextBlock), typeof(ImageBlock))]
public partial IReadOnlyList<IContentBlock> Body { get; }
```

The SG produces serialization, deserialization, validation, and the editor binding.

## The Page Composition Model

```
Page
  +-- Layout (shared across pages)
        +-- Area: Top       (layout-level, same on all pages with this layout)
        +-- Area: Left
        +-- Area: Content   (page-level, unique per page)
        +-- Area: Right
        +-- Area: Bottom
              +-- Zone (column within an area)
                    +-- WidgetInstance (configured widget placement)
```

Pages are **database records**, not source files. Editors create pages, assign layouts, and drag widgets into zones from the admin UI. Developers define widgets as components with `[PageWidget]` and configuration properties with `[WidgetConfig]`. The compiler generates the widget catalog. Editors place them.

This separation is fundamental: **developers define what widgets exist; editors decide where they go.**

## The Workflow Sub-DSL

```csharp
[Workflow("Editorial")]
[Stage("Draft", IsInitial = true)]
[Stage("Review")]
[Stage("Published", IsFinal = true)]
[Transition("Submit", From = "Draft", To = "Review")]
[Transition("Approve", From = "Review", To = "Published")]
[RequiresRole("Approve", Role = "Editor")]
[ScheduledTransition("AutoExpire", From = "Published", After = "30.00:00:00")]
public partial class EditorialWorkflow { }
```

The generator emits a `WorkflowEngine<TArticle>` with `CanTransition`, `Transition`, and gate evaluation. Scheduled transitions run via a hosted background service. Locale tracking allows per-locale progress.

## The Customization Contract (Four Layers)

```
Layer 1: ComponentBase                  Framework base (services, logging, DI)
Layer 2: SubDslBase<T>                  Sub-DSL base (virtual hooks)
Layer 3: ArticleComponents.g.cs         Generated (regenerated every build)
Layer 4: ArticleComponents.cs           Developer's partial class (never overwritten)
```

Layer 2 declares virtual methods (`OnCustomizeListQuery`, `OnRender`, `OnBeforeSave`). Layer 3 calls them in the generated implementation. Layer 4 overrides only what is needed.

## Requirements Traceability

```csharp
[Requirement("REQ-ART-001", "Articles must have a unique slug")]
public static class ArticleRequirements { }

[TracedBy("REQ-ART-001")]
public partial class Article { }

[TracedBy("REQ-ART-001")]
public class ArticleSlugUniquenessTests { }
```

The Requirements SG builds a registry mapping requirement -> implementing types -> covering tests. The CLI emits a traceability matrix. A requirement without a covering test is a build warning.

## Infrastructure Boundaries

The CMF defines abstractions and ships at least one default implementation per concern:

| Concern        | Abstraction         | Default                  |
|----------------|---------------------|--------------------------|
| Media storage  | `IMediaStorage`     | FileSystem               |
| Search         | `ISearchEngine`     | Lucene.Net               |
| Identity       | `IUserContext`      | Built-in user/role types |
| Caching        | `IContentCache`     | Tag-based memory cache   |
| Notifications  | `INotifications`    | In-app                   |
| Real-time      | `IHubClient`        | SignalR                  |

Each is independently swappable.

## CLI Surface

A CMF ships a single CLI entry point with sub-commands:

```
cmf new          # scaffold a project
cmf add          # add a sub-DSL or module
cmf generate     # run source generators (rare; usually automatic)
cmf validate     # validate DSL model consistency
cmf migrate      # generate/apply persistence migrations
cmf report       # output DSL model report
cmf design       # launch design-time tooling
```
