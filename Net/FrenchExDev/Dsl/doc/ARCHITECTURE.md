# Architecture — FrenchExDev.Net.Dsl

## Overview

FrenchExDev.Net.Dsl is an M3 meta-metamodel framework. It provides the foundational vocabulary from which all DSLs (M2) are built. The framework is self-describing: the 5 primitives that define DSLs are themselves defined using those same primitives.

```
              writes once          uses to build DSLs        developers use
Framework ──────────────> M3 (Dsl) ──────────────────> M2 (DDD, Content...) ────> M1 (Order, BlogPost...)
 author                                DSL author                                    app developer
```

## Four-Layer Architecture (OMG MOF)

```
Layer   What                  Who writes it              Example
-----   ----                  -------------              -------
M3      Meta-metamodel        Framework authors (once)    MetaConcept, MetaProperty
M2      DSL attributes        DSL authors                 [AggregateRoot], [ContentPart], [Workflow]
M1      Domain models         Application developers      class Order, class BlogPost
M0      Runtime instances     The running application     order.Total = 1500
```

Each layer defines the structure of the layer below it. M3 defines how M2 DSLs are structured. M2 defines how M1 models are structured. M1 creates M0 instances at runtime.

M3 is the fixed point. There is no M4. The 5 primitives are sufficient to describe themselves, any DSL, and any model.

## The 5 Primitives

```
                     ┌──────────────────┐
                     │   MetaConcept    │──── describes itself (fixed point)
                     └──────┬───────────┘
                            │ has
              ┌─────────────┼─────────────┬──────────────┐
              v             v             v              v
        MetaProperty  MetaReference  MetaConstraint  MetaInherits
        (typed slot)  (association)  (validation)    (inheritance)
```

### MetaConcept

Declares that an attribute class represents a modeling concept. Applied to attribute classes. Self-describing: `[MetaConcept(typeof(MetaConceptConcept))]` on itself.

Takes a `Type conceptType` parameter pointing to the behavioral companion class.

### MetaProperty

Declares a typed configuration slot on a concept. Applied to properties within attribute classes. Describes the metamodel schema — what configuration each concept accepts.

Parameters: `name`, `type`, optional `Required`, optional `DefaultValue`.

### MetaReference

Declares a directed association between concepts. Applied to properties that reference other concepts. Supports multiplicity (`"1"`, `"0..1"`, `"0..*"`, `"1..*"`), containment, and bidirectional references via `Opposite`.

### MetaConstraint

Declares a validation rule. Unlike OCL string expressions, constraints reference **real C# static methods** by name. The method receives a `ConceptValidationContext` and returns a `ConstraintResult`. This makes constraints debuggable, testable, and IDE-navigable.

### MetaInherits

Declares metamodel-level inheritance between concepts. Distinct from C# class inheritance. `[MetaInherits(typeof(EntityConcept))]` means "AggregateRoot IS-A Entity at the metamodel level."

## Companion Classes (Behavioral Metamodeling)

Every DSL concept has two parts:

1. **The attribute** — passive metadata (declarative, applied to user classes)
2. **The companion** — active behavior (validation, containment rules, lifecycle hooks)

```
┌──────────────────────────┐     ┌──────────────────────────────┐
│   AggregateRootAttribute │     │   AggregateRootConcept       │
│   (passive metadata)     │────>│   : MetaConcept              │
│                          │     │   (active behavior)          │
│   [MetaConcept(typeof(   │     │                              │
│     AggregateRootConcept │     │   Validate()                 │
│   ))]                    │     │   CanContain()               │
│   Name, BoundedContext   │     │   OnDiscovered()             │
│   MustHaveIdConstraint() │     │   OnBeforeValidation()       │
└──────────────────────────┘     └──────────────────────────────┘
```

The `MetaConcept` base class provides:

| Method | Purpose | Default |
|--------|---------|---------|
| `Validate(context)` | Validate an M1 instance | Returns `Satisfied()` |
| `CanContain(child)` | Check if a child concept is allowed | Returns `true` |
| `IsSuperTypeOf(other)` | Check metamodel inheritance | Walks `SuperTypes` |
| `OnDiscovered(context)` | Lifecycle: concept found by SG | No-op |
| `OnBeforeValidation(context)` | Lifecycle: before constraint check | No-op |
| `OnAfterValidation(context, result)` | Lifecycle: after constraint check | No-op |

## Validation Flow

```
          ┌──────────────┐
          │ M1 class     │   [AggregateRoot("Order")]
          │ with DSL     │   public partial class Order { ... }
          │ attributes   │
          └──────┬───────┘
                 │
                 v
    ┌─────────────────────────┐
    │ ConceptValidationContext│   Built from Roslyn symbols (compile-time)
    │                         │   or from reflection (design-time)
    │  ConceptName = "..."    │
    │  TypeName = "Order"     │
    │  Properties = [...]     │   Each property: Name, TypeName, AttributeNames
    │  Methods = [...]        │   Each method: Name, ReturnType, AttributeNames
    │  ClassAttributes = [... │
    └────────┬────────────────┘
             │
             v
    ┌────────────────────────┐
    │ Constraint methods     │   Static methods on the attribute class
    │                        │   Referenced by [MetaConstraint(name, nameof(Method))]
    │ MustHaveIdConstraint() │
    │ MustHaveFieldConstraint│   Receives ConceptValidationContext
    │ ...                    │   Returns ConstraintResult
    └────────┬───────────────┘
             │
             v
    ┌────────────────────────┐
    │ ConstraintResult       │   Satisfied() or Failed("reason")
    │                        │   Aggregate() combines multiple results
    └────────────────────────┘
```

