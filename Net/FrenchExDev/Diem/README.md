# Diem CMF

A **Content Management Framework** for .NET 10, built on attribute-based DSLs and Roslyn source generation.

A CMF is not a CMS. A CMS is an application (WordPress, Drupal) -- it ships with opinions baked in. A CMF is a framework -- it starts empty. The developer defines the domain using C# attributes; the compiler generates the entire application stack: admin CRUD, front-end components, page trees, workflows, and infrastructure wiring.

## Origin

The original Diem (2010) was a PHP/Symfony 1.4 CMF. It used `schema.yml` to define models and `modules.yml` to declare modules. Running `dm:setup` generated admin CRUD, front-end components, and page trees. Pages were composed from Layouts, Areas, Zones, and Widgets.

This C# version replaces YAML with C# attributes and runtime interpretation with Roslyn source generation. The composition model is preserved: Page > Layout > Area > Zone > WidgetInstance.

## Architecture

Four sub-DSLs plus shared infrastructure, all built on `FrenchExDev.Net.Dsl` (M3 meta-metamodel) and `FrenchExDev.Net.Ddd` (DDD DSL).

| Sub-DSL | Purpose | Attributes | Source Generator |
|---------|---------|------------|-----------------|
| **Content** | Parts, Blocks, StreamFields | `[HasPart]`, `[ContentBlock]`, `[StreamField]` | 3 generators |
| **Admin** | Lists, Forms, Actions | `[AdminModule]`, `[AdminField]`, `[AdminAction]` | 3 generators |
| **Pages** | Widgets, Layouts, Routing | `[PageWidget]`, `[WidgetConfig]`, routing attrs | 1 generator |
| **Workflow** | StateMachine, Gates, Scheduling, Locales | `[Workflow]`, `[Stage]`, `[Transition]`, `[RequiresRole]` | 2 generators |

### Content Sub-DSL (Parts + Blocks + StreamFields)

**Parts** provide horizontal composition -- cross-cutting concerns added to any entity.

| Part | What it adds |
|------|-------------|
| `RoutablePart` | URL slug, canonical path |
| `SeoablePart` | Meta title, description, OG tags |
| `TaggablePart` | Taxonomy tags |
| `AuditablePart` | CreatedAt, UpdatedAt, CreatedBy, UpdatedBy |
| `VersionablePart` | Temporal data versioning (ValidFrom/ValidTo) |
| `LocalizablePart` | Per-locale content variants |
| `SortablePart` | Drag-and-drop ordering (Position) |
| `SchedulablePart` | PublishAt, UnpublishAt |
| `MediablePart` | Attached media files |

**Blocks** provide vertical composition -- structured content types. 4 built-in: Hero, RichText, Testimonial, Image. All implement `IContentBlock`.

**StreamFields** are ordered polymorphic sequences of blocks, stored as JSON.

### Admin Sub-DSL (Lists + Forms + Actions)

- **Lists**: `[AdminModule("Products", typeof(Product))]` generates a paginated list page with filters
- **Forms**: `[AdminField("Price", DisplayType = "Currency")]` generates create/edit forms
- **Actions**: `[AdminAction("Publish", Command = "PublishProduct")]` generates batch actions

### Pages Sub-DSL (Widgets + Layouts + Routing)

- **Layouts**: Page > Layout > Area > Zone > WidgetInstance (preserving the original Diem composition model)
- **Widgets**: `[PageWidget("ProductList", Module = "Product")]` generates placeable Blazor components
- **Routing**: Materialized path URL resolution with bound entity pages

### Workflow Sub-DSL (StateMachine + Gates + Scheduling + Locales)

- **StateMachine**: `[Workflow]` + `[Stage]` + `[Transition]` generates a WorkflowEngine with transition validation
- **Gates**: `[RequiresRole]`, `[RequiresApproval]` add guard conditions on transitions
- **Scheduling**: `[ScheduledTransition]` enables timed auto-transitions via BackgroundService
- **Locales**: `[ForEachLocale]` enables per-locale progress tracking

### Infrastructure

