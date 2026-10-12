# DSL-FOUNDATIONS — Philosophy

A metamodel framework must be able to describe itself. If the meta-language needs something outside itself to explain what it is, you have an M4, and then you need an M5, and so on. The right answer is a fixed point: five primitives that can describe any modeling language, and can describe themselves.

## The Fixed Point

Five primitives are enough. Six are redundant. Four are insufficient.

| Primitive | What it declares |
|---|---|
| **MetaConcept** | This thing exists. A concept in the domain. |
| **MetaProperty** | This thing has a typed slot. Configuration data. |
| **MetaReference** | This thing points to that thing. A directed association. |
| **MetaConstraint** | This thing must satisfy a rule. Validation. |
| **MetaInherits** | This thing is a kind of that thing. Inheritance at the metamodel level. |

That is the entire vocabulary. Every DSL — DDD, content management, workflow, requirements, function blocks, anything — is built by spelling its concepts in these five words.

The five primitives are themselves declared with the five primitives. `MetaConceptAttribute` is `[MetaConcept(typeof(MetaConceptConcept))]`. `MetaPropertyAttribute` is `[MetaConcept(typeof(MetaPropertyConcept))]`. The system is closed. There is no M4.

This is the same insight behind Ecore (`EClass` is an `EClass`), Lisp (code is data), and self-hosting compilers. The representation and the thing represented are the same.

## Why Attributes, Not a Modeling Language

Other metamodel frameworks use XMI files, `.ecore` files, or projectional editors. We use C# attributes.

This is not a compromise. The host language is the modeling tool:

- **The compiler is the modeling tool.** No separate environment. No XML files. No model-to-code transform. The C# file is the model. `dotnet build` is the transformation.
- **The IDE is the navigation tool.** Ctrl+Click on `typeof(EntityConcept)` jumps to the concept. Find All References on `MetaConceptAttribute` enumerates every DSL concept in the codebase. Rename refactoring updates everything.
- **The type system is the constraint language.** `[MetaInherits(typeof(EntityConcept))]` is compiler-checked. `[MetaConstraint("MustHaveId", nameof(MustHaveIdConstraint))]` is refactor-safe. A typo is a build error, not a runtime discovery.

Other frameworks have separate modeling tools because their host languages cannot express models natively. C# can. So we use it.

## Behavioral Companions

C# attributes are passive. They store data, full stop. No virtual methods, no interfaces, no construction logic.

So every concept attribute has a **companion** — a `MetaConcept` subclass that carries behavior. The attribute says "I am an AggregateRoot with these properties." The companion says "I can validate, I can check containment, I hook into the pipeline."

```csharp
public sealed class AggregateRootConcept : MetaConcept
{
    public override string Name => "AggregateRoot";
    public override Type AttributeType => typeof(AggregateRootAttribute);
    public override IReadOnlyList<Type> SuperTypes => new[] { typeof(EntityConcept) };

    public override bool CanContain(MetaConcept child)
        => child is EntityConcept || child is ValueObjectConcept;

    public override ConstraintResult Validate(ConceptValidationContext context) { ... }

    public override void OnDiscovered(ConceptValidationContext context) { ... }
    public override void OnBeforeValidation(ConceptValidationContext context) { ... }
    public override void OnAfterValidation(ConceptValidationContext context, ConstraintResult result) { ... }
}
```

The split is a feature, not a workaround. The declarative side and the behavioral side have different consumers: attributes are read by developers and source generators; companions are invoked by validators and design-time tools. Separating them keeps each one simple.

## Constraints Are Methods, Not Strings

OCL (Object Constraint Language) and similar string-based constraint languages require a parser, a runtime evaluator, and IDE plugins to be productive. We tried this. Then we asked: why express constraints in a language that isn't C# when the system already runs in C#?

A constraint method:

- Is a real C# method — debuggable, breakpointable
- Has a typed signature — `ConceptValidationContext` in, `ConstraintResult` out
- Is referenced via `nameof(...)` — refactor-safe
- Can be unit-tested like any other method
- Has full IDE support

A string expression has none of these. The only advantage of strings is that they can live in XMI files. We don't have XMI files.

```csharp
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

## Source Generation, Not Interpretation

Ecore generates Java via JET templates. Acceleo. Modeling Workflow Engine. Each requires a separate "generate" step: edit model, run tool, compile output.

Roslyn source generators eliminate the separate step. The generator runs inside the compiler. Edit the model (the C# file with attributes), save, and the generated code exists in the compilation. No button to press, no command to run, no generated files on disk to version-control. The feedback loop drops from minutes to seconds.

A **MetamodelRegistry** generator scans every `[MetaConcept]`-decorated attribute in the compilation and emits a static registry: a dictionary of concept descriptors. Every DSL gets discovery for free. No manual registration.

## Zero Dependencies at the Root

The DSL framework depends on **nothing**. Not on Roslyn. Not on a NuGet package. Not on a runtime framework. It targets `netstandard2.0` for the widest possible compatibility.

This is because M3 sits at the bottom of every dependency graph. Every DSL references it. Every attribute project references it. Every source generator loads it. If M3 had dependencies, those dependencies would propagate to every consumer.

Zero dependencies means zero conflicts. Any project on any .NET version can use the framework. Source generators (which must target `netstandard2.0`) reference it without workarounds. Design-time tools reference it without version conflicts.

## What Five Primitives Can Build

The DSL framework does not know about DDD. It does not know about content management or workflows or admin interfaces. It knows about concepts, properties, references, constraints, and inheritance. That is all.

From these five primitives, downstream DSLs build whatever they need:

- A DDD DSL with concepts like `AggregateRoot`, `Entity`, `Composition`, `ValueObject`, `DomainEvent`, `Invariant`
- A content management DSL with `ContentType`, `Part`, `Field`, `Display`
- A workflow DSL with `Stage`, `Transition`, `Gate`, `RequiresRole`
- A function block DSL with `FunctionBlock`, `EventInput`, `DataPort`, `Algorithm`
- A requirements DSL with `Epic`, `Feature`, `AcceptanceCriterion`

Each DSL speaks the same M3 vocabulary but says completely different things. The framework cares only that every model is well-structured, validated, and discoverable. The rest is the DSL author's job.

## Trade-offs Accepted

| Trade-off | Decision |
|---|---|
| Behavior split between attribute and companion | Required because C# attributes are passive. Discoverable via convention (`{Name}Attribute` ↔ `{Name}Concept`). |
| Constraints are methods, not declarative | Loses external tooling, gains IDE support, debuggability, and tests. Worth it. |
| No XMI / external model file | Loses interop with legacy modeling tools, gains zero-tool friction. |
| `netstandard2.0` only | Can't use newer language features in M3 itself. Acceptable — M3 is mostly attribute classes and small structs. |
