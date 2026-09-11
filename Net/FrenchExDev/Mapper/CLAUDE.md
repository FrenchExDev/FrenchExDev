# Mapper — Claude Context

Source-generation-ready object mapping contracts and attributes — defines `IMapper<in TSource, out TTarget>` and five attributes (`MapFrom`, `MapTo`, `MapProperty`, `IgnoreMapping`, `OneWay`) for a source generator to consume. No runtime reflection.

## Package docs
- [README](README.md)
- [Architecture](doc/ARCHITECTURE.md)
- [How-To](doc/HOW-TO.md)
- [Philosophy](doc/PHILOSOPHY.md)

## Relevant skills
- [MAPPER-PATTERN](../../../Skills/Net/Programming/MAPPER-PATTERN/PHILOSOPHY.md)
- [SOLID](../../../Skills/Net/Programming/SOLID/PHILOSOPHY.md)
- [Solution Layout](../../../Skills/Net/Programming/SOLUTION-LAYOUT/ARCHITECTURE.md)
- [Central Package Management](../../../Skills/Net/Programming/CENTRAL-PACKAGE-MANAGEMENT/ARCHITECTURE.md)

## Solution
- `FrenchExDev.Net.Mapper.slnx`

## Notes for Claude
- Four-project decomposition: `Mapper` (interface), `Mapper.Attributes` (attributes), `Mapper.Testing` (assertions), `Mapper.Tests`. The SG project lives outside this solution and is a future addition.
- `Mapper.Attributes` is **independent of `Mapper`** — it's read by the SG, not by application code.
- `IMapper<in TSource, out TTarget>` is contravariant on source AND covariant on target. Single `Map(TSource) → TTarget` method, synchronous.
- Class-level attributes (`MapFrom`, `MapTo`, `OneWay`) have `AllowMultiple = true` so one type can participate in many mappings.
- `[MapFrom]` and `[MapTo]` are EQUIVALENT — produce the same mapper. The choice is about code organization (DTO project vs domain project), not behavior.
- Targets `netstandard2.0 + net10.0`.
- 15 tests covering attribute metadata and mapper interface assertions.