At compile time, the source generator builds the context from Roslyn symbols. At design time, `MetaConstraintRunner` builds it from reflection and invokes constraint methods directly.

## Source Generator Pipeline

```
     Stage 0                    Stage 1                 Stage 2-3              Stage 4
  ┌──────────────┐        ┌──────────────┐        ┌──────────────┐       ┌──────────────┐
  │ Metamodel    │        │ Validation   │        │ Code         │       │ Traceability │
  │ Registration │──────> │ & Collection │──────> │ Generation   │─────> │ & Diagnostics│
  │              │        │              │        │              │       │              │
  │ Dsl.SG runs  │        │ Each DSL SG  │        │ Each DSL SG  │       │ Req SG runs  │
  │ discovers all│        │ validates M1 │        │ emits code   │       │ emits matrix │
  │ [MetaConcept]│        │ models       │        │ from models  │       │              │
  └──────────────┘        └──────────────┘        └──────────────┘       └──────────────┘

  Output:                  Output:                 Output:                Output:
  MetamodelRegistry.g.cs   Roslyn diagnostics      Entity.g.cs            TraceabilityMatrix.g.cs
                           (errors/warnings)       Builder.g.cs           RequirementRegistry.g.cs
                                                   EfConfig.g.cs
                                                   AdminList.g.cs
                                                   ...
```

The Dsl framework owns Stage 0. Each DSL owns its own Stages 1-3. The Requirements DSL owns Stage 4.

## ConceptDescriptor (Runtime Registry)

The generated `MetamodelRegistry` contains `ConceptDescriptor` instances — the runtime representation of the metamodel:

```
MetamodelRegistry.Concepts["AggregateRoot"]
  ├── Name: "AggregateRoot"
  ├── AttributeType: typeof(AggregateRootAttribute)
  ├── ConceptType: typeof(AggregateRootConcept)
  ├── Inherits: ["Entity"]
  ├── Properties:
  │     ├── { Name: "Name", Type: "string", Required: true }
  │     └── { Name: "BoundedContext", Type: "string", Required: false }
  └── Constraints:
        └── { Name: "MustHaveId", MethodName: "MustHaveIdConstraint", Message: "..." }
```

This registry is generated per-compilation. A project referencing DDD attributes + Content attributes + Workflow attributes gets a registry containing ALL concepts from all referenced DSLs.

## Project Dependencies

```
FrenchExDev.Net.Dsl                    (no dependencies — root)
  │
  ├── FrenchExDev.Net.Dsl.Design       (references Dsl — reflection-based tools)
  │
  ├── FrenchExDev.Net.Dsl.SourceGenerator      (references Dsl + SG.Lib)
  │     └── FrenchExDev.Net.Dsl.SourceGenerator.Lib  (no Roslyn dep — reusable emitter)
  │
  └── FrenchExDev.Net.Dsl.Tests        (references Dsl + SG as Analyzer)
```

The core `Dsl` project has zero external dependencies. It targets `netstandard2.0;net10.0` for maximum compatibility. The SG targets `netstandard2.0` (Roslyn requirement). The SG.Lib has no Roslyn dependency and can be consumed by any source generator.

## Cross-Assembly Discovery

The MetamodelRegistry source generator uses `ForAttributeWithMetadataName`, which searches the current compilation AND all referenced assemblies. This means:

- A project referencing `Ddd.Attributes` sees DDD concepts
- A project referencing `Ddd.Attributes` + `Content.Parts.Attributes` sees both
- The registry is always complete for the current compilation's visible concepts

No manual registration required. Add a `ProjectReference` and concepts appear automatically.

## Design Decisions

### Why companion classes instead of just attributes?

Attributes are passive. They store data but can't act. Companion classes carry validation logic, containment rules, and lifecycle hooks. This is the difference between describing a concept (attribute) and being a concept (companion). Ecore's `EClass` is a live object with methods — our `MetaConcept` provides the same capability.

### Why `nameof()` for constraints instead of lambdas?

Lambdas can't be used in attribute arguments (C# language limitation). String literals would work but break on rename. `nameof(MethodName)` is compiler-checked, refactor-safe, and IDE-navigable: Ctrl+Click jumps to the constraint method.

### Why `netstandard2.0` for the core library?

Attribute projects consumed by source generators must target `netstandard2.0`. By making the core Dsl library `netstandard2.0`-compatible, all DSL attribute projects can reference it without multi-targeting issues.

### Why separate Attribute + Companion instead of one class?

C# attributes have limitations: no interfaces, no virtual methods, no complex construction. The companion class has none of these limitations. Separating them gives full OOP power to the behavioral side while keeping the declarative side clean.

### Why string-based Multiplicity instead of structured type?

C# attributes can only use primitive types, `Type`, `string`, and arrays of these as parameters. A `Multiplicity` record can't be used as an attribute argument. The string `"0..*"` is parsed by the source generator.

## Comparison with Ecore

| Aspect | Ecore (Java/Eclipse) | FrenchExDev.Net.Dsl (C#) |
|--------|---------------------|--------------------------|
| Expression | XMI/XML models | C# attributes + companion classes |
| Validation | OCL string constraints | Real C# static methods |
| Code generation | Template-based (Acceleo, Xtext) | Roslyn incremental source generators |
| Registry | EPackage.getEClassifiers() | MetamodelRegistry.Concepts (generated) |
| Inheritance | eSuperTypes (runtime list) | [MetaInherits] + SuperTypes property |
| Containment | EReference.containment | MetaReference.IsContainment + CanContain() |
| IDE integration | Eclipse plugin | Standard C# IDE (VS, Rider, VS Code) |
| Self-describing | EClass is an EClass | MetaConceptAttribute is [MetaConcept] |
