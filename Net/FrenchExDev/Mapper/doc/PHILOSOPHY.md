# Mapper -- Philosophy

## Source generation over runtime reflection

AutoMapper, Mapster, and similar libraries resolve mappings at runtime using reflection. This works, but:

- Mapping failures are discovered at runtime, not compile time
- Reflection-based mapping is incompatible with AOT compilation
- Performance depends on caching strategies that vary by library version
- Missing property mappings are silent unless you configure strict mode (and remember to do so)

A source generator reads `[MapFrom]` and `[MapTo]` attributes at compile time and emits concrete mapper classes. The generated code is plain C# property assignments -- no reflection, no expression trees, no `DynamicInvoke`. It's visible in the IDE, debuggable, and AOT-compatible.

If a source property doesn't exist, the SG emits a compiler diagnostic. Missing mappings are compile errors, not runtime surprises.

---

## Typed interface over untyped Map

AutoMapper's `IMapper` has `TTarget Map<TTarget>(object source)`. The source type is erased to `object`. This means:

- You can call `mapper.Map<OrderDto>(userEntity)` and get a runtime error
- The compiler can't verify that a mapping exists for a given source/target pair
- IntelliSense can't tell you what a mapper supports

`IMapper<in TSource, out TTarget>` is fully typed. `Map(TSource) → TTarget`. The compiler verifies both types. If you have an `IMapper<OrderEntity, OrderDto>`, it can only map `OrderEntity` to `OrderDto`. No runtime type errors.

The variance (`in TSource, out TTarget`) enables polymorphic usage without losing type safety.

---

## Separate Attributes project because the SG needs it

The source generator runs as a Roslyn analyzer. It needs to read the attribute metadata. If the attributes lived in the main `Mapper` assembly (which targets `netstandard2.0`), the SG would need to reference a `netstandard2.0` assembly from an analyzer context -- which has complex dependency resolution rules.

A separate `Mapper.Attributes` project with no dependencies is the cleanest analyzer input. The SG references it, reads the attribute types, and never loads the main Mapper assembly.

This is the same pattern as `Builder.Attributes` + `Builder.SourceGenerator` and `Injectable.Attributes` + `Injectable.SourceGenerator` in the FrenchExDev ecosystem.

---

## MapFrom and MapTo are equivalent, not different

`[MapFrom(typeof(Source))]` on the target and `[MapTo(typeof(Target))]` on the source produce the same mapper. They exist as alternatives, not as distinct features.

Use `[MapFrom]` when the target type is the "owner" of the mapping -- common in DTO projects that shouldn't reference domain entities:

```csharp
// In the DTO project (doesn't reference Domain)
[MapFrom(typeof(OrderEntity))]  // ← the SG knows the source type via typeof
public class OrderDto { ... }
```

Use `[MapTo]` when the source type is the owner:

```csharp
// In the Domain project
[MapTo(typeof(OrderDto))]
public class OrderEntity { ... }
```

The choice is about code organization, not behavior.

---

## AllowMultiple because types map to many things

An `OrderEntity` might map to `OrderDto`, `OrderSummaryDto`, `OrderListItemDto`, and `OrderExportRow`. Restricting to one mapping per type would require wrapper types or manual mappers for the extras.

`AllowMultiple = true` on `[MapFrom]` and `[MapTo]` lets one type participate in as many mappings as needed. The SG generates a separate mapper class for each pair.

---

## OneWay because reverse mappings are often wrong

A `UserEntity → UserDto` mapping is natural: copy Id, Name, Email. The reverse `UserDto → UserEntity` is dangerous: should it set the password hash? The creation date? The internal flags?

By default, the SG generates both directions (maximizing convenience). `[OneWay]` suppresses the reverse when it would be unsafe or meaningless. This is an opt-out, not an opt-in, because most simple DTOs have safe bidirectional mappings.

---

## Assertions on the mapper, not on the result

`mapper.ShouldMapTo(source, expected)` reads better than `Assert.Equal(expected, mapper.Map(source))`. More importantly, `mapper.ShouldMapProperty(source, t => t.Name, "Alice")` tests a single property without requiring full equality on the target type.

Full equality requires overriding `Equals` (or using records). Property assertions don't. For types with 20 properties where you're testing one specific mapping rule, property assertions are more focused and produce better failure messages.
