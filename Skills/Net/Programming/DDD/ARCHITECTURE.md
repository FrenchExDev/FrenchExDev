# DDD — Architecture

## Attribute Taxonomy

### Class-Level Attributes

| Attribute | Target | MetaConcept | Purpose |
|-----------|--------|-------------|---------|
| `[AggregateRoot("name")]` | Class | `AggregateRootConcept` | Transaction boundary, must have `[EntityId]` |
| `[Entity("name")]` | Class | `EntityConcept` | Identity by ID, can contain other entities/VOs |
| `[ValueObject("name")]` | Class | `ValueObjectConcept` | Immutable, identity by value |
| `[Command("name")]` | Class | `CommandConcept` | CQRS command, optional `AggregateRoot` property |
| `[DomainEvent("name")]` | Class | `DomainEventConcept` | Event with optional `SourceAggregate` |
| `[BoundedContext("name")]` | Class | `BoundedContextConcept` | Bounded context marker |

### Property-Level Attributes

| Attribute | Target | Purpose |
|-----------|--------|---------|
| `[EntityId]` | Property | Identifies the ID property (required on `[AggregateRoot]`) |
| `[Property("name")]` | Property | Value property with optional `MaxLength`, `Required` |
| `[ValueComponent("name")]` | Property | For value object fields |
| `[Composition]` | Property | Owns children, inside aggregate boundary |
| `[Association]` | Property | Cross-aggregate reference, eventual consistency |
| `[Aggregation]` | Property | Weak reference, shared lifecycle |

### Method-Level Attributes

| Attribute | Target | Purpose |
|-----------|--------|---------|
| `[Invariant("description")]` | Method | Domain invariant, must return `Result` |

## Metamodel Layers

```
M3 (Dsl primitives)
  MetaConcept, MetaConceptAttribute, MetaConstraint, ConstraintResult
    |
M2 (DDD concepts)
  AggregateRootConcept, EntityConcept, ValueObjectConcept, CommandConcept, ...
  AggregateRootAttribute, EntityAttribute, ValueObjectAttribute, ...
    |
M1 (User domain)
  [AggregateRoot("Order")] class Order, [Entity("OrderLine")] class OrderLine, ...
    |
M0 (Runtime instances)
  order.Total = 1500, line.Quantity = 3
```

## Concept Hierarchy

```
MetaConcept (Dsl base)
  |-- AggregateRootConcept (extends EntityConcept)
  |     CanContain: Entity, ValueObject
  |     Constraint: MustHaveIdConstraint
  |-- EntityConcept
  |     CanContain: Entity, ValueObject
  |-- ValueObjectConcept
  |     CanContain: ValueObject (no entities)
  |-- CommandConcept
  |-- DomainEventConcept
  +-- BoundedContextConcept
```

## Relationship Model

```
+-- Aggregate Boundary (transaction boundary) --------+
|                                                      |
|  [AggregateRoot] Order                               |
|    |-- [Composition] --> [Entity] OrderLine           |
|    |       |-- [Composition] --> [ValueObject] Money  |
|    |-- [Composition] --> [ValueObject] ShippingAddress |
|                                                      |
+------------------------------------------------------+
         |
    [Association] (cross-aggregate, no cascade)
         |
    [AggregateRoot] Customer (different aggregate)
```

- **Composition** = inside boundary, cascade delete, owned lifecycle
- **Association** = cross-aggregate, nullable FK, eventual consistency
- **Aggregation** = weak reference, nullable FK

## SG Pipeline

```
[AggregateRoot]/[Entity] + [Invariant] methods
  --> InvariantGenerator (IIncrementalGenerator)
  --> InvariantModel (plain data, Roslyn-free)
  --> InvariantEmitter.Emit(model) --> EnsureInvariants() method
```

Generated `EnsureInvariants()` calls all `[Invariant]` methods and aggregates results via `Result.Combine()`.

## 5-Project Layout

| Project | Purpose |
|---------|---------|
| `FrenchExDev.Net.Ddd` | Runtime: `ICommandHandler`, `IQueryHandler`, `IDomainEvent` |
| `FrenchExDev.Net.Ddd.Attributes` | 13 attributes + 13 MetaConcept companions |
| `FrenchExDev.Net.Ddd.SourceGenerator` | `InvariantGenerator` (Roslyn extraction) |
| `FrenchExDev.Net.Ddd.SourceGenerator.Lib` | `InvariantEmitter` (Roslyn-free emission) |
| `FrenchExDev.Net.Ddd.Tests` | xUnit tests |

## Key Files

- `Ddd/src/FrenchExDev.Net.Ddd/ICommandHandler.cs` — CQRS command handler interface
- `Ddd/src/FrenchExDev.Net.Ddd/IQueryHandler.cs` — CQRS query handler interface
- `Ddd/src/FrenchExDev.Net.Ddd/IDomainEvent.cs` — Domain event base
- `Ddd/src/FrenchExDev.Net.Ddd.Attributes/AggregateRootAttribute.cs` — with MustHaveIdConstraint
- `Ddd/src/FrenchExDev.Net.Ddd.Attributes/Concepts/` — MetaConcept companions
- `Ddd/src/FrenchExDev.Net.Ddd.SourceGenerator/InvariantGenerator.cs`
- `Result/src/FrenchExDev.Net.Result/Code.cs` — Result<T> used by invariants
