# Ddd — Claude Context

Domain-Driven Design DSL built on the M3 framework: `[AggregateRoot]`, `[Entity]`, `[ValueObject]`, `[Composition]`, `[Aggregation]`, `[Association]`, `[Invariant]`, `[DomainEvent]`, plus a source generator that emits entity implementations and invariant enforcement.

## Package docs
- [README](README.md)
- [Architecture](doc/ARCHITECTURE.md)
- [How-To](doc/HOW-TO.md)
- [Philosophy](doc/PHILOSOPHY.md)

## Relevant skills
- [DDD](../../../Skills/Net/Programming/DDD/PHILOSOPHY.md)
- [DSL-FOUNDATIONS](../../../Skills/Net/Programming/DSL-FOUNDATIONS/PHILOSOPHY.md)
- [SOLID](../../../Skills/Net/Programming/SOLID/PHILOSOPHY.md)
- [Solution Layout](../../../Skills/Net/Programming/SOLUTION-LAYOUT/ARCHITECTURE.md)
- [Central Package Management](../../../Skills/Net/Programming/CENTRAL-PACKAGE-MANAGEMENT/ARCHITECTURE.md)

## Solution
- `FrenchExDev.Net.Ddd.slnx`

## Notes for Claude
- DDD attributes are persistence-agnostic. The Ddd assembly **must not** reference EF Core or any persistence library — that crossover is the job of the `Ddd.Entity.Dsl` bridge SG.
- Relationship attributes (`[Composition]`, `[Aggregation]`, `[Association]`) carry implied lifecycle semantics that downstream generators (Entity DSL) translate to delete behaviors. Don't override the convention casually.
- Every entity class must be `partial` — generators emit behavior partials.
- Invariants are real C# methods; the SG wires them into a generated `EnsureInvariants()` call. Failed invariants surface as `Result.Failure(...)`, never thrown.
