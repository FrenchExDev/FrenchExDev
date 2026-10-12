# Philosophy — FrenchExDev.Net.Diem

## The Core Belief

**Define the domain once. Generate everything else.**

A developer should write ~300 lines of attributed C# describing what they want: the domain model, the content structure, the admin interface, the page composition, the editorial workflow. The compiler should write ~8,000 lines implementing how it works: persistence, API, admin UI, routing, state machines, traceability.

The developer writes the **what**. The compiler writes the **how**.

## Where This Started

In 2010, Thibault Duplessis built Diem — a Content Management Framework on PHP/Symfony 1.4. It was not WordPress. It was not Drupal. It started empty.

A developer wrote ~200 lines of YAML:
- `schema.yml` defined the data model
- `modules.yml` declared the modules and their components

Then ran `php symfony dm:setup`. Diem generated ~5,000 lines of PHP:
- Admin interfaces with list views, forms, filters, batch actions
- Front-end components with list and show templates
- A page tree where each record had its own page
- Layouts with areas, zones, and draggable widgets

The generated code was not version-controlled. It was regenerated on every run. But it was extensible through a class hierarchy where the developer's code sat above the generated code and could override anything.

That was 2010. PHP. YAML. Runtime code generation.

This is 2026. C#. Attributes. Compile-time source generation.

The belief hasn't changed. The tools have.

## Why a Framework, Not an Application

A CMS is an application. You install WordPress, you get a blog. You install Drupal, you get a content management system. Then you fight the application to make it do what you need — plugins, hooks, filters, configuration files, theme overrides.

A CMF is a framework. You install Diem, you get nothing. Then you declare what you want, and the framework generates exactly that — nothing more, nothing less. There is no default blog. There is no default theme. There is no comments module you have to disable.

The distinction matters because it determines who is in control:

| Aspect | CMS (application) | CMF (framework) |
|--------|-------------------|-----------------|
| Starting point | Prebuilt application | Empty project |
| Extension model | Plugins, hooks, filters | DSL attributes, source generation |
| Data model | Generic (post meta, custom fields) | Domain-specific (typed entities) |
| Type safety | String-based | Compiler-checked |
| Generated code | None (interpreted at runtime) | Full stack (entities to API to UI) |
| Performance | Runtime interpretation | Ahead-of-time compilation |
| Who controls architecture | The CMS | The developer |

A CMS says: "Here is an application. Adapt it to your needs."
A CMF says: "Here are tools. Build your application."

## The Four Disciplines

Diem combines four disciplines that are usually practiced separately.

### 1. Domain-Driven Design

The domain model is not an afterthought shoved into a generic "content type" system. It is the center of the architecture. Aggregates, entities, value objects, compositions, associations, invariants — these are first-class concepts with dedicated attributes and compile-time validation.

An `Order` is not a "content type with custom fields." It is an aggregate root with typed lines, a shipping address value object, invariants that prevent invalid state, and a command/event lifecycle. The compiler knows this and generates code that respects these boundaries.

### 2. Content Composition

Content is not a monolithic blob. It is composed from reusable parts (horizontal) and structured blocks (vertical).

Parts add capabilities: an entity gains SEO metadata by attaching `[HasPart(typeof(SeoablePart))]`. It gains URL routing by attaching `[HasPart(typeof(RoutablePart))]`. It gains temporal versioning by attaching `[HasPart(typeof(VersionablePart))]`. The entity's domain model stays clean. The parts handle cross-cutting concerns.

Blocks structure content: a page body is not raw HTML. It is an ordered sequence of typed blocks — a hero banner, then rich text, then a testimonial, then an image. Each block has a schema, a JSON representation, and a rendering component. The developer defines which blocks are allowed in which fields.

This was inspired by Orchard Core (parts), Wagtail (StreamFields), and Drupal (content types). Diem unifies them under a single DSL.

### 3. Editorial Workflows

Content publication is not a binary switch. It is a pipeline with stages, transitions, guards, and audit trails.

A blog post moves from Draft to Review to Translation to Published. Each transition has guards: only editors can approve, two approvals are required, all locales must be translated before publication. The workflow is declared with attributes. The compiler generates the state machine, the gate evaluator, and the domain events.

This was a first-class citizen in the original Diem — content editors controlled the publication lifecycle from the admin interface. In this C# version, workflows are a full sub-DSL with scheduled transitions and locale tracking.

