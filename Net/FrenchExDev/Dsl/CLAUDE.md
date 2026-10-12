# Dsl — Claude Context

M3 metamodel framework: 5 primitives ([MetaConcept], [MetaProperty], [MetaReference], [MetaConstraint], [MetaInherits]) for building attribute-based DSLs in C#. Self-describing fixed point — equivalent to Ecore/EMOF.

## Package docs
- [README](README.md)
- [Architecture](doc/ARCHITECTURE.md)
- [How-To](doc/HOW-TO.md)
- [Philosophy](doc/PHILOSOPHY.md)

## Relevant skills
- [DSL-FOUNDATIONS](../../../Skills/Net/Programming/DSL-FOUNDATIONS/PHILOSOPHY.md)
- [SG](../../../Skills/Net/Programming/SG/PHILOSOPHY.md)
- [SOLID](../../../Skills/Net/Programming/SOLID/PHILOSOPHY.md)
- [Solution Layout](../../../Skills/Net/Programming/SOLUTION-LAYOUT/ARCHITECTURE.md)
- [Central Package Management](../../../Skills/Net/Programming/CENTRAL-PACKAGE-MANAGEMENT/ARCHITECTURE.md)

## Solution
- `FrenchExDev.Net.Dsl.slnx`

## Notes for Claude
- This is the root of the dependency graph. **Zero runtime dependencies** — never add a NuGet reference here. Every downstream DSL inherits whatever lives here.
- Self-describing fixed point: `MetaConceptAttribute` is itself `[MetaConcept(typeof(MetaConceptConcept))]`. The five primitives in `Concepts/` describe themselves; do not "simplify" by removing them.
- Naming convention is load-bearing: `{Name}Attribute` ↔ `{Name}Concept`. The MetamodelRegistry generator and `MetaConstraintRunner` both rely on this pairing.
- Constraint methods are `public static`, signature `(ConceptValidationContext) → ConstraintResult`, referenced via `nameof(...)`. Never strings, never instance methods.
- Downstream source generators should walk `[MetaConcept]` AST themselves rather than reading the generated `MetamodelRegistry` (which is for runtime consumers).
- `ValidationResult.MemberNames` is never null — `Enumerable.Empty<string>()` minimum.
