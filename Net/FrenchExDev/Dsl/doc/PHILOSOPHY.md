# Philosophy — FrenchExDev.Net.Dsl

## The Fixed Point

A metamodel framework must be able to describe itself. If MetaConcept cannot describe MetaConcept, it is incomplete. If it needs something outside itself to explain what it is, there is an M4 — and then what describes M4?

`MetaConceptAttribute` is `[MetaConcept(typeof(MetaConceptConcept))]`. This is not decoration. This is the fixed point. The system is closed. Five primitives. Self-describing. No M4.

This is the same insight behind Ecore (`EClass` is an `EClass`), Lisp (code is data), and compilers written in their own language. The representation and the thing represented are the same.

## Why Five Primitives

We started with the question: what is the minimum vocabulary needed to describe any modeling language?

- **MetaConcept** — "this thing exists" (a concept in the domain)
- **MetaProperty** — "this thing has a typed slot" (configuration)
- **MetaReference** — "this thing points to that thing" (association)
- **MetaConstraint** — "this thing must satisfy a rule" (validation)
- **MetaInherits** — "this thing is a kind of that thing" (inheritance)

Six would be redundant. Four would be insufficient. Five is the minimum vocabulary that can describe Ecore, MOF, UML class diagrams, ER diagrams, and every DSL we've built on top of it.

We verified this by describing Ecore itself using our five primitives. `EClass` maps to `[MetaConcept]`. `EAttribute` maps to `[MetaProperty]`. `EReference` maps to `[MetaReference]`. If we couldn't describe Ecore, our vocabulary would be too small. We can, so it isn't.

## Why Attributes, Not a Modeling Language

Ecore uses XMI/XML. EMF uses `.ecore` files. MPS uses projectional editing. We use C# attributes.

This is not a compromise. It is a deliberate choice:

**The compiler IS the modeling tool.** There is no separate modeling environment. No XML files to maintain. No Eclipse plugin to install. No model-to-code transformation step. The C# file is the model. `dotnet build` is the transformation.

**The IDE IS the navigation tool.** Ctrl+Click on `typeof(EntityConcept)` jumps to the concept. Find All References on `MetaConceptAttribute` shows every DSL concept. Rename refactoring updates everything. No custom tooling required.

**The type system IS the constraint language.** `[MetaInherits(typeof(EntityConcept))]` uses `typeof()` — compiler-checked. `[MetaConstraint("MustHaveId", nameof(MustHaveIdConstraint))]` uses `nameof()` — refactor-safe. A typo is a compile error, not a runtime discovery.

Other frameworks have modeling tools because their host language can't express models natively. C# attributes can. So we use them.

## Why Behavioral Companions

Ecore's `EClass` is a live object with methods: `isSuperTypeOf()`, `getEAllStructuralFeatures()`, `getEPackage()`. It is not just a descriptor. It acts.

Our attributes are passive — C# attributes are limited to data storage. No virtual methods. No interfaces. No complex construction. They describe but don't act.

So every attribute has a companion: a `MetaConcept` subclass that carries behavior. The attribute says "I am an AggregateRoot with these properties." The companion says "I can validate, I can check containment, I hook into the pipeline."

This separation is not a limitation. It is a feature. The declarative side (attribute) and the behavioral side (companion) have different consumers: the attribute is read by developers and source generators; the companion is invoked by validators and design tools. Keeping them separate makes each simpler.

## Why Constraints Are Methods, Not Strings

Ecore and UML use OCL (Object Constraint Language) — string expressions evaluated at runtime. We tried this first. Then we asked: why express constraints in a language that isn't C# when the system already runs in C#?

A constraint method:
- Is a real C# method — debuggable, breakpointable
- Has a typed signature — `ConceptValidationContext` in, `ConstraintResult` out
- Is referenced by `nameof()` — refactor-safe
- Can be unit tested — like any other method
- Has full IDE support — IntelliSense, go-to-definition, find references

A string expression has none of these. The only advantage of strings is that they can be stored in XMI files. We don't use XMI files.

## Why Source Generation, Not Interpretation

Ecore generates Java code from models via JET templates or Acceleo. The generation is a separate step: edit model, run generator, compile output.

Roslyn source generators eliminate the separate step. The generator runs inside the compiler. Edit the model (the C# file with attributes), save, and the generated code exists in the compilation. No button to press. No command to run. No generated files on disk to version-control.

This changes the feedback loop from minutes (edit → generate → compile → test) to seconds (edit → save → IntelliSense updates). The developer sees the effect of their model changes immediately.

## Why Zero Dependencies

`FrenchExDev.Net.Dsl` depends on nothing. Not on Roslyn. Not on any NuGet package. Not on any framework. It targets `netstandard2.0` — the widest possible compatibility.

This is because M3 sits at the bottom of every dependency graph. Every DSL references it. Every attribute project references it. Every source generator loads it. If M3 had dependencies, those dependencies would propagate everywhere.

Zero dependencies means zero conflicts. Any project on any .NET version can use the Dsl framework. The source generator (which must target `netstandard2.0`) can reference it without workarounds. The design-time tools can reference it without version conflicts.

The five primitives, the companion base class, the validation context, and the constraint result — all implemented in plain C# with no external dependencies. This is the foundation. It must be solid.

## The Bigger Picture

The Dsl framework does not know about DDD. It does not know about content management. It does not know about workflows or admin interfaces or page trees.

It knows about concepts, properties, references, constraints, and inheritance. That's all.

From these five primitives, the DDD DSL builds 13 concepts (AggregateRoot, Entity, Composition, Invariant...). The Content DSL builds parts and blocks. The Workflow DSL builds state machines. Each DSL speaks the same M3 vocabulary but says completely different things.

This is why the Dsl framework is a separate project from Diem. Diem is a CMF. The Dsl framework is the meta-language that Diem's DSLs are written in. Someone could use the Dsl framework to build a completely different kind of system — a game engine DSL, a hardware description DSL, a financial modeling DSL — without ever touching Diem.

The five primitives don't care what you model. They care that your model is well-structured, validated, and discoverable. The rest is up to you.