| Service | Interface | Implementations |
|---------|-----------|----------------|
| Media | `IMediaStorage` | FileSystem, Minio/S3 |
| Search | `ISearchEngine` | Lucene.Net |
| Identity | `IDiemUserContext` | `DiemUser`, `DiemRoles` (Admin/Editor/Author/Publisher/Viewer) |
| Caching | `IDiemCache` | Tag-based invalidation (widget/page/query cache) |
| Metrics | -- | OpenTelemetry counters (page views, widget render time, content edits, workflow transitions) |
| RealTime | `IDiemHubClient` | SignalR hub for admin collaboration |
| Notifications | `INotificationService` | Email, Push, InApp |

### CLI (`cmf` command)

| Command | Description |
|---------|-------------|
| `cmf new` | Scaffold a new Diem project |
| `cmf add` | Add a sub-DSL or module |
| `cmf generate` | Run source generators |
| `cmf validate` | Validate DSL model consistency |
| `cmf migrate` | Generate/apply EF Core migrations |
| `cmf report` | Output DSL model report |
| `cmf design` | Launch design-time tooling |
| `cmf install` | Install a Diem plugin |
| `cmf list` | List installed modules/plugins |
| `cmf uninstall` | Remove a Diem plugin |

### Aspire

AppHost for local dev orchestration: server + client + worker + PostgreSQL + Redis + Minio.

## Developer Experience

What the developer writes:

```csharp
// 1. Define the domain model
[AggregateRoot("Product", BoundedContext = "Catalog")]
[HasPart(typeof(RoutablePart))]
[HasPart(typeof(SeoablePart))]
[HasPart(typeof(VersionablePart))]
public partial class Product
{
    [EntityId] public partial ProductId Id { get; }
    [Property("Name", Required = true)] public partial string Name { get; }
    [Property("Price", Required = true)] public partial decimal Price { get; }
    [Composition] public partial IReadOnlyList<ProductVariant> Variants { get; }

    [Invariant("Price must be positive")]
    private Result PriceIsPositive() => Price > 0 ? Result.Success() : Result.Failure("...");
}

// 2. Declare admin module
[AdminModule("Products", typeof(Product), Icon = "box", Group = "Catalog")]
[AdminFilter("Category", FilterType = "Dropdown")]
[AdminAction("Publish", Command = "PublishProduct", RequiresRole = "Editor")]
public partial class ProductsAdminModule { }

// 3. Create a page widget
[PageWidget("ProductList", Module = "Product", Icon = "grid")]
public partial class ProductListWidget
{
    [WidgetConfig(DisplayName = "Category")] public string? Category { get; set; }
    [WidgetConfig(DisplayName = "Page Size")] public int PageSize { get; set; } = 12;
}

// 4. Define editorial workflow
[Workflow("Editorial")]
[Stage("Draft", IsInitial = true)]
[Stage("Review")]
[Stage("Published", IsFinal = true)]
[Transition("Submit", From = "Draft", To = "Review")]
[Transition("Approve", From = "Review", To = "Published")]
[RequiresRole("Approve", Role = "Editor")]
public partial class EditorialWorkflow { }
```

The compiler generates the rest: admin list/form/action pages, page widgets, workflow engine, routing, validation, and all infrastructure wiring.

## 4-Layer Customization Model

Carried over from the original Diem PHP architecture -- every generated component can be extended without touching generated code:

```
Layer 1: DiemComponentBase               (framework -- shipped in Diem)
Layer 2: DiemFrontModuleBase<T>          (sub-DSL base -- virtual methods)
Layer 3: ArticleComponents.g.cs          (generated -- regenerated every build)
Layer 4: ArticleComponents.cs            (developer's partial class -- never overwritten)
```

Layer 3 is always safe to regenerate. Layer 4 is the developer's extension point. Virtual methods in Layer 2 provide hooks that Layer 3 calls and Layer 4 can override.

## Project Structure (62 projects)

