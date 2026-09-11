# Diem CMF -- Architecture

> A C# / .NET 10 reimagining of the Diem Content Management Framework,
> powered by Roslyn source generation and a formal metamodel (M3/M2/M1/M0).

---

## 1. CMF vs CMS

A **Content Management System** (CMS) is a finished product -- install WordPress, pick a
theme, add plugins. A **Content Management Framework** (CMF) is the machinery you use to
*build* a CMS tailored to your domain.

Diem is a framework. It ships no theme, no admin UI out of the box, no page tree. It ships
*DSLs* -- small attribute-based languages whose source generators produce all of that for
you at compile time. The developer declares intent (`[ContentPart("Routable")]`,
`[AdminModule("Products", typeof(Product))]`, `[Workflow("Editorial")]`) and the framework
generates entities, EF Core mappings, admin CRUD pages, page layouts, workflow engines, and
API endpoints.

The original **Diem PHP** (2010, Symfony 1.4) pioneered this approach:

- `schema.yml` declared data models --> Doctrine ORM entities
- `modules.yml` declared admin + front modules --> generated components
- `dm:setup` ran the generator pipeline --> everything materialized
- The developer then overrode stubs at two customization points

The C# Diem preserves the same philosophy -- *declare, generate, override* -- but replaces
YAML with C# attributes, runtime interpretation with Roslyn source generation, and the PHP
class hierarchy with `partial class` + `virtual`/`override`.

---

## 2. The Original Diem (PHP / Symfony 1.4, 2010)

### 2.1 Declaration Files

| File | Role |
|------|------|
| `config/doctrine/schema.yml` | Data model: columns, relations, behaviors (Timestampable, Sluggable, etc.) |
| `config/dm/modules.yml` | Module registry: which models get admin CRUD, which get front components |
| `apps/{app}/modules/{mod}/config/generator.yml` | Per-module admin customization: list columns, form fields, filters, actions |

### 2.2 The dm:setup Pipeline

Running `./symfony dm:setup` (or `php symfony dm:setup`) would:

1. Parse `schema.yml` --> generate Doctrine model classes + SQL migrations
2. Parse `modules.yml` --> scaffold admin modules (list + form + actions) and front modules (show + list)
3. Generate the page tree skeleton (Page --> Layout --> Areas --> Zones --> Widgets)
4. Create empty stub classes at two customization layers for the developer

### 2.3 Page Composition Model

```
Page (row in dm_page, has materialized path URL)
 +-- Layout (defines area grid, e.g. "2-column")
      +-- Area (named region, e.g. "main", "sidebar")
           +-- Zone (vertical slot within an area)
                +-- Widget (server-rendered PHP partial)
```

Pages were stored in the database. Layouts defined structural regions. Zones held ordered
lists of widgets. Widgets were reusable components -- text, media, navigation, module output
-- that editors dragged into zones through the front-end toolbar.

### 2.4 The 6-Layer Class Hierarchy

Every front module had a 6-deep inheritance chain with 2 developer customization stubs:

```
articleComponents                        <-- Developer code (module-specific)
  extends myFrontModuleComponents        <-- Empty stub (per-module hook)
    extends dmFrontModuleComponents      <-- Framework: getShowQuery, getListQuery, getPager
      extends myFrontBaseComponents      <-- Empty stub (cross-cutting hook)
        extends dmFrontBaseComponents    <-- Framework: front-specific helpers
          extends dmBaseComponents       <-- Framework: services, routing, helpers
            extends sfComponents         <-- Symfony framework
```

The developer wrote `articleComponents`. The framework generated everything in between.
`myFrontModuleComponents` and `myFrontBaseComponents` were empty stubs the developer could
fill in to customize behavior at two granularity levels.

---

## 3. The C# Reimagining

