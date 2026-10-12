# ENTITY-DSL — Requirements

Acceptance criteria for any model package using the Entity DSL pattern.

## Project Layout

The persistence-aware part of a domain must split into:

- [ ] **Domain assembly** — POCOs + DSL attributes only. References `Entity.Dsl.Attributes` (and DDD attributes if used).
- [ ] **Persistence assembly** — `[DbContext]` partial, EF Core dependency, generated config gets emitted here.
- [ ] **Abstractions assembly** — interfaces (`IRepository<T>`, `IUnitOfWork`, behavior markers like `ITimestampable`).

The domain assembly **must not** reference `Microsoft.EntityFrameworkCore`. If it does, the pattern's main benefit is gone.

## Entity Declaration Quality Bar

Every entity class must:

- [ ] Be `partial` (generated behavior partials require it)
- [ ] Carry exactly one of `[Entity]`, `[AggregateRoot]`
- [ ] Carry `[PrimaryKey]` (class-level) or have a property marked `[EntityId]` + `[PrimaryKey]`
- [ ] Have nullable annotations enabled
- [ ] Use `[Property("Name", Required = ..., MaxLength = ...)]` for every persisted scalar
- [ ] Use `[Composition]` / `[Aggregation]` / `[Association]` for every relationship

## DbContext Quality Bar

Every `DbContext` must:

- [ ] Be `partial`
- [ ] Carry `[DbContext]`
- [ ] Have a constructor accepting `DbContextOptions<T>`
- [ ] Not declare `DbSet<T>` properties manually for entities the generator covers
- [ ] Not override `OnModelCreating` (let the generator do it; extend the partial stub if you need to)

## Lifecycle Convention Compliance

The package enforces a relationship-to-delete-behavior mapping:

| Attribute | Generated `OnDelete` |
|---|---|
| `[Composition]` | `Cascade` |
| `[Aggregation]` | `Restrict` |
| `[Association]` | `NoAction` |

Override only when the domain truly demands it. Do not "default everything to NoAction" out of caution — that defeats the convention.

## Behavior Attribute Quality Bar

For shipped behaviors (`[Timestampable]`, `[SoftDeletable]`, etc.):

- [ ] The attribute is applied at the class level
- [ ] The matching marker interface (`ITimestampable`, `ISoftDeletable`) is implemented automatically by the generator
- [ ] Behavior properties live in the `*.Behaviors.g.cs` partial — never hand-written
- [ ] The DbContext SaveChanges hook is generated, not hand-written

For new custom behaviors:

- [ ] The attribute lives in your own attributes assembly (not in `Entity.Dsl.Attributes`)
- [ ] The contributor extends the existing emitter, never forks it

## Generated File Discipline

- [ ] Never edit a `*.g.cs` file
- [ ] Always extend behavior via the developer-owned partial stub (`{Entity}Configuration`, `{Entity}Repository`)
- [ ] Always call `base.Configure(builder)` in overrides

## Repository / UoW Quality Bar

- [ ] Repositories are consumed via the generated interface (`IOrderRepository`), not the concrete class
- [ ] Custom queries live in the partial stub (`OrderRepository.cs`), not in the base
- [ ] The Injectable SG is wired up so generated repositories register automatically
- [ ] The UoW exposes only the operations the application actually needs (no leaky `DbContext` accessor unless required)

## Bridge Generator Compliance (DDD users)

If your domain uses DDD attributes, the `Ddd.Entity.Dsl` bridge SG is in scope. Verify:

- [ ] The bridge SG package is referenced from the domain project
- [ ] Generated `*.DddBridge.g.cs` files appear in the IDE's analyzer output
- [ ] No hand-written `[MappedEntity]` / `[NavigationProperty]` declarations on classes the bridge already covers
- [ ] Two-stage generation completes in one compile (you don't need a separate build step)

## Testing Quality Bar

- [ ] Entity round-trip tests use a real provider (sqlite or in-memory) — not mocks
- [ ] Behavior tests verify the SaveChanges hook actually fires (e.g. `CreatedAt` is non-default after add)
- [ ] Repository custom-method tests verify the query, not the generator
- [ ] Emitter tests live in the `Lib` test project and assert the emitted string

## Things You Must Never Do

- Reference `Microsoft.EntityFrameworkCore` from the domain assembly
- Hand-write `IEntityTypeConfiguration<T>` for an entity the SG already covers
- Edit a `*.g.cs` file
- Hand-write a `DbSet<T>` property the generator already declares
- Hand-write a `SaveChanges` override that duplicates a behavior hook
- Use `[Composition]` and then "override OnDelete to NoAction everywhere just to be safe"
- Make an entity class non-`partial`
- Add property attributes from a partial class (C# does not allow it; use class-level `[NavigationProperty(...)]`)
- Forge a bridge SG that bypasses the Entity DSL — extend it instead