```
Diem/
  FrenchExDev.Net.Diem.slnx
  doc/
    ARCHITECTURE.md
    HOW-TO.md
    PLAN.md
  src/
    # Core
    FrenchExDev.Net.Diem/                                  # DiemComponentBase, options, DI

    # Content Sub-DSL (Parts)
    FrenchExDev.Net.Diem.Content.Parts/                    # Runtime: 9 built-in parts
    FrenchExDev.Net.Diem.Content.Parts.Attributes/         # [HasPart] attribute + concepts
    FrenchExDev.Net.Diem.Content.Parts.Design/             # Design-time tooling
    FrenchExDev.Net.Diem.Content.Parts.SourceGenerator/    # Roslyn SG

    # Content Sub-DSL (Blocks)
    FrenchExDev.Net.Diem.Content.Blocks/                   # Runtime: Hero, RichText, Testimonial, Image
    FrenchExDev.Net.Diem.Content.Blocks.Attributes/        # [ContentBlock] attribute + concepts
    FrenchExDev.Net.Diem.Content.Blocks.Design/            # Design-time tooling
    FrenchExDev.Net.Diem.Content.Blocks.SourceGenerator/   # Roslyn SG

    # Content Sub-DSL (StreamFields)
    FrenchExDev.Net.Diem.Content.StreamFields/             # Runtime: polymorphic JSON sequences
    FrenchExDev.Net.Diem.Content.StreamFields.Attributes/  # [StreamField] attribute + concepts
    FrenchExDev.Net.Diem.Content.StreamFields.SourceGenerator/

    # Admin Sub-DSL (Lists)
    FrenchExDev.Net.Diem.Admin.Lists/                      # Runtime: paginated list pages
    FrenchExDev.Net.Diem.Admin.Lists.Attributes/           # [AdminModule], [AdminFilter]
    FrenchExDev.Net.Diem.Admin.Lists.SourceGenerator/      # Roslyn SG

    # Admin Sub-DSL (Forms)
    FrenchExDev.Net.Diem.Admin.Forms/                      # Runtime: create/edit forms
    FrenchExDev.Net.Diem.Admin.Forms.Attributes/           # [AdminField]
    FrenchExDev.Net.Diem.Admin.Forms.SourceGenerator/      # Roslyn SG

    # Admin Sub-DSL (Actions)
    FrenchExDev.Net.Diem.Admin.Actions/                    # Runtime: batch actions
    FrenchExDev.Net.Diem.Admin.Actions.Attributes/         # [AdminAction]
    FrenchExDev.Net.Diem.Admin.Actions.SourceGenerator/    # Roslyn SG
    FrenchExDev.Net.Diem.Admin.Design/                     # Admin design-time tooling

    # Pages Sub-DSL (Layouts)
    FrenchExDev.Net.Diem.Pages.Layouts/                    # Runtime: Layout > Area > Zone
    FrenchExDev.Net.Diem.Pages.Layouts.Attributes/         # Layout attributes

    # Pages Sub-DSL (Routing)
    FrenchExDev.Net.Diem.Pages.Routing/                    # Runtime: materialized path resolution
    FrenchExDev.Net.Diem.Pages.Routing.Attributes/         # Routing attributes

    # Pages Sub-DSL (Widgets)
    FrenchExDev.Net.Diem.Pages.Widgets/                    # Runtime: Blazor widget components
    FrenchExDev.Net.Diem.Pages.Widgets.Attributes/         # [PageWidget], [WidgetConfig]
    FrenchExDev.Net.Diem.Pages.Widgets.Design/             # Widget design-time tooling
    FrenchExDev.Net.Diem.Pages.Widgets.SourceGenerator/    # Roslyn SG
    FrenchExDev.Net.Diem.Pages.Design/                     # Pages design-time tooling

    # Workflow Sub-DSL (StateMachine)
    FrenchExDev.Net.Diem.Workflow.StateMachine/            # Runtime: WorkflowEngine
    FrenchExDev.Net.Diem.Workflow.StateMachine.Attributes/ # [Workflow], [Stage], [Transition]
    FrenchExDev.Net.Diem.Workflow.StateMachine.SourceGenerator/

    # Workflow Sub-DSL (Gates)
    FrenchExDev.Net.Diem.Workflow.Gates/                   # Runtime: guard conditions
    FrenchExDev.Net.Diem.Workflow.Gates.Attributes/        # [RequiresRole], [RequiresApproval]
    FrenchExDev.Net.Diem.Workflow.Gates.SourceGenerator/   # Roslyn SG

    # Workflow Sub-DSL (Scheduling)
    FrenchExDev.Net.Diem.Workflow.Scheduling/              # Runtime: BackgroundService transitions
    FrenchExDev.Net.Diem.Workflow.Scheduling.Attributes/   # [ScheduledTransition]

    # Workflow Sub-DSL (Locales)
    FrenchExDev.Net.Diem.Workflow.Locales/                 # Runtime: per-locale tracking
    FrenchExDev.Net.Diem.Workflow.Locales.Attributes/      # [ForEachLocale]
    FrenchExDev.Net.Diem.Workflow.Design/                  # Workflow design-time tooling

    # Infrastructure
    FrenchExDev.Net.Diem.Media/                            # IMediaStorage abstraction
    FrenchExDev.Net.Diem.Media.FileSystem/                 # FileSystem backend
    FrenchExDev.Net.Diem.Media.Minio/                      # Minio/S3 backend
    FrenchExDev.Net.Diem.Search/                           # ISearchEngine abstraction
    FrenchExDev.Net.Diem.Search.Lucene/                    # Lucene.Net backend
    FrenchExDev.Net.Diem.Identity/                         # DiemUser, DiemRoles, IDiemUserContext
    FrenchExDev.Net.Diem.Caching/                          # IDiemCache, tag-based invalidation
    FrenchExDev.Net.Diem.Metrics/                          # OpenTelemetry counters
    FrenchExDev.Net.Diem.RealTime/                         # SignalR hub, IDiemHubClient
    FrenchExDev.Net.Diem.Notifications/                    # INotificationService (Email/Push/InApp)

    # CLI
    FrenchExDev.Net.Diem.Cli/                              # cmf command entry point
    FrenchExDev.Net.Diem.Cli.Lib/                          # CLI shared logic

    # Aspire
    FrenchExDev.Net.Diem.Aspire.AppHost/                   # Local dev orchestration
    FrenchExDev.Net.Diem.Aspire.ServiceDefaults/           # Shared Aspire defaults

  test/
    FrenchExDev.Net.Diem.Tests/                            # Core framework tests
    FrenchExDev.Net.Diem.Content.Tests/                    # Content sub-DSL tests
    FrenchExDev.Net.Diem.Admin.Tests/                      # Admin sub-DSL tests
    FrenchExDev.Net.Diem.Pages.Tests/                      # Pages sub-DSL tests
    FrenchExDev.Net.Diem.Workflow.Tests/                   # Workflow sub-DSL tests
    FrenchExDev.Net.Diem.Cli.Tests/                        # CLI tests
```

