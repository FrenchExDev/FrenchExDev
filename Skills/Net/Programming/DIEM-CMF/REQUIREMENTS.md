# DIEM-CMF — Requirements

Hard requirements that any declaration-first CMF in this style must meet. Each is testable. None is aspirational.

## R1 — A New Domain Aggregate Requires Only One File

Adding a new aggregate root with full CRUD, admin, persistence, and routing must require **a single developer-authored file** with attribute declarations and partial properties. No manual registration in startup, no hand-written repositories, no hand-written controllers.

**Test**: scaffold a new aggregate, build, and observe a working list/create/edit/detail page in the admin without touching any other file.

## R2 — Sub-DSLs Compose on a Single Type

A single class must be able to carry attributes from at least four sub-DSLs (DDD, Content, Admin, Workflow) simultaneously, and all four source generators must read it independently and emit non-conflicting code.

**Test**: build the canonical `Article` example with `[AggregateRoot]`, `[HasPart(...)]`, `[AdminModule]`, and `[Workflow]` together. All generated files compile without conflict.

## R3 — Generated Code Is Never Edited

`*.g.cs` files must be regenerated on every build and overwritten without warning. Developer customization happens exclusively via Layer 4 partial classes overriding Layer 2 virtual hooks.

**Test**: delete all `*.g.cs` files, rebuild, run the test suite. All tests pass.

## R4 — The Customization Contract Has Four Layers

Every generated component must follow:

1. Layer 1: framework `ComponentBase`
2. Layer 2: sub-DSL `<Sub>Base<T>` with `virtual` hooks
3. Layer 3: `Foo.g.cs` (regenerated, calls Layer 2 hooks)
4. Layer 4: `Foo.cs` (developer's partial, never overwritten)

**Test**: every Layer 3 file inherits from a Layer 2 base, calls at least one virtual hook, and has a sibling Layer 4 partial declared.

## R5 — Parts Are Horizontal, Blocks Are Vertical

Parts attach via `[HasPart(typeof(...))]` and add fields/behavior to existing entities. Blocks attach via `[StreamField(typeof(...), ...)]` and become typed entries inside ordered polymorphic sequences.

**Test**: an entity gains SEO via `[HasPart(typeof(SeoablePart))]` without modifying its declaration body. A page body composes 3+ block types from a single property.

## R6 — Every Built-In Is Its Own Project

Every built-in part, block, widget, and page template is a separate package with its own tests and (where applicable) a `.Testing` helper project. Built-ins use the same attributes developers use — no internal API.

**Test**: a developer can copy a built-in part's source verbatim, rename it, and have a working custom part. There is no `internal` framework call inside built-in code.

## R7 — The Metamodel Is Self-Describing

Every DSL attribute is itself decorated with `[MetaConcept(typeof(...))]`. The metamodel registry is built at compile time without manual registration.

**Test**: adding a new attribute class requires zero changes to a "registry" file; the SG discovers it automatically.

## R8 — Workflows Have Stages, Transitions, Guards, and Scheduling

The workflow sub-DSL must support:
- Multiple named stages, exactly one initial, one or more final
- Named transitions between stages
- Guards (`[RequiresRole]`, `[RequiresApproval(Count = N)]`)
- Scheduled transitions via `[ScheduledTransition(After = ...)]`
- Per-locale progress tracking via `[ForEachLocale]`

**Test**: a workflow with all five features compiles, transitions correctly, and the guards block unauthorized transitions with a typed result.

## R9 — Three Audiences, Three Toolchains

The CMF serves developers (IDE), editors (browser admin UI), and DevOps (`dotnet publish` + container orchestration). None requires the others' tools.

**Test**: an editor can create a page and place widgets using the admin UI alone. A developer can add a new aggregate using only the IDE. A DevOps engineer can deploy with only `dotnet publish` and a compose file.

## R10 — Type Safety End to End

From the DSL declaration to the database column to the API DTO to the admin form field, types must be checked at compile time. No magic strings for property names — use `nameof()`. No string-typed property bags.

**Test**: rename a property in the source. Every reference (admin field, list filter, workflow guard) updates via the IDE refactor; the build fails on any miss.

## R11 — Source Generation Is Compile-Time and Incremental

Generators must use the incremental SG API. They must not write to disk. They must not require a manual `cmf generate` step in the inner dev loop.

**Test**: change a single attribute value, rebuild, and only the affected sub-DSL's outputs are recomputed.

## R12 — Requirements Are Traceable

Every requirement is declared with `[Requirement(id, description)]`. Implementing types and covering tests are linked via `[TracedBy(id)]`. The CLI emits a matrix; uncovered requirements raise a build warning.

**Test**: declare a requirement without a `[TracedBy]` test; observe a build warning. Add the test; the warning disappears.

## R13 — No Mocking Frameworks in Tests

Hand-written fakes only. Each sub-DSL ships a `<Sub>.Testing` project with reusable harnesses and fakes. No Moq, no NSubstitute.

**Test**: every test project compiles with zero mocking-framework references.

## R14 — Each Sub-DSL Owns Its Tests

Every sub-DSL has at least one xUnit test project. Tests must run as part of `dotnet test` against the solution. No integration test depends on a real database — use in-memory or testcontainers behind the abstraction.

**Test**: `dotnet test` against the full solution finishes without external dependencies.

## R15 — Pages Are Records, Widgets Are Code

Page instances live in the database. Widget definitions live in C# with `[PageWidget]` and `[WidgetConfig]`. Editors compose pages from the admin UI; developers register widgets via attributes.

**Test**: an editor creates a new page, drags a widget into a zone, and saves. The widget renders. The developer never edited a page file.