### 4. Source Generation

From the attributes, Roslyn source generators produce the entire stack:
- Entity implementations with backing fields and invariant enforcement
- Fluent builders that validate before construction
- EF Core configurations with correct relationship mappings
- Repository interfaces and implementations
- CQRS command handlers that enforce invariants
- Admin CRUD interfaces (list, form, detail, batch actions)
- Page widgets with configuration forms
- Workflow state machines with guard conditions
- Requirement registries and traceability matrices

The developer writes partial classes with attributes. The compiler writes the other half. The two halves merge at compilation. The developer's code is never overwritten. The generated code is regenerated on every build.

## The Page Model

The original Diem had a distinctive page composition model that set it apart from other CMFs:

```
Page
  +-- Layout (shared across pages)
        +-- Area: Top       (layout-level, same on all pages with this layout)
        +-- Area: Left
        +-- Area: Content   (page-level, unique per page)
        +-- Area: Right
        +-- Area: Bottom
              +-- Zone (column within an area)
                    +-- Widget (independent piece of content)
```

Pages are database records, not source files. Content editors create pages, assign layouts, and drag widgets into zones — all from the admin interface. Developers define widgets as Blazor components with `[PageWidget]` and configuration properties with `[WidgetConfig]`. The compiler generates the widget catalog and configuration forms. Editors place them.

This separation is fundamental: **developers define what widgets exist; editors decide where they go.** The developer writes code. The editor composes pages. Neither needs the other's permission.

## The Customization Contract

The original Diem had a 6-layer PHP class hierarchy:

```
articleComponents                        YOUR module code
  myFrontModuleComponents                YOUR module-level hook (empty stub)
    dmFrontModuleComponents              DIEM's module logic (getShowQuery, getListQuery, getPager)
      myFrontBaseComponents              YOUR cross-cutting hook (empty stub)
        dmFrontBaseComponents            DIEM's front-specific base
          dmBaseComponents               DIEM's core (services, routing)
            sfComponents                 SYMFONY framework
```

Two `my*` empty stubs sat between the framework layers, giving developers precise override points without modifying framework code. The C# version achieves the same with `partial class` + `virtual`/`override`:

```
Layer 1: DiemComponentBase           Framework base (services, logging, DI)
Layer 2: DiemFrontModuleBase<T>      Sub-DSL base (GetListQuery, GetPager, ApplyFilters)
Layer 3: ArticleComponents.g.cs      Generated (regenerated every build, virtual hooks)
Layer 4: ArticleComponents.cs        Developer's partial class (created once, never overwritten)
```

**The contract**: The compiler generates code with virtual methods and hook points. The developer overrides only what they need. Everything else works out of the box.

The developer can:

- **Override virtual methods** to change behavior (`OnCustomizeListQuery`)
- **Override entire methods** to replace behavior (`ExecuteListAsync`)
- **Add new methods** for custom functionality
- **Do nothing** and get sensible defaults

## The Metamodel Foundation

Every DSL attribute in Diem is built on the same M3 meta-metamodel (`FrenchExDev.Net.Dsl`). Five primitives. Self-describing. No M4.

This means:

1. **Every concept is self-describing.** `[ContentPart]` is `[MetaConcept(typeof(ContentPartConcept))]`. The system knows what concepts exist because they declare themselves.

2. **Every concept has behavioral companions.** The `ContentPartConcept` class carries validation logic, containment rules, and lifecycle hooks. The attribute stores metadata; the companion acts on it.

3. **Every concept has constraints.** `[MetaConstraint("MustHaveField", nameof(MustHaveFieldConstraint))]` references a real C# method that validates at compile time. No string expressions. Debuggable. Testable.

4. **The registry is automatic.** Add a new concept, and it appears in the MetamodelRegistry. No manual registration. The source generator discovers it.

5. **DSLs compose.** A single entity can use attributes from DDD (`[AggregateRoot]`), Content (`[HasPart]`), Workflow (`[HasWorkflow]`), and Admin (`[AdminModule]`) simultaneously. They all speak the same M3 language.

This is equivalent to OMG's Essential MOF and Eclipse's Ecore. The same fixed-point reflexivity: `MetaConcept` is a `[MetaConcept]` that describes `[MetaConcept]`s.

