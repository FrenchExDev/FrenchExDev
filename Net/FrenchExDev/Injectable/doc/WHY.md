# Injectable — Why

## The developer knows best

The developer who writes a service class knows its scoping requirements. Writing a singleton is not the same as writing a transient service — thread safety, state management, resource disposal all differ fundamentally. The author of the class is the one who made those design decisions. They should declare the lifetime.

## DI wiring is blind

Traditional DI wiring happens far from the service implementation, often in a startup file or a composition root. The person writing the wiring has to guess — or go read the source — to decide whether a service should be singleton, scoped, or transient. That guessing is a bug waiting to happen.

Worse, some codebases turn wiring into elaborate escape games: trees of static helper classes, extension method chains, module registrations scattered across assemblies. All of this hand-written plumbing exists to answer a question the service author already knew the answer to.

## Declare, don't wire

With `[Injectable(Scope = Scope.Singleton)]`, the lifetime lives where it belongs — on the class itself. Source generators handle the rest. No hand-written wiring, no guessing, no drift between intent and registration.

```csharp
[Injectable(Scope = Scope.Singleton)]
public class CacheService : ICacheService { ... }

[Injectable(Scope = Scope.Scoped)]
public class RequestContext : IRequestContext { ... }

[Injectable]
public class EmailSender : IEmailSender { ... }
```

One line. The developer decides. The compiler wires.

## The interface owns the contract

Sometimes the scope decision isn't per-class — it's per-interface. Every implementation of `IRequestContext` must be scoped. Every implementation of `ICache` must be a singleton. Putting `[Injectable]` on the interface declares that contract:

```csharp
[Injectable(Scope = Scope.Scoped)]
public interface IRequestContext { }

// No [Injectable] needed — scope is inherited from the interface
public class RequestContext : IRequestContext { }
public class TestRequestContext : IRequestContext { }
```

Implementations are auto-registered. If a class explicitly sets a different scope, the compiler raises INJECT004 — an error, not a warning, because violating the interface contract is always a bug.

## What else the developer knows

The developer also knows:

- **Whether the service is a default** that consumers can override → `TryAdd = true`
- **Whether the service is a named variant** of an interface → `Key = "primary"`
- **Whether the service is a generic repository** that should be registered open → automatic open generic detection
- **Whether a decorator wraps another service** → `[InjectableDecorator(typeof(IService))]`
- **Whether the whole assembly follows a convention** → `[assembly: InjectableDefaults(Scope = Scope.Scoped)]`

None of these decisions belong in a startup file. They all belong where the code is.

## Catch mistakes at compile time

The compiler already knows every service's scope. Injectable leverages this with Roslyn analyzers:

- **INJECT001**: A Singleton depends on a Scoped service? That's a captive dependency — flagged at build time, not discovered in production.
- **INJECT002**: You put `[Injectable]` on an abstract class? The generator silently skips it — now you get a warning.
- **INJECT003**: A class implements `IMyService` but isn't registered? Maybe intentional, maybe an oversight — the analyzer gives you a nudge.
- **INJECT004**: A class sets `Scope = Singleton` but its interface declares `Scope = Scoped`? That's a contract violation — build error.

The goal: **the developer decides, the compiler wires, and the compiler checks**.