| PHP Diem | C# Diem | Why |
|----------|---------|-----|
| `schema.yml` (YAML) | `[AggregateRoot]`, `[ContentPart]`, `[Property]` (C# attributes) | Type-safe, IDE-navigable, refactor-safe |
| `modules.yml` (YAML) | `[AdminModule]`, `[PageWidget]` (C# attributes) | Same benefits + compile-time validation |
| `dm:setup` (CLI command) | `dotnet build` (source generators run automatically) | Zero-step generation, always in sync |
| `generator.yml` (YAML) | `[AdminField]`, `[AdminFilter]`, `[AdminAction]` (C# attributes) | Declarative, validated by SG |
| Runtime interpretation | Roslyn source generation (compile-time) | Errors caught at build, not at request time |
| PHP class hierarchy (6 layers) | `partial class` + `virtual`/`override` (4 layers) | Flatter, no empty stubs, clearer customization |
| `sfComponents` base | `DiemComponentBase` (DI, logging) | Standard .NET patterns |
| Doctrine ORM behaviors | Parts (`[HasPart(typeof(RoutablePart))]`) | Horizontal composition, not mixins |

### Key Insight

The C# version replaces **inheritance depth** with **composition breadth**. Instead of 6
layers of base classes, the developer composes behaviors horizontally via Parts and vertically
via the 4-layer customization model. Source generation produces the glue code that PHP Diem
created at `dm:setup` time, but it runs on every build, so generated code never drifts from
declarations.

---

## 4. Sub-DSL Architecture

Each Diem sub-DSL follows a consistent 5-project pattern:

```
FrenchExDev.Net.Diem.{SubDsl}                      <-- Runtime library (net10.0)
FrenchExDev.Net.Diem.{SubDsl}.Attributes            <-- DSL attributes + companion concepts
                                                        (netstandard2.0;net10.0)
FrenchExDev.Net.Diem.{SubDsl}.SourceGenerator        <-- Roslyn incremental SG
                                                        (netstandard2.0, IsRoslynComponent)
FrenchExDev.Net.Diem.{SubDsl}.Design                 <-- Design-time: CLI scaffolding
                                                        (net10.0, optional)
```

The **Attributes** project always contains:
- Attribute classes (the declarative surface developers use)
- A `Concepts/` folder with companion classes extending `MetaConcept` from `FrenchExDev.Net.Dsl`
- Each attribute links to its companion via `[MetaConcept(typeof(XxxConcept))]`

### 4.1 Content Sub-DSL

Three sub-DSLs under Content, each with their own Attributes + SourceGenerator:

#### Parts -- Horizontal Composition

Parts are reusable cross-cutting concerns that attach to content entities via `[HasPart]`.
They model *capabilities* (routable, seoable, auditable), not data hierarchy.

| Attribute | Target | Purpose |
|-----------|--------|---------|
| `[ContentPart("name")]` | Class | Declares a part definition |
| `[PartField("name")]` | Property | Declares a typed field within a part |
| `[HasPart(typeof(XxxPart))]` | Class | Attaches a part to a content entity |

**9 built-in parts** (in `FrenchExDev.Net.Diem.Content.Parts`):

| Part | Fields | Purpose |
|------|--------|---------|
| `RoutablePart` | Slug, UrlPath, IsCanonical | URL routing with slugs |
| `SeoablePart` | MetaTitle, MetaDescription, OgImage, NoIndex | SEO metadata |
| `AuditablePart` | CreatedBy, CreatedAt, ModifiedBy, ModifiedAt | Audit trail |
| `VersionablePart` | VersionNumber, ValidFrom, ValidTo, IsCurrent | Temporal versioning |
| `LocalizablePart` | Culture, LocalizationSet, IsDefault | Multi-locale support |
| `TaggablePart` | Tags, Taxonomy | Taxonomy tagging |
| `SortablePart` | SortOrder, SortGroup | Manual ordering |
| `SchedulablePart` | PublishAt, UnpublishAt, IsPublished | Publication scheduling |
| `MediablePart` | MediaPath, AltText, MimeType, FileSizeBytes | Media attachment |

**Developer usage:**

```csharp
[AggregateRoot("BlogPost")]
[HasPart(typeof(RoutablePart))]
[HasPart(typeof(SeoablePart))]
[HasPart(typeof(VersionablePart))]
public partial class BlogPost { ... }
```

The source generator reads `[HasPart]` and generates EF Core owned-type mappings, admin form
fields, and API DTO projections for each attached part.

#### Blocks -- Vertical Composition

Blocks are self-contained content units that compose into sequences. They model the *building
blocks* of page content (hero banners, text sections, images).

| Attribute | Target | Purpose |
|-----------|--------|---------|
| `[StructBlock("name")]` | Class | Declares a structured block with named fields |
| `[ListBlock("name")]` | Class | Declares a block that repeats a single child type |
| `[StreamBlock("name")]` | Class | Declares a block that allows mixed child types |
| `[BlockField("name")]` | Property | Declares a typed field within a block |

All blocks implement `IContentBlock` (property: `string BlockType`).

**4 built-in blocks** (in `FrenchExDev.Net.Diem.Content.Blocks`):

| Block | Key Fields | Purpose |
|-------|------------|---------|
| `HeroBlock` | Heading, Subheading, BackgroundImage, CtaText, CtaUrl | Hero banner with CTA |
| `RichTextBlock` | Body, Format | Rich text (HTML or Markdown) |
| `ImageBlock` | Src, Alt, Caption, Width, Height | Image with metadata |
| `TestimonialBlock` | Quote, Author, Role, AvatarUrl | Customer testimonial |

The source generator produces JSON converters (type-discriminated polymorphic serialization)
and Blazor renderer scaffolds for each block.

#### StreamFields -- Polymorphic JSON Sequences

StreamFields bridge Parts and Blocks: a stream field is a property on a content entity that
holds an ordered, polymorphic sequence of blocks.

| Attribute | Target | Purpose |
|-----------|--------|---------|
| `[StreamField("name", AllowedBlockTypes = ...)]` | Property | Declares a polymorphic block sequence |

```csharp
[StreamField("Body", AllowedBlockTypes = new[]
    { typeof(HeroBlock), typeof(RichTextBlock), typeof(ImageBlock) })]
public partial IReadOnlyList<IContentBlock> Body { get; }
```

The source generator produces a JSON array column mapping with type-discriminated converters
that respect the `AllowedBlockTypes` constraint.

### 4.2 Admin Sub-DSL

Three sub-DSLs under Admin, mirroring the original Diem's `generator.yml`:

#### Lists

| Attribute | Target | Purpose |
|-----------|--------|---------|
| `[AdminModule("name", typeof(Aggregate))]` | Class | Declares an admin module for an aggregate |
| `[AdminFilter("field")]` | Class | Adds a filter to the list page |

`AdminModule` properties: `Name`, `Aggregate` (type), `Icon`, `Group`, `PageSize` (default 25).

#### Forms

| Attribute | Target | Purpose |
|-----------|--------|---------|
| `[AdminField("name")]` | Property | Customizes a field's admin rendering |

`AdminField` properties: `Name`, `DisplayType`, `DisplayName`, `ReadOnly`, `HideInList`,
`HideInForm`, `Order`.

The source generator infers field widgets from property types when `[AdminField]` is not
explicitly applied (string --> text input, bool --> checkbox, DateTime --> date picker, etc.).

#### Actions

| Attribute | Target | Purpose |
|-----------|--------|---------|
| `[AdminAction("name")]` | Class | Declares a batch action on the list page |

`AdminAction` properties: `Name`, `Command`, `Icon`, `ConfirmationMessage`, `RequiresRole`.

**Developer usage -- one class, entire CRUD generated:**

```csharp
[AdminModule("Products", typeof(Product), Icon = "box", Group = "Catalog")]
[AdminFilter("Category", FilterType = "Dropdown")]
[AdminFilter("IsActive", FilterType = "Boolean")]
[AdminAction("Publish", Command = "PublishProduct", RequiresRole = "Editor")]
public partial class ProductsAdminModule { }
```

Generated output: Blazor list page + form page + detail page + navigation entry (~700 lines).

### 4.3 Pages Sub-DSL

Three sub-DSLs under Pages:

#### Widgets

| Attribute | Target | Purpose |
|-----------|--------|---------|
| `[PageWidget("name")]` | Class | Registers a widget in the catalog |
| `[WidgetConfig]` | Property | Declares a configurable widget parameter |

`PageWidget` properties: `Name`, `Module`, `Description`, `Icon`.
`WidgetConfig` properties: `DisplayName`, `HelpText`, `Required`, `DefaultValue`.

```csharp
[PageWidget("ProductList", Module = "Product", Icon = "grid")]
public partial class ProductListWidget : ComponentBase
{
    [WidgetConfig(DisplayName = "Category")] public string? Category { get; set; }
    [WidgetConfig(DisplayName = "Page Size")] public int PageSize { get; set; } = 12;
}
```

#### Layouts

| Attribute | Target | Purpose |
|-----------|--------|---------|
| `[Layout("name")]` | Class | Declares a page layout template |
| `[Area("name")]` | Class | Declares a named region within a layout |
| `[Zone("name")]` | Class | Declares a widget slot within an area |

`Zone` properties: `Name`, `MaxWidgets` (default 10).

The Page --> Layout --> Area --> Zone --> WidgetInstance hierarchy mirrors the original PHP
Diem exactly:

```
Page (database row, materialized path URL)
 +-- Layout (compile-time definition via [Layout])
      +-- Area (compile-time definition via [Area])
           +-- Zone (compile-time definition via [Zone])
                +-- WidgetInstance (runtime data, references a [PageWidget])
```

Layouts, areas, and zones are defined at compile time with attributes. Pages and widget
instances are managed at runtime (database). This split means structural integrity is
validated at build time while content remains editable.

#### Routing

| Attribute | Target | Purpose |
|-----------|--------|---------|
| `[BoundEntity(typeof(Entity))]` | Class | Binds a page to a content entity |

`BoundEntity` properties: `EntityType`, `UrlPattern`.

Materialized path URL resolution: each page stores its full URL path (e.g., `/products/shoes`)
and an optional pattern for bound entities (e.g., `/products/{slug}`). The router resolves
URLs by longest-prefix match on the materialized path, then delegates to the bound entity
lookup.

### 4.4 Workflow Sub-DSL

Four sub-DSLs under Workflow:

#### StateMachine

| Attribute | Target | Purpose |
|-----------|--------|---------|
| `[Workflow("name")]` | Class | Declares a workflow definition |
| `[Stage("name")]` | Class | Adds a stage (AllowMultiple) |
| `[Transition("name")]` | Class | Adds a transition between stages (AllowMultiple) |
| `[HasWorkflow("name")]` | Class | Attaches a workflow to a content entity |

`Stage` properties: `Name`, `IsInitial`, `IsFinal`, `Color`, `Description`.
`Transition` properties: `Name`, `From`, `To`, `Description`.

```csharp
[Workflow("Editorial")]
[Stage("Draft", IsInitial = true)]
[Stage("Review")]
[Stage("Published", IsFinal = true)]
[Transition("Submit", From = "Draft", To = "Review")]
[Transition("Approve", From = "Review", To = "Published")]
public partial class EditorialWorkflow { }
```

Generated output: stage enum, transition validator, `WorkflowEngine<T>` (~500 lines).

#### Gates

| Attribute | Target | Purpose |
|-----------|--------|---------|
| `[Gate("name")]` | Class | Custom gate (base concept) |
| `[RequiresRole("transition", "role")]` | Class | Role-based gate (inherits Gate) |
| `[RequiresApproval("transition", minApprovers)]` | Class | Approval gate (inherits Gate) |

Gates are evaluated by `IGateEvaluator` before a transition fires. Multiple gates on the
same transition are AND-combined.

#### Scheduling

| Attribute | Target | Purpose |
|-----------|--------|---------|
| `[ScheduledTransition("from", "to", "dateProperty")]` | Class | Time-triggered transition |

A `BackgroundService` polls entities with scheduled transitions and fires them when the
date property passes.

#### Locales

| Attribute | Target | Purpose |
|-----------|--------|---------|
| `[ForEachLocale("stage")]` | Class | Per-locale workflow tracking |

Binds to `ILocaleTracker` so that a content entity can be in different workflow stages for
different locales (e.g., the French translation is "Published" while German is still "Review").

---

## 5. The 4-Layer Customization Model (PHP --> C#)

The original 6-layer PHP hierarchy collapses into 4 C# layers:

```
PHP (Original Diem)                        C# (New Diem)
---------------------                      ---------------
sfComponents                               (framework base)
  +-- dmBaseComponents                     DiemComponentBase              [Layer 1]
    +-- dmFrontBaseComponents                services, logging, DI
      +-- myFrontBaseComponents            DiemFrontModuleBase<T>         [Layer 2]
        +-- dmFrontModuleComponents          GetShowQuery(), GetListQuery(),
          +-- myFrontModuleComponents        GetPager(), ApplyFilters()
            +-- articleComponents          ArticleComponents.g.cs         [Layer 3]
                                             generated: module-specific,
                                             virtual hooks
                                           ArticleComponents.cs           [Layer 4]
                                             developer's partial class,
                                             overrides
```

### Layer 1: Framework Base (`FrenchExDev.Net.Diem`)

```csharp
public abstract class DiemComponentBase
{
    protected IServiceProvider Services { get; }
    protected ILogger Logger { get; }
    protected T GetService<T>() where T : notnull;
}
```

Equivalent to `sfComponents` + `dmBaseComponents`. Provides DI access, logging, and core
framework plumbing. Ships in the `FrenchExDev.Net.Diem` NuGet package.

### Layer 2: Module Base (`FrenchExDev.Net.Diem.Pages`)

```csharp
public abstract class DiemFrontModuleBase<TEntity> : DiemComponentBase
{
    protected virtual IQueryable<TEntity> GetShowQuery() { ... }
    protected virtual IQueryable<TEntity> GetListQuery() { ... }
    protected virtual IPager<TEntity> GetPager(...) { ... }
    protected virtual IQueryable<TEntity> ApplyFilters(...) { ... }
    protected virtual IQueryable<TEntity> ApplyOrdering(...) { ... }
}
```

Equivalent to `dmFrontBaseComponents` + `dmFrontModuleComponents`. Provides the
module-level query pipeline with virtual methods the developer can override.

### Layer 3: Generated Partial Class

```csharp
// ArticleComponents.g.cs -- regenerated every build, NEVER hand-edit
public partial class ArticleComponents : DiemFrontModuleBase<Article>
{
    protected override IQueryable<Article> GetListQuery() { ... }
    public virtual async Task<ArticleListViewModel> ExecuteListAsync(...) { ... }
    protected virtual IQueryable<Article> OnCustomizeListQuery(query) => query;
    protected virtual IQueryable<Article> OnCustomizeShowQuery(query) => query;
}
```

Equivalent to `myFrontModuleComponents`. Source-generated from the developer's DSL
declarations. Provides module-specific logic with virtual hooks.

### Layer 4: Developer's Partial Class

```csharp
// ArticleComponents.cs -- created once by scaffolding, NEVER overwritten
public partial class ArticleComponents
{
    protected override IQueryable<Article> OnCustomizeListQuery(IQueryable<Article> query)
    {
        if (ShowOnlyMine) query = query.Where(a => a.AuthorId == CurrentUserId);
        return query;
    }
}
```

Equivalent to `articleComponents`. The developer's code. Overrides only the hooks they
need to customize. The file is scaffolded once by the CLI and never touched by the
generator again.

### Why 4 layers instead of 6?

The two empty PHP stubs (`myFrontModuleComponents`, `myFrontBaseComponents`) existed because
PHP lacked `partial class`. They were scaffolded as empty files the developer *could* fill in
later. In C#, `partial class` eliminates the need for an empty stub -- the generated code and
the developer's code coexist in the same class. The `virtual` keyword replaces the inheritance
chain for customization points.

---

## 6. Infrastructure Layer

Infrastructure concerns are independent projects that any sub-DSL can consume:

### Media

| Project | Purpose |
|---------|---------|
| `Diem.Media` | `IMediaStorage` interface: Upload, Download, Delete, Exists, GetPublicUrl |
| `Diem.Media.FileSystem` | Local file system implementation |
| `Diem.Media.Minio` | S3-compatible object storage via MinIO |

Supporting types: `MediaFile`, `ThumbnailOptions`.

### Search

| Project | Purpose |
|---------|---------|
| `Diem.Search` | `ISearchEngine` interface: Index, Delete, Search |
| `Diem.Search.Lucene` | Lucene.NET implementation |

Supporting types: `SearchDocument` (Id, Title, Body, EntityType, Url, Metadata),
`SearchQuery` (Text, MaxResults, Skip, EntityTypeFilter),
`SearchResults` / `SearchHit` (DocumentId, Title, Snippet, Score, Url).

### Identity

| Project | Purpose |
|---------|---------|
| `Diem.Identity` | `DiemUser`, `DiemRoles`, `IDiemUserContext` |

Built-in roles: `Admin`, `Editor`, `Author`, `Publisher`, `Viewer`.

`IDiemUserContext` provides: `CurrentUser`, `IsInRole(string)`, `UserId`.

### Caching

| Project | Purpose |
|---------|---------|
| `Diem.Caching` | `IDiemCache` interface with tag-based invalidation |

Interface methods: `GetAsync<T>`, `SetAsync<T>`, `RemoveAsync`, `InvalidateByTagAsync`.

`DiemCacheOptions`: `DefaultExpiry` (5 min), `EnableWidgetCache`, `EnablePageCache`,
`EnableQueryCache`.

### Metrics

| Project | Purpose |
|---------|---------|
| `Diem.Metrics` | OpenTelemetry counters and histograms |

Instruments (on `Meter("FrenchExDev.Net.Diem")`):

| Metric | Type | Name |
|--------|------|------|
| Page views | Counter | `diem.page.views` |
| Widget render time | Histogram | `diem.widget.render_duration` |
| Content edits | Counter | `diem.content.edits` |
| Workflow transitions | Counter | `diem.workflow.transitions` |
| Search queries | Counter | `diem.search.queries` |
| Media uploads | Counter | `diem.media.uploads` |

### RealTime

| Project | Purpose |
|---------|---------|
| `Diem.RealTime` | `IDiemHubClient` -- SignalR hub interface for admin collaboration |

Events: `ContentUpdated`, `PageEdited`, `WidgetMoved`, `WorkflowTransitioned`.

### Notifications

| Project | Purpose |
|---------|---------|
| `Diem.Notifications` | `INotificationService` with `Notification` model |

`NotificationType` enum: `Email`, `Push`, `InApp`.

---

## 7. Pipeline: 5-Stage Source Generation

Diem's source generation follows the same staged pipeline as `FrenchExDev.Net.Dsl`:

```
+------------------------------------------------------------------+
|  Stage 0: MetamodelRegistration (Dsl SG)                         |
|    - Scans all [MetaConcept]-decorated attributes                |
|    - Instantiates companion concept classes                      |
|    - Calls concept.OnDiscovered(ctx)                             |
|    - Emits MetamodelRegistry.g.cs                                |
+------------------------------------------------------------------+
         |
         v
+------------------------------------------------------------------+
|  Stage 1: Validation (each sub-DSL SG)                           |
|    - For each M1 class with DSL attributes:                      |
|      concept.OnBeforeValidation(ctx)                             |
|      result = concept.Validate(ctx)                              |
|      concept.OnAfterValidation(ctx, result)                      |
|    - Violated constraints --> Roslyn diagnostics (build errors)   |
+------------------------------------------------------------------+
         |
         v
+------------------------------------------------------------------+
|  Stage 2: Core Generation (entities, builders, EF Core)          |
|    - Content.Parts SG: EF owned types, part field accessors      |
|    - Content.Blocks SG: JSON converters, IContentBlock impls     |
|    - Content.StreamFields SG: JSON array column mappings         |
|    - Workflow.StateMachine SG: stage enum, transition validator   |
|    - Workflow.Gates SG: gate evaluator implementations            |
+------------------------------------------------------------------+
         |
         v
+------------------------------------------------------------------+
|  Stage 3: Cross-Cutting Generation (admin, pages, API, workflows)|
|    - Admin.Lists SG: Blazor list pages, filters, paging          |
|    - Admin.Forms SG: Blazor form pages, field widgets             |
|    - Admin.Actions SG: batch action handlers                     |
|    - Pages.Widgets SG: WidgetCatalog, config forms                |
+------------------------------------------------------------------+
         |
         v
+------------------------------------------------------------------+
|  Stage 4: Traceability (Requirements SG)                         |
|    - RequirementRegistry linking M1 models to features            |
|    - TraceabilityMatrix: spec --> implementation --> tests         |
+------------------------------------------------------------------+
```

Each stage depends on the outputs of the previous stage. The Roslyn incremental SG pipeline
ensures only changed inputs trigger regeneration. Companion concept lifecycle hooks
(`OnDiscovered`, `OnBeforeValidation`, `OnBeforeEmit`, `OnAfterEmit`) allow sub-DSLs to
participate in stages they do not own.

---

## 8. Plugin / Template System

### NuGet Packages (Compile-Time Extensions)

Each Diem sub-DSL ships as a NuGet package. A developer adds DSLs by adding package
references -- the source generators activate automatically:

```xml
<PackageReference Include="FrenchExDev.Net.Diem.Content.Parts" />
<PackageReference Include="FrenchExDev.Net.Diem.Admin.Lists" />
<PackageReference Include="FrenchExDev.Net.Diem.Workflow.StateMachine" />
```

Third-party DSL extensions follow the same pattern: ship an `.Attributes` + `.SourceGenerator`
package pair.

### Runtime Discovery

Widget types, content parts, and blocks are discovered at runtime via assembly scanning.
The generated `WidgetCatalog`, `PartRegistry`, and `BlockRegistry` classes enumerate all
discovered types from loaded assemblies.

### CLI Install

```
cmf install template-pack-xxx
```

The `cmf` CLI (`FrenchExDev.Net.Diem.Cli`) provides scaffolding commands:

| Command | Purpose |
|---------|---------|
| `cmf new part` | Scaffold a content part (Attributes + runtime class) |
| `cmf new block` | Scaffold a content block |
| `cmf new widget` | Scaffold a page widget |
| `cmf new admin-module` | Scaffold an admin module |
| `cmf new workflow` | Scaffold a workflow definition |
| `cmf new page` | Scaffold a page binding |

The CLI is split into `Diem.Cli` (entry point, PackAsTool) and `Diem.Cli.Lib` (reusable
scaffolding logic, TUI, template engine).

---

## 9. Aspire Orchestration

### AppHost (`FrenchExDev.Net.Diem.Aspire.AppHost`)

.NET Aspire AppHost for local development orchestration. Coordinates the web server,
database, MinIO (media storage), and any background workers into a single `F5` experience.

### ServiceDefaults (`FrenchExDev.Net.Diem.Aspire.ServiceDefaults`)

Shared Aspire service defaults:

- OpenTelemetry configuration (traces, metrics, logs) -- integrates with `DiemMetrics`
- Health checks for database, media storage, search engine
- Resilience policies (retry, circuit breaker)
- Service discovery

---

## 10. Project Dependency Graph

### Source Projects (58 total)

```
FrenchExDev.Net.Diem                          <-- CMF core (DI, pipeline, DiemComponentBase)
|
+-- Content.Parts                             <-- Part runtime
|   +-- Content.Parts.Attributes              <-- [ContentPart], [PartField], [HasPart]
|   +-- Content.Parts.SourceGenerator         <-- Part SG (Roslyn)
|   +-- Content.Parts.Design                  <-- cmf new part
|
+-- Content.Blocks                            <-- Block runtime (IContentBlock)
|   +-- Content.Blocks.Attributes             <-- [StructBlock], [ListBlock], [StreamBlock], [BlockField]
|   +-- Content.Blocks.SourceGenerator        <-- Block SG
|   +-- Content.Blocks.Design                 <-- cmf new block
|
+-- Content.StreamFields                      <-- StreamField runtime
|   +-- Content.StreamFields.Attributes       <-- [StreamField]
|   +-- Content.StreamFields.SourceGenerator  <-- StreamField SG
|
+-- Admin.Lists                               <-- List runtime (paging, sorting, filtering)
|   +-- Admin.Lists.Attributes                <-- [AdminModule], [AdminFilter]
|   +-- Admin.Lists.SourceGenerator           <-- List page SG
|
+-- Admin.Forms                               <-- Form runtime (validation, nested editing)
|   +-- Admin.Forms.Attributes                <-- [AdminField]
|   +-- Admin.Forms.SourceGenerator           <-- Form page SG
|
+-- Admin.Actions                             <-- Batch action runtime
|   +-- Admin.Actions.Attributes              <-- [AdminAction]
|   +-- Admin.Actions.SourceGenerator         <-- Action handler SG
|
+-- Admin.Design                              <-- cmf new admin-module
|
+-- Pages.Widgets                             <-- Widget runtime (catalog, config)
|   +-- Pages.Widgets.Attributes              <-- [PageWidget], [WidgetConfig]
|   +-- Pages.Widgets.SourceGenerator         <-- Widget catalog SG
|   +-- Pages.Widgets.Design                  <-- cmf new widget
|
+-- Pages.Layouts                             <-- Layout/Area/Zone entities
|   +-- Pages.Layouts.Attributes              <-- [Layout], [Area], [Zone]
|
+-- Pages.Routing                             <-- PageRouter, materialized paths
|   +-- Pages.Routing.Attributes              <-- [BoundEntity]
|
+-- Pages.Design                              <-- cmf new page
|
+-- Workflow.StateMachine                     <-- Stages, transitions, engine
|   +-- Workflow.StateMachine.Attributes      <-- [Workflow], [Stage], [Transition], [HasWorkflow]
|   +-- Workflow.StateMachine.SourceGenerator <-- StateMachine SG
|
+-- Workflow.Gates                            <-- Gate evaluation runtime
|   +-- Workflow.Gates.Attributes             <-- [Gate], [RequiresRole], [RequiresApproval]
|   +-- Workflow.Gates.SourceGenerator        <-- Gate evaluator SG
|
+-- Workflow.Scheduling                       <-- Timed transitions, BackgroundService
|   +-- Workflow.Scheduling.Attributes        <-- [ScheduledTransition]
|
+-- Workflow.Locales                          <-- Per-locale progress tracking
|   +-- Workflow.Locales.Attributes           <-- [ForEachLocale]
|
+-- Workflow.Design                           <-- cmf new workflow
|
+-- Media                                     <-- IMediaStorage interface
|   +-- Media.FileSystem                      <-- Local FS implementation
|   +-- Media.Minio                           <-- MinIO (S3) implementation
|
+-- Search                                    <-- ISearchEngine interface
|   +-- Search.Lucene                         <-- Lucene.NET implementation
|
+-- Identity                                  <-- DiemUser, DiemRoles, IDiemUserContext
+-- Caching                                   <-- IDiemCache, tag-based invalidation
+-- Metrics                                   <-- OpenTelemetry instruments
+-- RealTime                                  <-- IDiemHubClient (SignalR)
+-- Notifications                             <-- INotificationService
|
+-- Cli.Lib                                   <-- CLI reusable features
+-- Cli                                       <-- cmf entry point (Exe, PackAsTool)
|
+-- Aspire.AppHost                            <-- Local dev orchestration
+-- Aspire.ServiceDefaults                    <-- OTel, health checks
```

### External Dependencies

```
FrenchExDev.Net.Dsl                           <-- M3 metamodel framework (standalone solution)
FrenchExDev.Net.Ddd                           <-- DDD DSL (standalone solution)
FrenchExDev.Net.Requirements                  <-- Requirements DSL (standalone solution)
FrenchExDev.Net.Result                        <-- Result monad ([Invariant] return type)
FrenchExDev.Net.Builder                       <-- AbstractBuilder<T> + BuilderEmitter
```

### Test Projects (6 total)

```
FrenchExDev.Net.Diem.Tests                    <-- Integration tests (core)
FrenchExDev.Net.Diem.Content.Tests            <-- Parts + Blocks + StreamFields
FrenchExDev.Net.Diem.Admin.Tests              <-- Lists + Forms + Actions
FrenchExDev.Net.Diem.Pages.Tests              <-- Widgets + Layouts + Routing
FrenchExDev.Net.Diem.Workflow.Tests           <-- StateMachine + Gates + Scheduling + Locales
FrenchExDev.Net.Diem.Cli.Tests                <-- CLI commands
```

---

## 11. Design Decisions

### Why sub-DSLs inside Diem (not standalone)?

Content, Admin, Pages, and Workflow are all intrinsic to what a CMF *is*. They share concepts
cross-cuttingly: an `[AdminModule]` references an `[AggregateRoot]`; a `[PageWidget]` renders
content from `[HasPart]` entities; `[HasWorkflow]` attaches to the same entities that
`[AdminModule]` manages. Extracting them into standalone solutions would create circular
dependencies or require an orchestration layer that adds complexity without benefit.

By contrast, truly independent concerns (Dsl, Ddd, Result, Builder, Requirements) *are*
standalone solutions because they have no knowledge of CMF concepts.

### Why each built-in is its own project?

Each Part (RoutablePart, SeoablePart, etc.) and Block (HeroBlock, RichTextBlock, etc.) lives
in its sub-DSL's runtime project, not in a separate project per built-in. However, each
*sub-DSL* (Parts vs Blocks vs StreamFields, Lists vs Forms vs Actions, etc.) is its own
project because:

1. **Independent source generators.** Each SG targets a specific attribute set. Combining
   them would create a monolithic generator that is harder to test, debug, and version.

2. **Selective adoption.** A project can reference `Diem.Content.Parts` without pulling in
   `Diem.Admin.Lists`. NuGet packages map to projects -- finer granularity means smaller
   dependency closures.

3. **Parallel compilation.** MSBuild can compile independent projects in parallel. More
   projects = faster builds when the dependency graph allows it.

### Why partial class instead of PHP inheritance?

PHP lacked `partial class`. The only way to combine framework-generated code with developer
code was through inheritance -- hence the 6-layer chain. In C#, `partial class` lets the
source generator and the developer contribute to the *same* class without inheritance. This
eliminates:

- Empty stub files that exist only as inheritance anchors
- The cognitive overhead of reasoning about 6 layers of `parent::method()` calls
- Name collisions between layers (C# `virtual`/`override` is explicit)

The result is a flatter, more discoverable API: the developer sees *one* class with virtual
methods they can override, not a chain of base classes they must mentally traverse.

### Why temporal versioning (VersionablePart) not draft/publish?

Traditional CMS draft/publish is a boolean state: content is either visible or not. Diem uses
temporal versioning instead:

- **`VersionablePart`** tracks `VersionNumber`, `ValidFrom`, `ValidTo`, `IsCurrent`
- **`SchedulablePart`** tracks `PublishAt`, `UnpublishAt`, `IsPublished`
- **`Workflow.StateMachine`** manages editorial state transitions (Draft --> Review --> Published)

This three-part approach is more expressive:

1. **Temporal queries.** "Show me the version that was live on 2025-12-01" is a simple
   `WHERE ValidFrom <= @date AND (ValidTo IS NULL OR ValidTo > @date)` query. Draft/publish
   cannot answer historical questions.

2. **Non-destructive edits.** Editing creates a new version; the previous version remains
   queryable. Draft/publish overwrites in place.

3. **Separation of concerns.** Versioning (data), scheduling (time), and editorial workflow
   (process) are orthogonal parts that compose independently. A content type can have
   versioning without a workflow, or a workflow without scheduling.
