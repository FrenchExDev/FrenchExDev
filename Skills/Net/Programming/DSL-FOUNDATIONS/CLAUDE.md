# DSL-FOUNDATIONS — Claude Context

M3 metamodel framework with 5 fixed-point primitives (MetaConcept, MetaProperty, MetaReference, MetaConstraint, MetaInherits). Attributes are the modeling tool; behavioral companions split passive data from active behavior. Self-describing — no M4 needed.

## Skill docs
- [Philosophy](PHILOSOPHY.md) — design rationale and trade-offs
- [Architecture](ARCHITECTURE.md) — internal structure
- [How-To](HOW-TO.md) — step-by-step tasks
- [Requirements](REQUIREMENTS.md) — formal requirements

## Related skills
- [DDD](../DDD/) — DDD attributes built on M3
- [BUILDER-PATTERN](../BUILDER-PATTERN/) — construction
- [COMPOSE-BUNDLE](../COMPOSE-BUNDLE/) — schema-driven generation
- [ENTITY-DSL](../ENTITY-DSL/) — bridge SG
- [DIEM-CMF](../DIEM-CMF/) — sub-DSL composition

## Related packages
- [`FrenchExDev.Net.Dsl`](../../../../Net/FrenchExDev/Dsl/)

## Notes for Claude
- 5 primitives are sufficient — 6 redundant, 4 insufficient
- System describes itself: `MetaConceptAttribute` is a `[MetaConcept]`
- Naming convention non-negotiable: `{Name}Attribute` <-> `{Name}Concept`
- Constraints never throw — return `ConstraintResult.Satisfied()` or `.Failed(...)`
- Zero dependencies on M3 — sits at bottom of every dependency graph
- Read the registry at runtime; walk AST at compile time (SGs should not consume generated registry)
- Targets `netstandard2.0` with zero external NuGet deps
