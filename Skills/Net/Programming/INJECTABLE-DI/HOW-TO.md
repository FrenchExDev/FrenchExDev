# INJECTABLE-DI — How-To

## 1. Quick Start

```csharp
using FrenchExDev.Net.Injectable.Attributes;

[Injectable(Scope = Scope.Singleton)]
public class CacheService : ICacheService { ... }

[Injectable(Scope = Scope.Scoped)]
public class RequestContext : IRequestContext { ... }

[Injectable]   // Transient by default
public class EmailSender : IEmailSender { ... }
```

At startup:

```csharp
// Microsoft DI
services.AddMyAssemblyInjectables();

// DryIoc
container.AddMyAssemblyInjectables();
```

The method name derives from the assembly name (e.g. `MyApp.Services` → `AddMyAppServicesInjectables`).

## 2. Choosing The Scope

| Use case | Scope |
|---|---|
| Stateless, thread-safe utility | `Scope.Singleton` |
| Per-request state (HTTP, GraphQL operation) | `Scope.Scoped` |
| Stateful, short-lived, or per-call | `Scope.Transient` |

If unsure, use `Scope.Transient` — it's safest. Promote to `Scoped` or `Singleton` only when you have a reason.

## 3. Interface Resolution

```csharp
// No As → registers for IMyService and IOther
[Injectable(Scope = Scope.Scoped)]
public class MyService : IMyService, IOther { }

// As specified → registers only as IMyService (IOther ignored)
[Injectable(Scope = Scope.Singleton, As = typeof(IMyService))]
public class MyService : IMyService, IOther { }

// No interfaces → registers as self
[Injectable]
public class BackgroundJob { }
```

## 4. Interface Scope Contracts

When ALL implementations of an interface must use the same scope, declare it on the interface:

```csharp
[Injectable(Scope = Scope.Scoped)]
public interface IRequestContext { }

// Auto-registered as Scoped — no [Injectable] on the class
public class RequestContext : IRequestContext { }
public class TestRequestContext : IRequestContext { }
```

If a class violates the contract, INJECT004 raises a build error:

```csharp
[Injectable(Scope = Scope.Scoped)]
public interface IRequestContext { }

[Injectable(Scope = Scope.Singleton)]   // INJECT004 ERROR
public class BadContext : IRequestContext { }
```

## 5. AllowMultiple — Split Registrations

Apply `[Injectable]` multiple times to register one class under multiple interfaces or keys:

```csharp
[Injectable(Scope = Scope.Singleton, As = typeof(IReader))]
[Injectable(Scope = Scope.Singleton, As = typeof(IWriter))]
public class FileStore : IReader, IWriter { }
```

## 6. TryAdd — Default-That-Can-Be-Overridden

```csharp
[Injectable(Scope = Scope.Singleton, TryAdd = true)]
public class DefaultCache : ICache { }
```

Generates `TryAddSingleton<ICache, DefaultCache>()` — only registers if `ICache` isn't already registered.

## 7. Keyed Services (.NET 8+)

```csharp
[Injectable(Scope = Scope.Singleton, Key = "primary")]
public class PrimaryDb : IDatabase { }

[Injectable(Scope = Scope.Singleton, Key = "readonly")]
public class ReadOnlyDb : IDatabase { }
```

Generates `AddKeyedSingleton<IDatabase, PrimaryDb>("primary")` inside `#if NET8_0_OR_GREATER`.

## 8. Open Generics

```csharp
[Injectable(Scope = Scope.Scoped)]
public class Repository<T> : IRepository<T> where T : class { }
```

Generates `services.AddScoped(typeof(IRepository<>), typeof(Repository<>))` — open-generic registration.

## 9. Assembly-Level Defaults

```csharp
using FrenchExDev.Net.Injectable.Attributes;

[assembly: InjectableDefaults(Scope = Scope.Scoped)]
```

All `[Injectable]` classes without an explicit `Scope` will use `Scoped`. Classes that explicitly set `Scope` are unaffected.

## 10. Decorators

Wrap an existing service with cross-cutting concerns:

```csharp
[InjectableDecorator(typeof(IMyService))]
public sealed class LoggingMyService : IMyService
{
    private readonly IMyService _inner;
    public LoggingMyService(IMyService inner) => _inner = inner;
    // delegates to _inner with logging
}
```

Multiple decorators apply in `Order` (lower = innermost):

```csharp
[InjectableDecorator(typeof(IMyService), Order = 0)]
public sealed class ValidationDecorator : IMyService { }

[InjectableDecorator(typeof(IMyService), Order = 1)]
public sealed class LoggingDecorator : IMyService { }
```

DryIoc uses native `Setup.Decorator`. Microsoft DI uses a service replacement pattern with `ActivatorUtilities`.

## 11. Heeding The Analyzers

| ID | Action |
|---|---|
| INJECT001 (captive dependency) | Promote the inner service to Singleton, OR demote the outer service from Singleton |
| INJECT002 (abstract class decorated) | Remove `[Injectable]` from the abstract class — decorate concrete classes instead |
| INJECT003 (missing `[Injectable]`) | Add `[Injectable]` if the class should be registered, or suppress if intentional |
| INJECT004 (interface contract violation) | Match the implementation scope to the interface, or remove `[Injectable]` from the class to inherit |

## What NOT To Do

- **Don't decorate abstract classes with `[Injectable]`.** It has no effect — INJECT002 warns.
- **Don't make a Singleton depend on a Scoped service.** That's a captive dependency — INJECT001 warns.
- **Don't hand-write registration extensions for `[Injectable]`-decorated classes.** The generator handles them.
- **Don't put `[Injectable]` on classes the consumer should construct manually** (entities, DTOs, value objects).
- **Don't fight the analyzers.** They catch real bugs.

## Anchor Package

[`Net/FrenchExDev/Injectable/`](../../../Net/FrenchExDev/Injectable/) — implementation reference.
