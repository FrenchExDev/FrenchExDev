# DIEM-CMF — Philosophy

A Content Management Framework (CMF) is not a Content Management System (CMS). A CMS ships an application with opinions baked in (WordPress, Drupal, Strapi); you adapt the application to your domain. A CMF starts empty; the developer declares the domain and the framework generates the application around it.

This skill captures the reusable philosophy behind a declaration-first CMF built on attribute DSLs and source generation. It is not about a single product — it is about the discipline of *defining the domain once and generating everything else*.

## The Core Belief

**Define the domain once. Generate everything else.**

A developer should write a few hundred lines of attributed C# describing the *what*: the domain model, the content structure, the admin interface, the page composition, the editorial workflow. The compiler should write thousands of lines implementing the *how*: persistence, API, admin UI, routing, state machines, traceability.

The developer writes the **what**. The compiler writes the **how**.

## CMF vs CMS

| Aspect | CMS (application) | CMF (framework) |
|--------|-------------------|-----------------|
| Starting point | Prebuilt application | Empty project |
| Extension model | Plugins, hooks, filters | DSL attributes, source generation |
| Data model | Generic (post meta, custom fields) | Domain-specific (typed entities) |
| Type safety | String-based, runtime | Compiler-checked |
| Generated code | None (interpreted) | Full stack (entities to API to UI) |
| Who controls architecture | The CMS | The developer |

A CMS says: "Here is an application. Adapt it to your needs."
A CMF says: "Here are tools. Build your application."

Most enterprise content platforms are built by *fighting* a CMS. The team installs WordPress or Drupal, then spends months making it do something it wasn't designed for. A CMF inverts this: define the domain in the language of the business, and let the framework generate exactly what you asked for — nothing more, nothing less.

## The Four Disciplines

A declaration-first CMF combines four disciplines that are usually practiced separately.

### 1. Domain-Driven Design

The domain model is not an afterthought shoved into a generic "content type" system. It is the **center** of the architecture. Aggregates, entities, value objects, compositions, associations, invariants — these are first-class concepts with dedicated attributes and compile-time validation.

An `Order` is not a "content type with custom fields." It is an aggregate root with typed lines, a shipping address value object, invariants that prevent invalid state, and a command/event lifecycle. The compiler knows this and generates code that respects these boundaries.

### 2. Content Composition (Parts + Blocks)

Content is composed from reusable units, in two orthogonal directions:

- **Parts (horizontal)** — cross-cutting concerns added to any entity. An entity gains SEO metadata by attaching `[HasPart(typeof(SeoablePart))]`. URL routing, temporal versioning, taxonomy tags, soft delete, audit fields, scheduling — each is a part.
- **Blocks / StreamFields (vertical)** — structured content shapes. A page body is not raw HTML; it is an ordered sequence of typed blocks (a hero, then rich text, then a testimonial). Each block has a schema, a JSON serialization, and a rendering component.

The entity's domain model stays clean. Parts handle cross-cutting concerns. Blocks handle structured content. This was inspired by Orchard Core (parts), Wagtail (StreamFields), and Drupal (content types), and unified under a single DSL.

### 3. Editorial Workflows

Publication is not a binary flag. It is a pipeline with stages, transitions, guards, and audit trails.

A blog post moves from Draft to Review to Translation to Published. Each transition has guards: only editors approve, two approvals are required, all locales must be translated before publication. The workflow is declared with attributes (`[Workflow]`, `[Stage]`, `[Transition]`, `[RequiresRole]`). The compiler generates the state machine, the gate evaluator, and the domain events.

### 4. Source Generation

From the attributes, source generators produce the entire stack:

- Entity implementations with backing fields and invariant enforcement
- Fluent builders that validate before construction
- Persistence configurations with correct relationship mappings
- Repository interfaces and implementations
- Command handlers that enforce invariants
- Admin CRUD pages (list, form, detail, batch actions)
- Page widgets with configuration forms
- Workflow state machines with guard conditions
- Requirement registries and traceability matrices

