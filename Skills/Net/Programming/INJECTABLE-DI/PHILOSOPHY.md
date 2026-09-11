# INJECTABLE-DI — Philosophy

The developer who writes a service class knows its scoping requirements. The DI registration code, written far from the implementation, has to **guess**. `[Injectable]` moves the lifetime decision onto the class itself, where it belongs, and a source generator emits the wiring at compile time.

## The Developer Knows Best

Writing a singleton is not the same as writing a transient service — thread safety, state management, resource disposal all differ fundamentally. The author of the class is the one who made those design decisions. **They should declare the lifetime.**

Traditional DI wiring happens far from the implementation: a startup file, a composition root, a chain of `IServiceCollection` extension methods. The person writing the wiring has to guess — or go read the source — to decide whether a service should be singleton, scoped, or transient. That guessing is a bug waiting to happen.

Worse, some codebases turn wiring into elaborate escape games: trees of static helper classes, extension method chains, module registrations scattered across assemblies. All of this hand-written plumbing exists to answer a question the service author already knew the answer to.

## Declare, Don't Wire

```csharp
[Injectable(Scope = Scope.Singleton)]
public class CacheService : ICacheService { ... }

[Injectable(Scope = Scope.Scoped)]
public class RequestContext : IRequestContext { ... }

[Injectable]   // Transient by default
public class EmailSender : IEmailSender { ... }
```

One line. The developer decides. The compiler wires.

A source generator scans for `[Injectable]`-decorated types at compile time and emits a single extension method per assembly:

```csharp
services.AddMyAssemblyInjectables();
```

This is one call at startup, no manual registration, no drift between intent and wiring.

## Container-Agnostic Attribute, Container-Specific Generator

The `[Injectable]` attribute is **container-agnostic** — it lives in `Injectable.Attributes`, which has zero dependencies on any specific DI container. It records developer intent (`Scope`, `As`, `TryAdd`, `Key`).

Separate source generators read this attribute and emit container-specific code:

- `Injectable.Microsoft.SourceGenerator` → `services.AddSingleton<IFoo, Foo>()`
- `Injectable.DryIoc.SourceGenerator` → `container.Register<IFoo, Foo>(Reuse.Singleton)`

Adding a new container is a new generator project, not a fork of the attribute or the application code.

## Zero Runtime Overhead

All registration is generated at compile time. There is **no reflection**, **no assembly scanning**, **no startup-time discovery**. The generated code is plain `services.AddXxx<T, TImpl>()` calls, identical to what a developer would have written by hand. Visible in the IDE, debuggable, AOT-compatible.

## The Interface Owns The Contract

Sometimes the scope decision isn't per-class — it's per-interface. Every implementation of `IRequestContext` must be scoped. Every implementation of `ICache` must be a singleton. Putting `[Injectable]` on the **interface** declares that contract:

```csharp
[Injectable(Scope = Scope.Scoped)]
public interface IRequestContext { }

// No [Injectable] needed — scope is inherited from the interface
public class RequestContext : IRequestContext { }
public class TestRequestContext : IRequestContext { }
```

Implementations are auto-registered. If a class explicitly sets a different scope than its interface declares, that's a contract violation — flagged as a build error by an analyzer (not a warning, because it's always a bug).

## Compile-Time Safety Via Analyzers

The compiler already knows every service's scope. Analyzers leverage this:

- **Captive dependency** (Singleton depending on Scoped) → warning at build time, not in production.
- **`[Injectable]` on an abstract class** → warning (the generator silently skips it).
- **Class implements interface but isn't decorated** → info hint.
- **Implementation scope violates interface contract** → build error.

## Reusable Emission

The emission logic — converting `InjectableServiceModel` instances into C# source — lives in a `SourceGenerator.Lib` project that has **no Roslyn dependency**. Both the Microsoft and DryIoc generators link the same Lib source files. Adding a new container means writing one new `Emit{Container}()` method, not duplicating the whole emission pipeline.

This is the same pattern as `Builder.SourceGenerator.Lib` and `Mapper.SourceGenerator.Lib` — a hub of reusable, Roslyn-free emission logic.

## Anchor Package

[`Net/FrenchExDev/Injectable/`](../../../Net/FrenchExDev/Injectable/) — `[Injectable]` attribute, `Scope` enum, source generators for Microsoft DI and DryIoc, four Roslyn analyzers.