## Dependencies

| Package | Role |
|---------|------|
| `FrenchExDev.Net.Dsl` | M3 meta-metamodel framework (attribute + concept infrastructure) |
| `FrenchExDev.Net.Ddd` | DDD DSL (`[AggregateRoot]`, `[EntityId]`, `[Property]`, `[Invariant]`) |
| `FrenchExDev.Net.Requirements` | Feature tracking and traceability |
| `FrenchExDev.Net.Result` | `Result` / `Result<T>` return type for invariants and validation |
| `FrenchExDev.Net.Builder` | Entity builder generation (AbstractBuilder, BuilderEmitter) |

## Tests

81 tests across 6 test projects, all passing.

| Test Project | Scope |
|-------------|-------|
| `FrenchExDev.Net.Diem.Tests` | Core framework |
| `FrenchExDev.Net.Diem.Content.Tests` | Parts, Blocks, StreamFields |
| `FrenchExDev.Net.Diem.Admin.Tests` | Lists, Forms, Actions |
| `FrenchExDev.Net.Diem.Pages.Tests` | Widgets, Layouts, Routing |
| `FrenchExDev.Net.Diem.Workflow.Tests` | StateMachine, Gates, Scheduling, Locales |
| `FrenchExDev.Net.Diem.Cli.Tests` | CLI commands |

## Further Reading

- [doc/ARCHITECTURE.md](doc/ARCHITECTURE.md) -- detailed architecture and design decisions
- [doc/HOW-TO.md](doc/HOW-TO.md) -- guides for common tasks
- [doc/PLAN.md](doc/PLAN.md) -- project roadmap
