# MAPPER-PATTERN — Philosophy

Object mapping should be **compile-time**, **typed**, and **explicit**. Source generation produces concrete mapper classes from declarative attributes — no runtime reflection, no expression trees, no `DynamicInvoke`, no AOT incompatibility.

## Source Generation Over Runtime Reflection

AutoMapper, Mapster, and similar libraries resolve mappings at runtime using reflection. This works, but:

- Mapping failures are discovered at runtime, not compile time
- Reflection-based mapping is incompatible with AOT compilation
- Performance depends on caching strategies that vary by library version
- Missing property mappings are silent unless you configure strict mode (and remember to do so)

A source generator reads `[MapFrom]` and `[MapTo]` attributes at compile time and emits concrete mapper classes. The generated code is plain C# property assignments — no reflection, no expression trees. It's visible in the IDE, debuggable, and AOT-compatible.

If a source property doesn't exist, the SG emits a compiler diagnostic. Missing mappings are compile errors, not runtime surprises.

## Typed Interface Over `object Map(object)`

AutoMapper's `IMapper` has `TTarget Map<TTarget>(object source)`. The source type is erased to `object`. This means:

- You can call `mapper.Map<OrderDto>(userEntity)` and get a runtime error
- The compiler can't verify a mapping exists for a given source/target pair
- IntelliSense can't tell you what a mapper supports

A typed interface is the alternative:

```csharp
public interface IMapper<in TSource, out TTarget>
{
    TTarget Map(TSource source);
}
```

The compiler verifies both types. If you have an `IMapper<OrderEntity, OrderDto>`, it can only map `OrderEntity` to `OrderDto`. No runtime type errors. The variance (`in`/`out`) enables polymorphic usage without losing type safety.

## Synchronous Because Mapping Is Pure Data Transformation

`Map` is `TSource → TTarget`. Synchronous. Not `Task<TTarget>`.

A mapper's job is to copy fields from one shape to another. It doesn't make I/O calls. It doesn't load related entities. If you need async work to compute a target field, that work belongs in a query/service, not in the mapper.

Keeping mappers synchronous makes them composable in LINQ chains (`entities.Select(mapper.Map)`) and prevents the pattern of "every mapper now needs `await` and a `CancellationToken`" creep.

## Separate Attributes Project Because The SG Needs It

The source generator runs as a Roslyn analyzer. It needs to read attribute metadata. If the attributes lived in the main `Mapper` assembly (which targets `netstandard2.0`+ for runtime use), the SG would need to load it from an analyzer context — which has complex dependency resolution rules.

A separate `Mapper.Attributes` project with **zero dependencies** is the cleanest analyzer input. The SG references it; it never loads the main Mapper assembly. This is the same pattern as `Builder.Attributes` + `Builder.SourceGenerator` in the FrenchExDev ecosystem.

## `MapFrom` And `MapTo` Are Equivalent, Not Different

`[MapFrom(typeof(Source))]` on the target and `[MapTo(typeof(Target))]` on the source produce the **same** mapper. They exist as alternatives for different code organizations.

- Use `[MapFrom]` when the target type is the "owner" — common in DTO projects that shouldn't reference domain entities directly. The DTO project says "I can be mapped from this entity."
- Use `[MapTo]` when the source type is the owner — the domain project says "I can be mapped to this DTO."

The choice is about code organization, not behavior.

## `AllowMultiple` Because Types Map To Many Things

An `OrderEntity` might map to `OrderDto`, `OrderSummaryDto`, `OrderListItemDto`, and `OrderExportRow`. Restricting one mapping per type would require wrapper types or manual mappers for the extras.

`AllowMultiple = true` on `[MapFrom]` and `[MapTo]` lets one type participate in as many mappings as needed. The SG generates a separate mapper class for each pair.

## `OneWay` Because Reverse Mappings Are Often Wrong

A `UserEntity → UserDto` mapping is natural: copy Id, Name, Email. The reverse `UserDto → UserEntity` is dangerous: should it set the password hash? The creation date? The internal flags?

By default, the SG generates both directions (maximizing convenience). `[OneWay]` suppresses the reverse when it would be unsafe. This is an opt-out, not an opt-in, because most simple DTOs have safe bidirectional mappings.

## Anchor Package

[`Net/FrenchExDev/Mapper/`](../../../Net/FrenchExDev/Mapper/) — `IMapper<in TSource, out TTarget>`, five attributes (`MapFrom`, `MapTo`, `MapProperty`, `IgnoreMapping`, `OneWay`), test assertions. Designed for a source generator to consume.
