# Injectable — How To

## Quick Start

Decorate your classes with `[Injectable]` to declare their DI lifetime:

```csharp
using FrenchExDev.Net.Injectable.Attributes;

[Injectable(Scope = Scope.Singleton)]
public class MyService : IMyService
{
    // ...
}
```

The source generator produces an extension method to register all decorated classes at once.

### Microsoft DI

```csharp
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddMyAssemblyInjectables();
```

### DryIoc

```csharp
using DryIoc;

var container = new Container();
container.AddMyAssemblyInjectables();
```

## Scope

| Value | Microsoft DI | DryIoc |
|-------|-------------|--------|
| `Scope.Transient` (default) | `AddTransient` | `Reuse.Transient` |
| `Scope.Scoped` | `AddScoped` | `Reuse.ScopedOrSingleton` |
| `Scope.Singleton` | `AddSingleton` | `Reuse.Singleton` |

## Interface Resolution

- **No `As` specified**: registers for each directly implemented interface. If the class implements no interfaces, registers as self.
- **`As` specified**: registers only as the specified type.

```csharp
// Registers as IMyService only (IOther is ignored)
[Injectable(Scope = Scope.Singleton, As = typeof(IMyService))]
public class MyService : IMyService, IOther { }

// Registers as both IFirst and ISecond
[Injectable(Scope = Scope.Scoped)]
public class MyService : IFirst, ISecond { }

// Registers as self (no interfaces)
[Injectable]
public class MyService { }
```

## Interface Contracts — Scope Inheritance

When `[Injectable]` is placed on an interface, all implementations are auto-registered with the interface's scope. No `[Injectable]` needed on the class:

```csharp
[Injectable(Scope = Scope.Scoped)]
public interface IRequestContext { }

// Auto-registered as Scoped — no attribute needed
public class RequestContext : IRequestContext { }
public class TestRequestContext : IRequestContext { }
```

This is the recommended approach for service contracts where the scope is a design constraint of the interface, not an implementation detail.

If a class explicitly sets `[Injectable]` with a **different** scope than its interface, analyzer INJECT004 reports an error:

```csharp
[Injectable(Scope = Scope.Scoped)]
public interface IRequestContext { }

// INJECT004 error: scope Singleton violates interface contract Scoped
[Injectable(Scope = Scope.Singleton)]
public class RequestContext : IRequestContext { }
```

A class can still add `[Injectable]` with the **same** scope (e.g. to set `Key` or `TryAdd`), or omit it entirely to inherit.

## AllowMultiple — Split Registrations

The attribute supports `AllowMultiple`, so you can apply it multiple times to register a class under different interfaces or keys:

```csharp
[Injectable(Scope = Scope.Singleton, As = typeof(IReader))]
[Injectable(Scope = Scope.Singleton, As = typeof(IWriter))]
public class FileStore : IReader, IWriter { }
```

## TryAdd — Avoid Duplicate Registrations

Use `TryAdd = true` when providing defaults that consumers can override:

```csharp
[Injectable(Scope = Scope.Singleton, TryAdd = true)]
public class DefaultCache : ICache { }
```

Generates `TryAddSingleton<ICache, DefaultCache>()` — only registers if `ICache` isn't already registered.

## Keyed Services (.NET 8+)

Register named instances with the `Key` property:

```csharp
[Injectable(Scope = Scope.Singleton, Key = "primary")]
public class PrimaryDb : IDatabase { }

[Injectable(Scope = Scope.Singleton, Key = "readonly")]
public class ReadOnlyDb : IDatabase { }
```

Generates `AddKeyedSingleton<IDatabase, PrimaryDb>("primary")` inside `#if NET8_0_OR_GREATER`.

## Open Generics

Open generic classes are detected automatically:

```csharp
[Injectable(Scope = Scope.Scoped)]
public class Repository<T> : IRepository<T> where T : class { }
```

Generates `services.AddScoped(typeof(IRepository<>), typeof(Repository<>))`.

## Assembly-Level Defaults

Set a default scope for the entire assembly:

```csharp
using FrenchExDev.Net.Injectable.Attributes;

[assembly: InjectableDefaults(Scope = Scope.Scoped)]
```

All `[Injectable]` classes without an explicit `Scope` will use `Scoped` instead of the default `Transient`. Classes that explicitly set `Scope` are unaffected.

## Decorators

Wrap an existing service with cross-cutting concerns:

```csharp
[InjectableDecorator(typeof(IMyService))]
public class LoggingMyService : IMyService
{
    private readonly IMyService _inner;
    public LoggingMyService(IMyService inner) => _inner = inner;
}
```

Multiple decorators on the same interface are applied in `Order` sequence (lower = innermost):

```csharp
[InjectableDecorator(typeof(IMyService), Order = 0)]
public class ValidationDecorator : IMyService { }

[InjectableDecorator(typeof(IMyService), Order = 1)]
public class LoggingDecorator : IMyService { }
```

DryIoc uses native `Setup.Decorator`. Microsoft DI uses a service replacement pattern with `ActivatorUtilities`.

## Analyzers

Injectable ships with Roslyn analyzers that catch common DI mistakes at compile time:

| ID | Severity | What It Catches |
|----|----------|----------------|
| INJECT001 | Warning | **Captive dependency** — a Singleton depending on a Scoped or Transient service |
| INJECT002 | Warning | `[Injectable]` on an abstract class (silently skipped by the generator) |
| INJECT003 | Info | A class implements a service interface but has no `[Injectable]` attribute |
| INJECT004 | Error | Implementation scope violates interface `[Injectable]` scope contract |

## Projects

| Project | Purpose |
|---------|---------|
| `FrenchExDev.Net.Injectable.Attributes` | `[Injectable]`, `[InjectableDefaults]`, `[InjectableDecorator]` attributes + `Scope` enum |
| `FrenchExDev.Net.Injectable.SourceGenerator.Lib` | Shared models + code emitters (no Roslyn dependency) |
| `FrenchExDev.Net.Injectable.Microsoft.SourceGenerator` | Source generator for Microsoft.Extensions.DependencyInjection |
| `FrenchExDev.Net.Injectable.DryIoc.SourceGenerator` | Source generator for DryIoc |
| `FrenchExDev.Net.Injectable.Analyzers` | Roslyn analyzers (INJECT001–INJECT004) |
