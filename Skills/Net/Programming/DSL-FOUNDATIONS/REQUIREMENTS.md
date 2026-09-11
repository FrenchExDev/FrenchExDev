# DSL-FOUNDATIONS — Requirements

Acceptance criteria for any DSL built on the M3 framework.

## Concept Authoring

Every DSL concept must satisfy:

- [ ] One C# attribute class with `[MetaConcept(typeof(XxxConcept))]`
- [ ] One companion class derived from `MetaConcept` with the matching name (`{Name}Attribute` ↔ `{Name}Concept`)
- [ ] The companion overrides `Name` and `AttributeType`
- [ ] Both classes live in the same DSL package, not in M3 itself
- [ ] The attribute carries `[AttributeUsage(...)]` declaring its valid targets
- [ ] Required configuration is exposed as constructor parameters; optional configuration as settable properties

## Properties and References

For every attribute property:

- [ ] Scalar configuration uses `[MetaProperty(name, type, ...)]`
- [ ] Cross-concept pointers use `[MetaReference(name, target, ...)]`
- [ ] `Required = true` is set if the property must be non-null/non-default at validation time
- [ ] `Multiplicity` is declared on every reference

## Constraints

For every domain rule:

- [ ] Implemented as a `public static` method on the attribute class
- [ ] Signature is exactly `(ConceptValidationContext) → ConstraintResult`
- [ ] Referenced from `[MetaConstraint]` via `nameof(...)`
- [ ] Carries a human-readable `Message`
- [ ] Does **not** throw — failures are values
- [ ] Is deterministic (no I/O, no clock, no RNG)
- [ ] Has at least one unit test asserting `ConstraintResult.Satisfied()`
- [ ] Has at least one unit test asserting `ConstraintResult.Failed(...)` with the right message

## Inheritance

If concept B is a kind of concept A:

- [ ] The attribute carries `[MetaInherits(typeof(AConcept))]`
- [ ] The companion overrides `SuperTypes` to include `typeof(AConcept)`
- [ ] If A's containment rules apply to B, B's `CanContain` defers to base behavior

## Companion Behavior

Override only what is needed:

- [ ] `CanContain` — only if the concept has structural ownership rules
- [ ] `Validate` — only for cross-cutting validation that doesn't fit in `[MetaConstraint]`
- [ ] `OnDiscovered` / `OnBeforeValidation` / `OnAfterValidation` — only for pipeline integration

Default-do-nothing implementations are correct for most concepts. Override sparingly.

## DSL Package Structure

A new DSL package follows the same shape as the framework itself:

```
MyDsl/
├── src/
│   ├── FrenchExDev.Net.MyDsl.Attributes        attributes + companions (netstandard2.0;net10.0)
│   ├── FrenchExDev.Net.MyDsl                   runtime types (net10.0)
│   ├── FrenchExDev.Net.MyDsl.SourceGenerator   Roslyn IIncrementalGenerator (netstandard2.0)
│   └── FrenchExDev.Net.MyDsl.SourceGenerator.Lib  emitters, no Roslyn dep (netstandard2.0)
└── test/
    └── FrenchExDev.Net.MyDsl.Tests             xUnit tests (net10.0)
```

The split between `SourceGenerator` and `SourceGenerator.Lib` is mandatory. The `Lib` is unit-testable; the SG is the Roslyn shell.

## Attribute Project Constraints

The attributes project must:

- [ ] Target `netstandard2.0` (or multi-target with `net10.0`)
- [ ] Reference `FrenchExDev.Net.Dsl` and nothing else from this codebase
- [ ] Have **zero** runtime dependencies on third-party NuGet packages
- [ ] Contain only attribute classes, companion classes, and small data types

If you find yourself wanting to import EF Core, ASP.NET, or any framework — you are in the wrong project.

## SourceGenerator.Lib Constraints

The `SourceGenerator.Lib` project must:

- [ ] Target `netstandard2.0`
- [ ] **Not** reference Roslyn (`Microsoft.CodeAnalysis.*`)
- [ ] Contain pure string-based emitters and POCO models
- [ ] Be unit-testable without spinning up a compilation

## SourceGenerator Constraints

The `SourceGenerator` project must:

- [ ] Be an `IIncrementalGenerator`, not an obsolete `ISourceGenerator`
- [ ] Target `netstandard2.0`
- [ ] Reference `Microsoft.CodeAnalysis.CSharp` and `Microsoft.CodeAnalysis.Analyzers` at the versions in `Directory.Packages.props`
- [ ] Reference `SourceGenerator.Lib` for emission
- [ ] Use `ForAttributeWithMetadataName` (not `CreateSyntaxProvider`) for attribute discovery

## Runtime Validation Quality Bar

If the DSL exposes a runtime validator (e.g. via `MetaConstraintRunner`), it must:

- [ ] Run every constraint and aggregate every error
- [ ] Never throw on a failed constraint — return `ConstraintResult.Failed(...)`
- [ ] Be safe to call from multiple threads on the same context

## Things You Must Never Do

- Add runtime dependencies to the M3 framework (`FrenchExDev.Net.Dsl`)
- Express constraints as strings (use `[MetaConstraint]` + a method)
- Throw exceptions from constraint methods (return `ConstraintResult.Failed(...)`)
- Skip the companion class because "the attribute is trivial"
- Read the generated `MetamodelRegistry` from another source generator (walk the AST instead)
- Use C# attribute inheritance as a substitute for `[MetaInherits]`
- Encode behavior in attribute properties (behavior belongs on the companion)
- Generate `.ecore` or XMI files — the C# file is the model
