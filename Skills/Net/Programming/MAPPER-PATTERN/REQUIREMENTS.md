# MAPPER-PATTERN — Requirements

## Project Layout

- [ ] Four projects: `Mapper`, `Mapper.Attributes`, `Mapper.Testing`, `Mapper.SourceGenerator`.
- [ ] `Mapper` and `Mapper.Attributes` have **zero NuGet dependencies**.
- [ ] `Mapper.Attributes` is independent of `Mapper` (it is read by an analyzer, not application code).
- [ ] `Mapper.SourceGenerator` references `Mapper.Attributes` + Roslyn.

## Interface

- [ ] `IMapper<in TSource, out TTarget>` with a single method `TTarget Map(TSource source)`.
- [ ] Contravariant on `TSource` (`in`).
- [ ] Covariant on `TTarget` (`out`).
- [ ] Synchronous — no `Task<TTarget>` variant.

## Attributes

- [ ] `[MapFrom(typeof(Source))]` on target classes — `AllowMultiple = true`.
- [ ] `[MapTo(typeof(Target))]` on source classes — `AllowMultiple = true`.
- [ ] `[MapProperty("SourceProp")]` on target properties.
- [ ] `[IgnoreMapping]` on target properties.
- [ ] `[OneWay]` on either source or target classes.
- [ ] `[MapFrom]` and `[MapTo]` produce equivalent mappers — choosing between them is about code organization, not behavior.

## Source Generator Behavior

- [ ] Generates one `sealed class {Source}To{Target}Mapper : IMapper<Source, Target>` per mapping pair.
- [ ] Reverse mapper auto-generated unless `[OneWay]` is present.
- [ ] Property mapping defaults to same-named source property.
- [ ] `[MapProperty("X")]` overrides the source property name.
- [ ] `[IgnoreMapping]` skips a target property entirely.
- [ ] Missing source property → compile diagnostic.
- [ ] Generated code is plain property assignments — **no reflection, no expression trees, no `DynamicInvoke`**.

## Test Assertions

- [ ] `mapper.ShouldMapTo(source, expected)` — full equality assertion.
- [ ] `mapper.ShouldMapProperty(source, selector, expected)` — single property assertion.
- [ ] Both throw a dedicated `MapperAssertionException` (not raw xUnit assertion failures).
- [ ] Both null-check the mapper and selector arguments.

## Multi-Targeting

- [ ] `Mapper` targets `netstandard2.0 + net10.0`.
- [ ] `Mapper.Attributes` targets `netstandard2.0 + net10.0`.
- [ ] `Mapper.SourceGenerator` targets `netstandard2.0` (Roslyn requirement).

## What MUST NOT Be Done

- [ ] No untyped `IMapper.Map<T>(object source)` interface.
- [ ] No async `Map` variant.
- [ ] No runtime configuration API — all mapping decisions are compile-time attributes.
- [ ] No reflection-based fallback — if the SG can't generate, the missing mapping is a compile error.
- [ ] Mappers MUST NOT load related entities or perform I/O.

## DI

- [ ] Mappers are stateless — register as singleton.

## Anchor Package

[`Net/FrenchExDev/Mapper/`](../../../Net/FrenchExDev/Mapper/) — implements every requirement.
