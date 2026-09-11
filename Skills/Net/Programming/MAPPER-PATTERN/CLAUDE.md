# MAPPER-PATTERN — Claude Context

Compile-time typed object mapping via `IMapper<in TSource, out TTarget>`. Source generator emits plain property assignments from `[MapFrom]`/`[MapTo]` attributes. Synchronous, stateless, AOT-compatible, zero reflection.

## Skill docs
- [Philosophy](PHILOSOPHY.md) — design rationale and trade-offs
- [Architecture](ARCHITECTURE.md) — internal structure
- [How-To](HOW-TO.md) — step-by-step tasks
- [Requirements](REQUIREMENTS.md) — formal requirements

## Related skills
- [BUILDER-PATTERN](../BUILDER-PATTERN/) — SG architecture
- [INJECTABLE-DI](../INJECTABLE-DI/) — DI registration

## Related packages
- [`FrenchExDev.Net.Mapper`](../../../../Net/FrenchExDev/Mapper/)
- [`FrenchExDev.Net.Mapper.Attributes`](../../../../Net/FrenchExDev/Mapper.Attributes/)
- [`FrenchExDev.Net.Mapper.Testing`](../../../../Net/FrenchExDev/Mapper.Testing/)
- [`FrenchExDev.Net.Mapper.SourceGenerator`](../../../../Net/FrenchExDev/Mapper.SourceGenerator/)

## Notes for Claude
- Mappers are stateless — always register as singleton
- Missing source property = compile-time diagnostic
- `[MapProperty("SourceProp")]` for renamed properties; `[IgnoreMapping]` to skip
- `[OneWay]` suppresses dangerous reverse mapper auto-generation
- Do NOT load related entities inside `Map` — mappers receive what they need
- Do NOT add validation to mappers — use Result-returning validators
- Do NOT make `Map` async — mapping is pure data transformation
- Open generics not auto-detected by SG — mappings require concrete types
