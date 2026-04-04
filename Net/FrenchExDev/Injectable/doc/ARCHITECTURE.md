# Injectable — Architecture

## Overview

Injectable is an attribute-driven source generator system that shifts DI lifetime responsibility from wiring code to the service class itself. Developers declare `[Injectable(Scope = Scope.Singleton)]` on their classes or interfaces, and source generators produce container-specific registration extension methods at compile time. When placed on an interface, all implementations are auto-registered with the declared scope — no attribute needed on the class.

## Design Principles

1. **Developer-owned scoping** — the class author decides the lifetime, not the DI bootstrapper
2. **Zero runtime overhead** — all registration code is generated at compile time
3. **Container-agnostic attribute** — one attribute works with any DI container via separate SG projects
4. **Reusable emission** — the `SourceGenerator.Lib` project holds models and emitters without Roslyn dependencies
5. **Compile-time safety** — Roslyn analyzers catch captive dependencies, abstract-class mistakes, and missing registrations

## Project Decomposition

```
Attributes (netstandard2.0 + net10.0)
    └── InjectableAttribute, InjectableDefaultsAttribute, InjectableDecoratorAttribute, Scope enum

SourceGenerator.Lib (netstandard2.0, no Roslyn)
    └── InjectableEmitModel, InjectableServiceModel, InjectableDecoratorModel, InterfaceContractModel, InjectableEmitter

Microsoft.SourceGenerator (netstandard2.0, Roslyn)
    └── MicrosoftInjectableGenerator : IIncrementalGenerator
    └── depends on: SourceGenerator.Lib (linked source)

DryIoc.SourceGenerator (netstandard2.0, Roslyn)
    └── DryIocInjectableGenerator : IIncrementalGenerator
    └── depends on: SourceGenerator.Lib (linked source)

Analyzers (netstandard2.0, Roslyn)
    └── CaptiveDependencyAnalyzer (INJECT001)
    └── AbstractClassDecoratedAnalyzer (INJECT002)
    └── MissingInjectableAnalyzer (INJECT003)
    └── InterfaceScopeMismatchAnalyzer (INJECT004)
```

## Source Generator Pipeline

1. **Pipeline 1 — Classes**: `ForAttributeWithMetadataName` scans for `[Injectable]`-decorated classes (supports `AllowMultiple`). Each class → one or more `InjectableServiceModel` instances.
2. **Pipeline 2 — Interfaces**: `ForAttributeWithMetadataName` scans for `[Injectable]`-decorated interfaces. Each interface → `InterfaceContractModel` (scope, key, tryAdd). The generator then walks the assembly's type tree to find all non-abstract classes implementing the interface and auto-registers them with the contract's scope.
3. **Pipeline 3 — Decorators**: `[InjectableDecorator]` classes → `InjectableDecoratorModel` instances.
4. Assembly-level `[InjectableDefaults]` is read to determine default scope.
5. All models are collected. Assembly defaults applied where scope was not explicitly set. Interface contracts skip classes already explicitly registered.
6. The emitter produces a single extension method file per assembly.
7. Output: `InjectableExtensions.g.cs`

## Registration Modes

| Mode | Attribute | Generated Code (Microsoft DI) |
|------|-----------|-------------------------------|
| Standard | `[Injectable]` | `services.AddTransient<IFoo, Foo>()` |
| TryAdd | `[Injectable(TryAdd = true)]` | `services.TryAddTransient<IFoo, Foo>()` |
| Keyed (.NET 8+) | `[Injectable(Key = "primary")]` | `services.AddKeyedSingleton<IFoo, Foo>("primary")` |
| Open generic | `[Injectable]` on `Repo<T> : IRepo<T>` | `services.AddTransient(typeof(IRepo<>), typeof(Repo<>))` |
| Decorator | `[InjectableDecorator(typeof(IFoo))]` | Service replacement pattern / `Setup.Decorator` |
| Interface contract | `[Injectable]` on interface | Implementations auto-registered with interface's scope |

## Interface Resolution

- If `As` is explicitly set → register only as that type
- If no `As` → register for each directly implemented interface
- If no interfaces → register as self (concrete type only)
- Abstract classes are skipped (with INJECT002 warning if decorated)
- Open generics produce unbound `typeof()` registrations

## Analyzers

| ID | Severity | Description |
|----|----------|-------------|
| INJECT001 | Warning | Captive dependency: Singleton depends on Scoped/Transient service |
| INJECT002 | Warning | `[Injectable]` on abstract class has no effect |
| INJECT003 | Info | Class implements interface but is not decorated with `[Injectable]` |
| INJECT004 | Error | Implementation scope violates interface `[Injectable]` contract |

## Adding a New DI Container

1. Create `FrenchExDev.Net.Injectable.{Container}.SourceGenerator` project (netstandard2.0, Roslyn)
2. Add `Emit{Container}` method to `InjectableEmitter`
3. Implement `IIncrementalGenerator` following the existing pattern
4. Link the `SourceGenerator.Lib` source files (not reference — avoids runtime Roslyn dependency)
5. Add test project with fixture classes
