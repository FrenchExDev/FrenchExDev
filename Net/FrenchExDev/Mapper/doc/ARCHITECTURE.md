# Mapper -- Architecture

## 1. Overview

Mapper provides the contracts and attributes for compile-time object mapping. The `IMapper<TSource, TTarget>` interface defines a single `Map` method. Five attributes (`[MapFrom]`, `[MapTo]`, `[MapProperty]`, `[IgnoreMapping]`, `[OneWay]`) describe mapping relationships declaratively. A source generator (separate project) reads these attributes and emits concrete mapper classes at compile time -- no runtime reflection.

---

## 2. Project Structure

```
Mapper/
  FrenchExDev.Net.Mapper.slnx
  quality-gate.yml
  src/
    FrenchExDev.Net.Mapper/                        (Interface -- netstandard2.0 + net10.0)
      IMapper.cs                                   IMapper<in TSource, out TTarget> : Map(TSource) → TTarget
    FrenchExDev.Net.Mapper.Attributes/             (Attributes -- netstandard2.0 + net10.0)
      MapFromAttribute.cs                          [MapFrom(typeof(Source))] on target type
      MapToAttribute.cs                            [MapTo(typeof(Target))] on source type
      MapPropertyAttribute.cs                      [MapProperty("SourceProp")] on target property
      IgnoreMappingAttribute.cs                    [IgnoreMapping] on property
      OneWayAttribute.cs                           [OneWay] on class/struct
    FrenchExDev.Net.Mapper.Testing/                (Test helpers -- netstandard2.0 + net10.0)
      MapperAssertions.cs                          ShouldMapTo, ShouldMapProperty extensions
      MapperAssertionException.cs                  Assertion failure exception
  test/
    FrenchExDev.Net.Mapper.Tests/                  (xUnit tests)
      AttributeTests.cs                            8 tests for attribute metadata
      MapperInterfaceTests.cs                      7 tests for IMapper + assertions
```

---

## 3. Dependency Graph

```
FrenchExDev.Net.Mapper                   (no dependencies, netstandard2.0 + net10.0)

FrenchExDev.Net.Mapper.Attributes        (no dependencies, netstandard2.0 + net10.0)

FrenchExDev.Net.Mapper.Testing           (refs Mapper only)

FrenchExDev.Net.Mapper.Tests             (refs Mapper + Attributes + Testing + xUnit)

(Future) Mapper.SourceGenerator          (refs Attributes + Roslyn, reads attributes, emits IMapper<> impls)
```

All three shipped assemblies have zero NuGet dependencies. The Attributes project is intentionally independent of the Mapper interface -- it's referenced by the source generator analyzer, not by application code.

---

## 4. Interface Design

### IMapper<in TSource, out TTarget>

```csharp
public interface IMapper<in TSource, out TTarget>
{
    TTarget Map(TSource source);
}
```

- **Contravariant on TSource** (`in`) -- a mapper from `Animal` to `AnimalDto` can be used where a mapper from `Dog` to `AnimalDto` is expected
- **Covariant on TTarget** (`out`) -- a mapper from `Order` to `OrderDetailDto` can be used where a mapper from `Order` to `OrderDto` is expected (if `OrderDetailDto : OrderDto`)
- **Synchronous** -- mapping is a pure data transformation, not an I/O operation. No `Task<TTarget>`.
- **Single method** -- `Map(TSource) → TTarget`. No `Map(TSource, TTarget)` overwrite variant.

---

## 5. Attribute Model

### Class-level attributes (AllowMultiple = true)

| Attribute | Placed on | Meaning |
|-----------|-----------|---------|
| `[MapFrom(typeof(Source))]` | Target class | "Generate a mapper FROM Source TO me" |
| `[MapTo(typeof(Target))]` | Source class | "Generate a mapper FROM me TO Target" |
| `[OneWay]` | Source or Target | "Do NOT generate the reverse mapper" |

`AllowMultiple = true` means a single class can participate in multiple mappings:

```csharp
[MapFrom(typeof(OrderEntity))]
[MapFrom(typeof(OrderEvent))]
public class OrderDto { ... }
```

### Property-level attributes

| Attribute | Meaning |
|-----------|---------|
| `[MapProperty("SourceProp")]` | Map this property from a differently-named source property |
| `[IgnoreMapping]` | Exclude this property from automatic mapping |

---

## 6. Source Generator Integration Pattern

The SG (not part of this project) follows this flow:

```
1. Find types decorated with [MapFrom] or [MapTo]
2. For each mapping pair (Source, Target):
   a. Enumerate target properties
   b. For each property:
      - If [IgnoreMapping] → skip
      - If [MapProperty("X")] → map from source.X
      - Else → map from source.{SameName}
   c. If source property not found → diagnostic warning
3. Emit: sealed class {Source}To{Target}Mapper : IMapper<Source, Target>
4. If not [OneWay] → also emit reverse mapper
```

The emitted mapper is a concrete class with a single `Map` method that does property-by-property assignment. No reflection at runtime.

---

## 7. Testing Assertions

| Assertion | Behavior |
|-----------|----------|
| `mapper.ShouldMapTo(source, expected)` | Maps source, asserts full equality with expected |
| `mapper.ShouldMapProperty(source, t => t.Prop, expected)` | Maps source, asserts single property value |

Both throw `MapperAssertionException` on failure with descriptive messages. Both null-check the mapper and selector arguments.

---

## 8. Ecosystem Position

```
FrenchExDev.Net ecosystem:
  Mapper           ← mapping contracts + attributes (this project)
  Mapper.SG        ← source generator (future, reads attributes, emits mappers)
  Builder          ← validated construction ([Builder] → SG)
  Injectable       ← DI registration ([Injectable] → SG)
  ...

Pattern:
  Attributes project  → consumed by SG at compile time
  Interface project   → consumed by application code at runtime
  Testing project     → consumed by test projects
  SG project          → references Attributes + Roslyn, emits Interface implementations
```

This is the same 4-project pattern used by Builder (`Builder`, `Builder.Attributes`, `Builder.SourceGenerator`, `Builder.Testing`).
