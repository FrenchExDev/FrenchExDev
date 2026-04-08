# DSL-FOUNDATIONS — How To

Recipes for building a new DSL on top of the five M3 primitives.

## Step 1 — Declare the Companion Class

The companion carries behavior. Start with the bare minimum: name and attribute type.

```csharp
using FrenchExDev.Net.Dsl;

public sealed class WidgetConcept : MetaConcept
{
    public override string Name => "Widget";
    public override Type AttributeType => typeof(WidgetAttribute);
}
```

## Step 2 — Declare the Attribute

The attribute is the developer-facing surface. Decorate it with `[MetaConcept]` pointing at the companion.

```csharp
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

The naming convention is `{Name}Attribute` ↔ `{Name}Concept`. Stick to it — tooling and humans both depend on it.

## Step 3 — Use It

```csharp
[Widget("ProductList", Icon = "grid")]
public class ProductListWidget { /* ... */ }
```

That is the entire developer surface for your DSL. The MetamodelRegistry source generator picks up the new concept automatically — no manual registration.

## Adding Properties

Use `[MetaProperty]` for any typed slot:

```csharp
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

The `(name, type)` strings end up in the registry. Source generators downstream read them.

## Adding References

Use `[MetaReference]` when one concept points at another:

```csharp
[MetaReference("Target", "Entity", IsContainment = true, Multiplicity = "1")]
public Type TargetType { get; set; }
```

`IsContainment = true` means "this concept owns the target." `Multiplicity` carries cardinality (`"1"`, `"0..1"`, `"*"`, `"1..*"`).

## Adding Constraints

Constraints are real C# methods. The attribute references them via `nameof()`:

```csharp
[MetaConcept(typeof(AggregateRootConcept))]
[MetaConstraint("MustHaveId", nameof(MustHaveIdConstraint),
    Message = "Aggregate root must have an [EntityId] property")]
public sealed class AggregateRootAttribute : Attribute
{
    public static ConstraintResult MustHaveIdConstraint(ConceptValidationContext ctx)
    {
        foreach (var p in ctx.Properties)
            if (p.AttributeNames.Contains("EntityId"))
                return ConstraintResult.Satisfied();

        return ConstraintResult.Failed("Aggregate root must have an [EntityId] property");
    }
}
```

Rules:
- The method must be `public static`.
- It accepts `ConceptValidationContext` and returns `ConstraintResult`.
- Reference it with `nameof(...)` so refactoring is safe.

## Metamodel Inheritance

`[MetaInherits]` declares inheritance at the metamodel level:

```csharp
[MetaConcept(typeof(AggregateRootConcept))]
[MetaInherits(typeof(EntityConcept))]
public sealed class AggregateRootAttribute : Attribute { ... }
```

Mirror the same in the companion class:

```csharp
public sealed class AggregateRootConcept : MetaConcept
{
    public override IReadOnlyList<Type> SuperTypes => new[] { typeof(EntityConcept) };
}
```

This is **not** the same as C# inheritance. C# attributes can inherit from each other, but metamodel inheritance describes the conceptual relationship (an aggregate root *is a* kind of entity), which a downstream generator can act on.

## Adding Behavior to a Companion

The companion supports five virtual hooks and one containment check:

```csharp
public sealed class AggregateRootConcept : MetaConcept
{
    public override string Name => "AggregateRoot";
    public override Type AttributeType => typeof(AggregateRootAttribute);
    public override IReadOnlyList<Type> SuperTypes => new[] { typeof(EntityConcept) };

    public override bool CanContain(MetaConcept child)
        => child is EntityConcept || child is ValueObjectConcept;

    public override ConstraintResult Validate(ConceptValidationContext ctx) { ... }

    public override void OnDiscovered(ConceptValidationContext ctx) { ... }
    public override void OnBeforeValidation(ConceptValidationContext ctx) { ... }
    public override void OnAfterValidation(ConceptValidationContext ctx, ConstraintResult result) { ... }
}
```

Override only what you use. Defaults do nothing.

## Running Constraints at Design Time

```csharp
using FrenchExDev.Net.Dsl.Design;

var ctx = BuildContext(typeof(MyOrder));   // your code: walk the type, fill the context
var result = MetaConstraintRunner.RunConstraints(typeof(AggregateRootAttribute), ctx);
if (!result.IsSatisfied)
    Console.WriteLine(result.Message);
```

`MetaConstraintRunner` walks every `[MetaConstraint]` attribute on the type, invokes each method via reflection, aggregates the results.

## Reading the Registry from a Source Generator

The MetamodelRegistry SG emits a static class. From a downstream generator, **don't** read the generated registry — read the same compilation symbols directly:

```csharp
// In your IIncrementalGenerator pipeline:
var concepts = context.SyntaxProvider
    .ForAttributeWithMetadataName(
        "FrenchExDev.Net.Dsl.MetaConceptAttribute",
        predicate: static (_, _) => true,
        transform: static (ctx, _) => ExtractConcept(ctx));
```

The registry is for **runtime** consumers (validators, design tools, IDE plugins). Source generators run earlier and should walk the AST themselves.

## Self-Hosting Sanity Check

A new DSL is healthy when:

- [ ] Every concept attribute has a matching companion class with the same prefix
- [ ] Every companion overrides at least `Name` and `AttributeType`
- [ ] Every constraint method is `public static` with the right signature
- [ ] `MetaConstraintRunner.RunConstraints` runs without throwing
- [ ] The MetamodelRegistry generator emits an entry for every concept
- [ ] No constraint method does I/O or reads the clock

## Anti-Patterns

| Don't | Why |
|---|---|
| Express constraints as strings | OCL is interpreted, untyped, and undebuggable. Use methods. |
| Invoke constraint methods directly from generated code | Use `MetaConstraintRunner` so all constraints run uniformly. |
| Skip the companion class for "trivial" concepts | The convention is more valuable than the per-concept savings. Always pair them. |
| Add runtime dependencies to the M3 attribute project | M3 sits at the bottom of every dependency graph. Keep it pure. |
| Encode behavior in attribute properties | Attributes are passive metadata. Behavior belongs on the companion. |
| Ignore `[MetaInherits]` and rely on C# attribute inheritance | C# attribute inheritance does not mean what you think it means. Be explicit. |