The developer writes partial classes with attributes. The compiler writes the other half. The two halves merge at compilation. The developer's code is never overwritten. The generated code is regenerated on every build.

## The Three Audiences

A CMF serves three audiences with the same codebase, and none of them needs to understand the others' tools.

**Developers** write domain models with DSL attributes, customize generated code via partial classes, and write tests. Their tool is the compiler. They work in their IDE.

**Content editors** create pages, compose layouts with widgets, manage records through auto-generated admin interfaces, and control publication through workflows. Their tool is the admin UI. They work in the browser.

**DevOps** build images, deploy with container orchestration, and configure environments. Their tool is `dotnet publish`. They work with CI/CD pipelines.

The developer doesn't configure infrastructure. The editor doesn't write code. The DevOps engineer doesn't manage content. The CMF sits between them, translating DSL declarations into running infrastructure.

## Why Sub-DSLs Compose

A CMF is not one giant DSL. It is a set of small, focused sub-DSLs that all speak the same metamodel language:

- **DDD sub-DSL** — aggregates, entities, value objects, invariants
- **Content sub-DSL** — parts, blocks, stream fields
- **Admin sub-DSL** — list pages, forms, batch actions
- **Pages sub-DSL** — widgets, layouts, areas, zones, routing
- **Workflow sub-DSL** — state machines, guards, scheduling, locales
- **Requirements sub-DSL** — feature traceability via `[TracedBy]`

A single entity can carry attributes from all of them simultaneously. They compose because they share the same M3 metamodel foundation: every attribute is a `[MetaConcept]`, every concept is self-describing, the registry is automatic. New sub-DSLs are added without touching existing ones.

## The Customization Contract

Every generated component must be extensible without touching generated code. Use a four-layer hierarchy of partial classes and virtual methods:

```
Layer 1: ComponentBase                Framework base (services, logging, DI)
Layer 2: SubDslBase<T>                Sub-DSL base (virtual hooks)
Layer 3: ArticleComponents.g.cs       Generated (regenerated every build)
Layer 4: ArticleComponents.cs         Developer's partial class (never overwritten)
```

Layer 3 is always safe to regenerate. Layer 4 is the developer's extension point. Virtual methods in Layer 2 provide hooks that Layer 3 calls and Layer 4 can override.

The developer can:

- **Override virtual methods** to change behavior (`OnCustomizeListQuery`)
- **Override entire methods** to replace behavior (`ExecuteListAsync`)
- **Add new methods** for custom functionality
- **Do nothing** and get sensible defaults

This was carried over from the original Diem PHP architecture, where empty `my*` stubs sat between framework layers as override points. Partial classes + virtual methods give the same precision in C#.

## Why Declarations, Not Configuration

Configuration files (YAML, JSON, XML) are pragmatic but limited:

- **No type safety.** A typo is a runtime error.
- **No IDE support.** No autocomplete, no refactoring, no go-to-definition.
- **No compile-time validation.** Invalid models discovered at startup.
- **Two languages.** The developer switches between code and config constantly.

C# attributes solve all four. `[AggregateRoot("Order")]` is checked by the compiler. `typeof()` and `nameof()` are refactor-safe. The source generator validates the model before any code runs. The developer never leaves their IDE.

## Why Source Generation Beats Runtime Codegen

Runtime code generation (the original Diem ran `dm:setup` and produced PHP files on disk) had problems:

- Generated files in the working directory create version-control noise
- Manual regeneration step required after every schema change
- No incremental processing

Compile-time source generators solve all three. Generated files are virtual: they exist in the compilation but not on disk. The generator runs on every build automatically. Only changed models are reprocessed.

The developer writes an attribute, saves, and the generated code exists. No command to run. No files to manage.

## Why This Matters

The result is a content platform that is **100% specific to the business's needs**. No unused features. No plugin tax. No generic data model struggling to fit a specific domain. Type-safe from the domain model to the database schema to the API to the admin UI.

Define the domain once. Generate everything else.