## Why Each Built-In Is Its Own Project

Every built-in part, block, widget, and page template is a separate project with its own test project and testing helpers. This is deliberate:

**Each built-in IS the documentation.** Want to know how to create a content part? Read `RoutablePart.cs`. Want to know how to test it? Read the tests. Want to know how to provide test helpers for consumers? Read the testing project.

Built-ins are not special. They use the exact same attributes and conventions that developers use. There is no internal API, no framework magic, no special path for built-in vs. custom. If you can read a built-in's source, you can build your own.

## Why Temporal Versioning

`VersionablePart` does not implement draft/publish workflow states. That is the Workflow sub-DSL's job.

`VersionablePart` implements **temporal data versioning**: every save creates a new version record with `ValidFrom` and `ValidTo` timestamps. You can browse the history of any entity, see what it looked like at any point in time, compare two versions, and restore an old version.

This was inspired by the original Diem's `DmVersionable` behavior. It is orthogonal to workflows: a blog post can be in "Draft" stage (workflow) while having 7 saved versions (temporal). The workflow controls when content becomes visible. Versioning controls the audit trail.

## Why C# Attributes Instead of YAML

The original Diem used YAML because PHP lacked attributes. YAML was pragmatic but fundamentally limited:

- **No type safety.** A typo in `schema.yml` was a runtime error.
- **No IDE support.** No autocomplete, no refactoring, no go-to-definition.
- **No compile-time validation.** Invalid schemas were discovered at `dm:setup` time.
- **Two languages.** The developer switched between PHP and YAML constantly.

C# attributes solve all four:

- **Type-safe.** `[AggregateRoot("Order")]` is checked by the compiler. `typeof()` and `nameof()` are refactor-safe.
- **IDE-native.** IntelliSense, go-to-definition, find-all-references work on attributes.
- **Compile-time.** The source generator validates the model before any code runs.
- **One language.** The domain model, the DSL decorations, the invariants, the generated code — all C#.

The developer never leaves their IDE. The compiler is the tool.

## Why Source Generation Instead of Runtime Codegen

The original Diem generated PHP files on disk via `dm:setup`. You ran a command, files appeared, you ran the app. This had issues:

- Generated files in the working directory (version control noise)
- Manual regeneration step (`dm:setup` after every schema change)
- No incremental processing (regenerate everything or nothing)

Roslyn incremental source generators solve all three:

- **Generated files are virtual.** They exist in the compilation but not on disk. No version control noise.
- **Automatic.** The generator runs on every build. No manual step.
- **Incremental.** Only changed models are reprocessed. Fast on large projects.

The developer writes an attribute, saves, and the generated code exists. No command to run. No files to manage.

## The Three Audiences

Diem serves three audiences with the same codebase:

**Developers** write domain models with DSL attributes, customize generated code via partial classes, and write tests linked to requirements. They work in their IDE. Their tool is the compiler.

**DevOps** builds Docker images, deploys with Podman Compose (PostgreSQL + Minio + Redis), and configures environments. They work with CI/CD pipelines. Their tool is `dotnet publish`.

**Content editors** create pages, compose layouts with widgets, manage content through auto-generated admin interfaces, and control publication through workflows. They work in the browser. Their tool is the admin UI.

None of these audiences needs to understand the others' tools. The developer doesn't configure Docker. The editor doesn't write C#. The DevOps engineer doesn't manage content. Diem sits between them, translating DSL declarations into running infrastructure.

## Why This Matters

Most enterprise content platforms are built by fighting a CMS. The team installs WordPress or Drupal or Strapi, then spends months making it do something it wasn't designed for. Custom post types, plugin conflicts, theme overrides, performance workarounds, migration nightmares.

Diem takes the opposite approach. The team defines their domain — their products, their orders, their blog posts, their workflows — using the language of their business. The framework generates the infrastructure. The team customizes by overriding virtual methods, not by hacking around plugin limitations.

The result: a content platform that is 100% specific to the business's needs. No unused features. No plugin tax. No generic data model struggling to fit a specific domain. Type-safe from the domain model to the database schema to the API to the admin UI.

**Define the domain once. Generate everything else.**

That was the promise in 2010 with 200 lines of YAML.
That is the promise in 2026 with 300 lines of C#.
The tools changed. The belief didn't.
