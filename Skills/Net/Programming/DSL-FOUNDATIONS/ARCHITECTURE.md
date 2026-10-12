# DSL-FOUNDATIONS — Architecture

The metamodel layer is implemented as a tiny attributes/types package, a Roslyn source generator, and a design-time helper. This file describes the moving parts.

## The 4 Layers

```
M3   [MetaConcept]                    the meta-vocabulary (this layer)
 ↓
M2   public class AggregateRootAttribute    a DSL attribute (one of many)
 ↓
M1   [AggregateRoot] class Order            a developer model
 ↓
M0   order.Total = 1500                     runtime data
```

The framework provides M3. DSL authors write M2 attribute classes. Application developers write M1 model code. The runtime is M0.

## Package Layout

```
Dsl/
├── FrenchExDev.Net.Dsl                       M3 attributes + companion base + descriptors (netstandard2.0;net10.0)
│   ├── MetaConceptAttribute.cs
│   ├── MetaPropertyAttribute.cs
│   ├── MetaReferenceAttribute.cs
│   ├── MetaConstraintAttribute.cs
│   ├── MetaInheritsAttribute.cs
│   ├── MetaConcept.cs                        abstract base for companion classes
│   ├── ConstraintResult.cs                   value type for validation result
│   ├── ConceptValidationContext.cs           context passed to constraint methods
│   ├── ConceptDescriptor.cs                  runtime descriptor types
│   └── Concepts/                             self-describing M3 fixed point
│       ├── MetaConceptConcept.cs
│       ├── MetaPropertyConcept.cs
│       ├── MetaReferenceConcept.cs
│       ├── MetaConstraintConcept.cs
│       └── MetaInheritsConcept.cs
├── FrenchExDev.Net.Dsl.Design                design-time reflection helpers (net10.0)
│   └── MetaConstraintRunner.cs               invokes constraint methods via reflection
├── FrenchExDev.Net.Dsl.SourceGenerator       Roslyn IIncrementalGenerator (netstandard2.0)
│   └── MetamodelRegistryGenerator.cs         scans [MetaConcept], emits a registry
└── FrenchExDev.Net.Dsl.SourceGenerator.Lib   string-based emitter, no Roslyn dep (netstandard2.0)
    └── MetamodelRegistryEmitter.cs
```

The framework has **zero runtime dependencies**. Downstream DSL packages depend on it; it depends on nothing.

## The Five Primitives in Detail

### `[MetaConcept]`

Applied to a C# attribute class. Says "this attribute represents a domain concept."

```csharp
[MetaConcept(typeof(WidgetConcept))]
[AttributeUsage(AttributeTargets.Class)]
public sealed class WidgetAttribute : Attribute { ... }
```

The argument is the **companion class type**. The naming convention is `{Name}Attribute` ↔ `{Name}Concept`.

### `[MetaProperty]`

Applied to a property on an attribute class. Says "this property is a typed configuration slot."

```csharp
public sealed class WidgetAttribute : Attribute
{
    [MetaProperty("Name", "string", Required = true)]
    public string Name { get; set; }

    [MetaProperty("Icon", "string")]
    public string? Icon { get; set; }
}
```

The two strings are `(name, type)`. Optional fields: `Required`, `DefaultValue`, `Description`.

### `[MetaReference]`

Applied to a property whose value points to another concept. Says "this is a directed association."

```csharp
[MetaReference("Target", "Entity", IsContainment = true, Multiplicity = "1")]
public Type TargetType { get; set; }
```

`IsContainment` distinguishes ownership ("this concept owns the target") from plain reference. `Multiplicity` carries cardinality.

### `[MetaConstraint]`

Applied to a class. References a static method on the same class that implements the rule.

```csharp
[MetaConstraint("MustHaveId", nameof(MustHaveIdConstraint),
    Message = "Aggregate root must have an [EntityId] property")]
public sealed class AggregateRootAttribute : Attribute
{
    public static ConstraintResult MustHaveIdConstraint(ConceptValidationContext ctx) { ... }
}
```

The constraint method must be `public static`, accept `ConceptValidationContext`, and return `ConstraintResult`. `MetaConstraintRunner.RunConstraints(type, context)` invokes them via reflection.

### `[MetaInherits]`

Applied to a class. Declares metamodel-level inheritance, distinct from C# inheritance.

```csharp
[MetaConcept(typeof(AggregateRootConcept))]
[MetaInherits(typeof(EntityConcept))]
public sealed class AggregateRootAttribute : Attribute { ... }
```

`AggregateRoot IS-A Entity` at the metamodel level. The companion mirrors this with `SuperTypes`.

## The Companion Pattern

Every concept attribute has a companion `MetaConcept` subclass:

```csharp
public abstract class MetaConcept
{
    public abstract string Name { get; }
    public abstract Type AttributeType { get; }
    public virtual IReadOnlyList<Type> SuperTypes => Array.Empty<Type>();

    public virtual bool CanContain(MetaConcept child) => true;
    public virtual ConstraintResult Validate(ConceptValidationContext ctx) => ConstraintResult.Satisfied();

    public virtual void OnDiscovered(ConceptValidationContext ctx) { }
    public virtual void OnBeforeValidation(ConceptValidationContext ctx) { }
    public virtual void OnAfterValidation(ConceptValidationContext ctx, ConstraintResult result) { }
}
```

Override the hooks the DSL needs. The defaults do nothing — concept companions only override what they care about.

## `ConceptValidationContext`

The context object passed to constraint methods and lifecycle hooks. It carries everything a constraint needs to know about the class being validated:

```csharp
public sealed class ConceptValidationContext
{
    public string ConceptName { get; set; }                      // e.g. "AggregateRoot"
    public string TypeName { get; set; }                         // e.g. "Order"
    public IReadOnlyList<ConceptPropertyInfo> Properties { get; set; }
    public IReadOnlyList<ConceptMethodInfo> Methods { get; set; }
    public IReadOnlyList<ConceptReferenceInfo> References { get; set; }
    public IReadOnlyList<string> SuperTypes { get; set; }
    public IReadOnlyList<ConceptAttributeInfo> ClassAttributes { get; set; }
}
```

This is a flat data structure — no Roslyn types, no reflection objects. The same context shape is built at design time (via reflection) and at compile time (via Roslyn). Constraint methods cannot tell which.

## `ConstraintResult`

Immutable value type returned from every constraint. Three constructors plus an aggregator:

```csharp
ConstraintResult.Satisfied()                // success
ConstraintResult.Failed("reason")           // failure with message
ConstraintResult.Aggregate(results)         // combines many; fails if any fails

result.IsSatisfied : bool
result.Message     : string?                // null on success
```

Errors are values. Constraints never throw.

## The MetamodelRegistry Source Generator

`MetamodelRegistryGenerator` is an `IIncrementalGenerator`. Pipeline:

1. **Syntax filter** — find class declarations with attribute lists.
2. **Symbol filter** — keep only those decorated with `[MetaConcept]`.
3. **Descriptor extraction** — for each concept, extract name, attribute type, companion type, and the list of `[MetaProperty]`/`[MetaReference]` declarations.
4. **Registry emission** — call `MetamodelRegistryEmitter.Emit(...)` from the `.Lib` package.
5. **AddSource** — write `MetamodelRegistry.g.cs` into the compilation.

Output:

```csharp
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
                Properties = new[]
                {
                    new PropertyDescriptor { Name = "Name", Type = "string", Required = true },
                    new PropertyDescriptor { Name = "Icon", Type = "string", Required = false },
                },
            },
            // ... one entry per [MetaConcept] in the compilation
        };
}
```

Every DSL concept becomes discoverable without manual registration.

## Design-Time Constraint Execution

`MetaConstraintRunner` (in the `Design` package) walks the `[MetaConstraint]` attributes on a type and invokes each method via reflection:

```csharp
using FrenchExDev.Net.Dsl.Design;

var ctx = BuildContextFromType(typeof(MyOrder));
var result = MetaConstraintRunner.RunConstraints(typeof(AggregateRootAttribute), ctx);
if (!result.IsSatisfied)
    Console.WriteLine(result.Message);
```

Used by IDE/tooling integrations and by source generators that want to validate user models before emitting code.

## The M3 Fixed Point

The `Concepts/` folder contains five companion classes — one per primitive — and each is wired up by `[MetaConcept(typeof(...))]`:

```csharp
public sealed class MetaConceptConcept : MetaConcept
{
    public override string Name => "MetaConcept";
    public override Type AttributeType => typeof(MetaConceptAttribute);
}

[MetaConcept(typeof(MetaConceptConcept))]
public sealed class MetaConceptAttribute : Attribute { ... }
```

`MetaConceptAttribute` is a `MetaConcept`. `MetaPropertyAttribute` is a `MetaConcept`. The system describes itself with itself. There is no M4.

## Target Frameworks

| Project | TFM | Why |
|---|---|---|
| `Dsl` | `netstandard2.0;net10.0` | Used by attribute projects (netstandard2.0) and runtime projects (net10.0) |
| `Dsl.Design` | `net10.0` | Design-time only, can use modern APIs |
| `Dsl.SourceGenerator` | `netstandard2.0` | Roslyn analyzers must target netstandard2.0 |
| `Dsl.SourceGenerator.Lib` | `netstandard2.0` | No Roslyn dependency, reusable by any SG |
