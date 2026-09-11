# INJECTABLE-DI — Architecture

## Project Decomposition

```
Attributes (netstandard2.0 + net10.0, zero deps)
    InjectableAttribute, InjectableDefaultsAttribute, InjectableDecoratorAttribute, Scope enum

SourceGenerator.Lib (netstandard2.0, no Roslyn)
    InjectableEmitModel
    InjectableServiceModel
    InjectableDecoratorModel
    InterfaceContractModel
    InjectableEmitter           ← reusable emission, one method per container

Microsoft.SourceGenerator (netstandard2.0, Roslyn)
    MicrosoftInjectableGenerator : IIncrementalGenerator
    Links SourceGenerator.Lib source files

DryIoc.SourceGenerator (netstandard2.0, Roslyn)
    DryIocInjectableGenerator : IIncrementalGenerator
    Links SourceGenerator.Lib source files

Analyzers (netstandard2.0, Roslyn)
    CaptiveDependencyAnalyzer        (INJECT001 — warning)
    AbstractClassDecoratedAnalyzer    (INJECT002 — warning)
    MissingInjectableAnalyzer         (INJECT003 — info)
    InterfaceScopeMismatchAnalyzer    (INJECT004 — error)
```

The Attributes project has zero dependencies — it is read by analyzers and source generators, not by application code's runtime path.

The Lib project holds all emission logic in pure C# (no Roslyn types), so multiple container-specific generators can share it via linked source files.

## Source Generator Pipeline

1. **Pipeline 1 — Classes**: `ForAttributeWithMetadataName` scans for `[Injectable]`-decorated classes (`AllowMultiple = true`). Each class becomes one or more `InjectableServiceModel` instances.
2. **Pipeline 2 — Interfaces**: scans for `[Injectable]`-decorated interfaces. Each becomes an `InterfaceContractModel`. The generator walks the assembly's type tree to find non-abstract classes implementing the interface and auto-registers them with the contract's scope.
3. **Pipeline 3 — Decorators**: `[InjectableDecorator]` classes become `InjectableDecoratorModel` instances.
4. Assembly-level `[InjectableDefaults]` is read for the assembly's default scope.
5. All models are collected. Defaults applied where scope was not explicitly set. Interface contracts skip classes already explicitly decorated.
6. The container-specific emitter produces one extension method file per assembly.
7. Output: `InjectableExtensions.g.cs`

## Attribute Reference

```csharp
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = true)]
public sealed class InjectableAttribute : Attribute
{
    public Scope Scope { get; set; } = Scope.Transient;
    public Type? As { get; set; }
    public bool TryAdd { get; set; }
    public string? Key { get; set; }
}

public enum Scope { Transient, Scoped, Singleton }
```

| Property | Default | Meaning |
|---|---|---|
| `Scope` | `Transient` | Service lifetime |
| `As` | `null` | Explicit service type — registers ONLY as this type |
| `TryAdd` | `false` | Use `TryAdd*` (avoids duplicates) |
| `Key` | `null` | Keyed service registration (.NET 8+) |

## Interface Resolution Rules

- `As` specified → register only as that type.
- No `As`, class implements interfaces → register for **each directly implemented interface**.
- No `As`, no interfaces → register as self (concrete type).
- Abstract class → skipped entirely (with INJECT002 warning).
- Open generics → unbound `typeof(IRepo<>)` / `typeof(Repo<>)` registration.

## Registration Modes

| Mode | Attribute | Microsoft DI Output |
|---|---|---|
| Standard | `[Injectable]` | `services.AddTransient<IFoo, Foo>()` |
| TryAdd | `[Injectable(TryAdd = true)]` | `services.TryAddTransient<IFoo, Foo>()` |
| Keyed (.NET 8+) | `[Injectable(Key = "primary")]` | `services.AddKeyedSingleton<IFoo, Foo>("primary")` |
| Open generic | `[Injectable]` on `Repo<T> : IRepo<T>` | `services.AddTransient(typeof(IRepo<>), typeof(Repo<>))` |
| Decorator | `[InjectableDecorator(typeof(IFoo))]` | Service replacement / `Setup.Decorator` |
| Interface contract | `[Injectable]` on interface | Implementations auto-registered |

## Extension Method Naming

The generated method name is derived from the assembly name by sanitizing separators:

| Assembly | Generated Method |
|---|---|
| `MyApp.Services` | `AddMyAppServicesInjectables()` |
| `FrenchExDev.Net.Diem` | `AddFrenchExDevNetDiemInjectables()` |
| `Acme-Web-Api` | `AddAcmeWebApiInjectables()` |

## Analyzers

| ID | Severity | Description |
|---|---|---|
| INJECT001 | Warning | Captive dependency: Singleton depends on Scoped/Transient |
| INJECT002 | Warning | `[Injectable]` on abstract class has no effect |
| INJECT003 | Info | Class implements interface but is not decorated |
| INJECT004 | Error | Implementation scope violates interface `[Injectable]` contract |

## Emitter Reusability

`InjectableEmitter` exposes one `Emit{Container}(InjectableEmitModel model)` method per supported container. The model is container-agnostic. The methods produce container-specific output. Adding a new container is a new method on the emitter and a new generator project — no changes to the model.

## Anchor Package

[`Net/FrenchExDev/Injectable/`](../../../Net/FrenchExDev/Injectable/) — full implementation.
