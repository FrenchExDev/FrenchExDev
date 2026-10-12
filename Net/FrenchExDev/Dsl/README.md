# FrenchExDev.Net.Dsl

A framework for building attribute-based DSLs in C# with Roslyn source generation.

Defines 5 M3 primitives that any DSL is built from. Equivalent to OMG Essential MOF (EMOF) / Eclipse EMF Ecore, but native C# with compile-time validation and code generation.

## The Idea

In metamodeling, there are 4 layers:

```
M3  [MetaConcept("AggregateRoot")]         defines what concepts exist
 |
M2  public sealed class AggregateRootAttribute   the DSL attribute
 |
M1  [AggregateRoot("Order")] class Order         developer's model
 |
M0  order.Total = 1500                           runtime data
```

This library is M3. It provides the vocabulary to define M2 DSLs. You write DSL attributes once (M2), developers use them on their classes (M1), and source generators produce code that runs at M0.

## The 5 Primitives

| Primitive | What it declares | Ecore equivalent |
|-----------|-----------------|-----------------|
| `[MetaConcept]` | A modeling concept (applied to attribute classes) | `EClass` |
| `[MetaProperty]` | A typed configuration slot on a concept | `EAttribute` |
| `[MetaReference]` | A directed association between concepts | `EReference` |
| `[MetaConstraint]` | A validation rule (references a real C# method) | OCL constraint |
| `[MetaInherits]` | Metamodel-level inheritance between concepts | `eSuperTypes` |

All 5 are self-describing: `MetaConceptAttribute` is itself `[MetaConcept(typeof(MetaConceptConcept))]`. This is the M3 fixed point.

## Quick Start: Create Your Own DSL

### 1. Define an attribute with `[MetaConcept]`

```csharp
using FrenchExDev.Net.Dsl;

// Your concept companion (behavioral)
public sealed class WidgetConcept : MetaConcept
{
    public override string Name => "Widget";
    public override Type AttributeType => typeof(WidgetAttribute);
}

// Your DSL attribute (declarative)
[MetaConcept(typeof(WidgetConcept))]
[AttributeUsage(AttributeTargets.Class)]
public sealed class WidgetAttribute : Attribute
{
    [MetaProperty("Name", "string", Required = true)]
    public string Name { get; set; }

    [MetaProperty("Icon", "string")]
    public string? Icon { get; set; }

    public WidgetAttribute(string name) { Name = name; }
}
```

### 2. Developers use your DSL

```csharp
[Widget("ProductList", Icon = "grid")]
public class ProductListWidget { /* ... */ }
```

### 3. The MetamodelRegistry SG auto-discovers it

The source generator scans all `[MetaConcept]`-decorated classes in the compilation and emits:

```csharp
// Generated: MetamodelRegistry.g.cs
public static class MetamodelRegistry
{
    public static IReadOnlyDictionary<string, ConceptDescriptor> Concepts { get; } =
        new Dictionary<string, ConceptDescriptor>
        {
            ["Widget"] = new ConceptDescriptor
            {
                Name = "Widget",
                AttributeType = typeof(WidgetAttribute),
                ConceptType = typeof(WidgetConcept),
                Properties = new[] {
                    new PropertyDescriptor { Name = "Name", Type = "string", Required = true },
                    new PropertyDescriptor { Name = "Icon", Type = "string", Required = false },
                },
            },
        };
}
```

## Features in Depth

### Companion Classes (Behavioral Metamodeling)

Every DSL concept has a companion class that extends `MetaConcept`. Attributes are passive metadata; companions carry behavior:

```csharp
public sealed class AggregateRootConcept : MetaConcept
{
    public override string Name => "AggregateRoot";
    public override Type AttributeType => typeof(AggregateRootAttribute);

    // Metamodel inheritance (AggregateRoot IS-A Entity)
    public override IReadOnlyList<Type> SuperTypes => new[] { typeof(EntityConcept) };

    // Containment rules: what can live inside an aggregate
    public override bool CanContain(MetaConcept child)
        => child is EntityConcept || child is ValueObjectConcept;

    // Behavioral validation
    public override ConstraintResult Validate(ConceptValidationContext context)
    {
        // Custom logic beyond declarative constraints
        return ConstraintResult.Satisfied();
    }

    // Lifecycle hooks
    public override void OnDiscovered(ConceptValidationContext context) { }
    public override void OnBeforeValidation(ConceptValidationContext context) { }
    public override void OnAfterValidation(ConceptValidationContext context, ConstraintResult result) { }
}
```

### Constraint Methods (Type-Safe Validation)

Constraints reference real C# static methods, not string expressions. They're debuggable, testable, and IDE-navigable:

```csharp
[MetaConcept(typeof(AggregateRootConcept))]
[MetaConstraint("MustHaveId", nameof(MustHaveIdConstraint),
    Message = "Aggregate root must have an [EntityId] property")]
public sealed class AggregateRootAttribute : Attribute
{
    // ... properties ...

    public static ConstraintResult MustHaveIdConstraint(ConceptValidationContext ctx)
    {
        foreach (var p in ctx.Properties)
        {
            foreach (var a in p.AttributeNames)
            {
                if (a == "EntityId") return ConstraintResult.Satisfied();
            }
        }
        return ConstraintResult.Failed("Aggregate root must have an [EntityId] property");
    }
}
```

The `ConceptValidationContext` provides the structure of the class being validated:

```csharp
public sealed class ConceptValidationContext
{
    public string ConceptName { get; set; }      // e.g. "AggregateRoot"
    public string TypeName { get; set; }          // e.g. "Order"
    public IReadOnlyList<ConceptPropertyInfo> Properties { get; set; }
    public IReadOnlyList<ConceptMethodInfo> Methods { get; set; }
    public IReadOnlyList<ConceptReferenceInfo> References { get; set; }
    public IReadOnlyList<string> SuperTypes { get; set; }
    public IReadOnlyList<ConceptAttributeInfo> ClassAttributes { get; set; }
}
```

At design time, `MetaConstraintRunner` invokes constraints via reflection:

```csharp
using FrenchExDev.Net.Dsl.Design;

var result = MetaConstraintRunner.RunConstraints(typeof(AggregateRootAttribute), context);
if (!result.IsSatisfied)
    Console.WriteLine(result.Message);
```

### Metamodel Inheritance

`[MetaInherits]` declares inheritance at the metamodel level (distinct from C# inheritance):

```csharp
[MetaConcept(typeof(AggregateRootConcept))]
[MetaInherits(typeof(EntityConcept))]              // AggregateRoot IS-A Entity
public sealed class AggregateRootAttribute : Attribute { /* ... */ }
```

The companion mirrors this:

```csharp
public sealed class AggregateRootConcept : MetaConcept
{
    public override IReadOnlyList<Type> SuperTypes => new[] { typeof(EntityConcept) };
}
```

### MetaProperty and MetaReference

Describe the structure of your concept at the metamodel level:

```csharp
[MetaConcept(typeof(StageConcept))]
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class StageAttribute : Attribute
{
    [MetaProperty("Name", "string", Required = true)]
    public string Name { get; set; }

    [MetaProperty("IsInitial", "bool")]
    public bool IsInitial { get; set; }

    [MetaProperty("IsFinal", "bool")]
    public bool IsFinal { get; set; }

    [MetaProperty("Color", "string")]
    public string? Color { get; set; }

    public StageAttribute(string name) { Name = name; }
}
```

References declare directed associations between concepts:

```csharp
[MetaReference("Target", "Entity", IsContainment = true, Multiplicity = "1")]
public Type TargetType { get; set; }
```

### ConstraintResult

Immutable value type for validation results:

```csharp
ConstraintResult.Satisfied()                // success
ConstraintResult.Failed("reason")           // failure with message
ConstraintResult.Aggregate(results)         // combines multiple (fails if any fails)

result.IsSatisfied  // bool
result.Message      // string? (null if satisfied)
```

## Real-World DSLs Built on This Framework

### DDD DSL (`FrenchExDev.Net.Ddd`)

```csharp
[AggregateRoot("Order", BoundedContext = "Ordering")]
public partial class Order
{
    [EntityId] public partial OrderId Id { get; }
    [Property("Status", Required = true)] public partial OrderStatus Status { get; }
    [Composition] public partial IReadOnlyList<OrderLine> Lines { get; }

    [Invariant("Order must have at least one line")]
    private Result HasLines() => Lines.Count > 0
        ? Result.Success() : Result.Failure("Need at least one line");
}
// Source generator emits: entity impl, builder, EnsureInvariants(), EF Core config, repository
```

### Requirements DSL (`FrenchExDev.Net.Requirements`)

```csharp
public abstract class OrderFeature : Feature<DomainEpic>
{
    public override string Title => "Order fulfillment";
    public override RequirementPriority Priority => RequirementPriority.Critical;
    public override string Owner => "ordering-team";

    public abstract AcceptanceCriterionResult OrderCanBePlaced(UserId customer, ProductId product);
}

// Traceability:
[ForRequirement(typeof(OrderFeature), nameof(OrderFeature.OrderCanBePlaced))]
public Result PlaceOrder(User customer, Product product) { /* ... */ }

[Verifies(typeof(OrderFeature), nameof(OrderFeature.OrderCanBePlaced))]
public void Customer_can_place_order() { /* test */ }
```

### Content DSL (`FrenchExDev.Net.Diem.Content`)

```csharp
[ContentPart("Seoable", Description = "SEO metadata")]
public partial class SeoablePart
{
    [PartField("MetaTitle", DisplayName = "Meta Title", MaxLength = 70)]
    public string? MetaTitle { get; set; }

    [PartField("MetaDescription", MaxLength = 160)]
    public string? MetaDescription { get; set; }
}

// Attach to any entity:
[AggregateRoot("BlogPost")]
[HasPart(typeof(SeoablePart))]
[HasPart(typeof(RoutablePart))]
public partial class BlogPost { /* ... */ }
```

### Workflow DSL (`FrenchExDev.Net.Diem.Workflow`)

```csharp
[Workflow("Editorial")]
[Stage("Draft", IsInitial = true)]
[Stage("Review")]
[Stage("Published", IsFinal = true)]
[Transition("Submit", From = "Draft", To = "Review")]
[Transition("Approve", From = "Review", To = "Published")]
[RequiresRole("Submit", Role = "Author")]
[RequiresRole("Approve", Role = "Editor")]
public partial class EditorialWorkflow { }
// Source generator emits: stage enum, transition validator, gate evaluator, workflow engine
```

## Ecore Compatibility

Our 5 primitives map 1:1 to Eclipse EMF Ecore (Java's EMOF implementation):

| Ecore | FrenchExDev.Net.Dsl | C# subsumes |
|-------|--------------------|-|
| `EClass` | `[MetaConcept]` | |
| `EAttribute` | `[MetaProperty]` | |
| `EReference` | `[MetaReference]` | |
| OCL constraints | `[MetaConstraint]` | |
| `eSuperTypes` | `[MetaInherits]` | |
| `EPackage` | | C# namespaces |
| `EEnum` | | C# `enum` |
| `EOperation` | | C# methods |
| `EDataType` | | C# types |
| `EFactory` | | `new` / DI |

Ecore models can be described using this framework (see the Ecore compatibility section in [Diem/doc/PLAN.md](../Diem/doc/PLAN.md)).

## Project Structure

```
Dsl/
  FrenchExDev.Net.Dsl.slnx
  src/
    FrenchExDev.Net.Dsl/                      5 M3 attributes + MetaConcept base
      MetaConceptAttribute.cs                    + companion classes + validation types
      MetaPropertyAttribute.cs                   + descriptors + DslStage enum
      MetaReferenceAttribute.cs
      MetaConstraintAttribute.cs
      MetaInheritsAttribute.cs
      MetaConcept.cs                             abstract base for companions
      ConstraintResult.cs                        validation result type
      ConceptValidationContext.cs                context for constraint methods
      ConceptDescriptor.cs                       runtime descriptor types
      DslStage.cs                                5-stage pipeline enum
      Concepts/
        MetaConceptConcept.cs                    M3 fixed point (self-describing)
        MetaPropertyConcept.cs
        MetaReferenceConcept.cs
        MetaConstraintConcept.cs
        MetaInheritsConcept.cs
    FrenchExDev.Net.Dsl.Design/               Design-time helpers
      MetaConstraintRunner.cs                    reflection-based constraint invocation
    FrenchExDev.Net.Dsl.SourceGenerator/      Roslyn incremental SG
      MetamodelRegistryGenerator.cs              discovers [MetaConcept], emits registry
    FrenchExDev.Net.Dsl.SourceGenerator.Lib/  Emission logic (no Roslyn dependency)
      MetamodelRegistryEmitter.cs                string-based code emitter
  test/
    FrenchExDev.Net.Dsl.Tests/                9 tests
      MetaConceptAttributeTests.cs               self-description, companions, ConstraintResult
```

## Target Frameworks

| Project | TFM | Why |
|---------|-----|-----|
| `FrenchExDev.Net.Dsl` | `netstandard2.0;net10.0` | Used by attribute projects (netstandard2.0) and runtime projects (net10.0) |
| `FrenchExDev.Net.Dsl.Design` | `net10.0` | Design-time only |
| `FrenchExDev.Net.Dsl.SourceGenerator` | `netstandard2.0` | Roslyn SG must target netstandard2.0 |
| `FrenchExDev.Net.Dsl.SourceGenerator.Lib` | `netstandard2.0` | No Roslyn dependency, reusable by any SG |

## Dependencies

None. This is the root of the dependency graph.

## License

MIT
