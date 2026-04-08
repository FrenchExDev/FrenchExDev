# BUILDER-PATTERN — Philosophy

This is the async-builder pattern as practiced in this codebase. Use it whenever object construction crosses any of these lines: it touches I/O, it must validate before allocating resources, the object graph has back-references, or two callers may race for the same instance.

## Construction Is Not a Constructor

C# constructors are synchronous, throw on the first bad argument, and have no notion of "the same instance for two callers". Real domain construction needs the opposite of all three:

1. **Async** — fetch related entities, call services, generate files
2. **Validate-then-build** — accumulate every error in one pass, then build only if clean
3. **Single-flight** — N concurrent callers receive the same instance, built exactly once

A `Builder<T>` is the smallest abstraction that gives you all three. It is not a "fluent setter chain" — that is a side effect, not the point.

## Validation Is Separated From Construction

```
ValidateAsync()  →  if OK  →  Instantiate()
```

Validation runs **first** and runs **completely**. It accumulates every error. Only on a clean validation pass does construction begin.

Consequences:

- `Instantiate` / `CreateInstance` can trust their inputs unconditionally — no null checks, no defensive guards inside construction logic.
- Callers receive a complete error report in one round trip — they fix everything at once instead of one error per build attempt.
- Construction is the expensive phase. Failing fast in validation never spends I/O on a doomed build.

This is the inverse of `throw new ArgumentNullException` on the first bad argument. That pattern punishes the caller; validation accumulation respects them.

## Failure Is a Value

`Exception` is not abandoned — it is promoted. An exception can play two roles:

```
Throw role:    throw new CustomerNotFoundException(...)
               ↑ unwinds the stack, requires a catch site

Value role:    Result<Order, CustomerNotFoundException>.Failure(...)
               ↑ flows as data, caller reads .Error, no stack unwind
```

Throw when something is genuinely unexpected (programming error, OOM, unrecoverable state). Return as a value when the outcome is a known business condition the caller must handle (not found, conflict, quota exceeded, invalid input).

The two builder modes encode this distinction in the type system:

- `[Builder]` → `Task<Result<Reference<T>>>` — validation accumulator, no domain failure type
- `[Builder(Exception = typeof(E))]` → `Task<Result<T, E>>` — typed domain failure in the signature

The typed-failure return makes the failure path **part of the API**. Reading the signature is enough to know what can go wrong; you do not need to grep for `throw` statements.

## Circular Graphs Are a First-Class Concern

Most builder frameworks break on `Person → Child → Parent` back-references. They infinite-loop or stack-overflow. This pattern does not.

The mechanism is `Reference<T>` + `VisitedObjects`:

- `Reference<T>` is a one-shot resolvable cell. The builder allocates it before the value exists. Nested builders see a `Reference<T>` they can pass around even though the underlying value is still being constructed.
- `VisitedObjects` is a `ConcurrentDictionary` keyed by **builder instance** (reference equality). When a nested builder calls back into a parent that is already in flight, `IsVisited` returns `true` and the in-flight `Reference<T>` is returned immediately.

The protocol the developer follows in manual graph builders is exactly two rules:

1. Call `reference.Resolve(instance)` **immediately after creating the instance**, before building any nested objects.
2. Pass `visitedObjects` into every nested `BuildAsync` call.

That is the entire cycle protocol. It is not optional. It is the only way circular graphs can build deterministically.

The source generator does not handle graphs — graphs require explicit control over when `Resolve` is called, and that is a per-domain decision. The generator handles the boring 90%; you write graph builders by hand for the interesting 10%.

## Single-Flight Is Automatic, Not Optional

A builder instance is a unit of work. The first caller wins the semaphore, runs validation and construction, then resolves the cached `Reference<T>`. Every subsequent caller — concurrent or sequential — receives the same cached reference without re-running anything.

This is **not** an optimization. It is a correctness guarantee for graphs. If two builders both depend on the same parent node, they must receive the same instance, not two separate copies. Without single-flight, you cannot build a graph correctly.

The implementation is `SemaphoreSlim(1, 1)` plus a double-check inside the lock. The double-check is critical: by the time you acquire the semaphore, another caller may have already resolved the reference, and you must not re-validate.

## The Source Generator Removes Boilerplate, Not Control

The generator writes the parts that are mechanical:

- Protected input properties (`protected T? Prop { get; private set; }`)
- Public `With{Prop}(value)` fluent methods
- One `virtual Validate{Prop}(value)` per scalar property
- One `virtual Validate{Prop}Item(item, index)` per collection property
- A `ValidateAsync` override that calls every hook and accumulates errors with `MemberName($"Prop[{i}]", builderType)`
- A `BuildException` (Mode 1) or `TypedBuildException` (Mode 2) override
- A **sealed** `Instantiate` bridge that calls `CreateInstance()`
- A `CreateInstance()` strategy implementation (`init` / `ctor` / `factory:Name` / `custom`)

Everything generated is `virtual` (or `abstract` in `custom` mode). Override the validation hooks to add domain rules. Override `CreateInstance` if the strategy doesn't fit. Override `TypedBuildException` to translate errors. The generator gives you a working baseline; you mutate it as needed.

What the generator deliberately **does not** hide:

- The two-mode distinction. `Mode 1` and `Mode 2` are different contracts and you opt into them explicitly.
- The graph protocol. If you want graphs, you write a manual builder so you control `Resolve` timing.
- Validation logic. The hooks are empty by default — every domain rule is your code.

## Instantiation Strategies — Open/Closed at the Construction Site

The four strategies cover every common construction pattern without forking the emitter:

| Strategy | When |
|---|---|
| `init` (default) | Target has parameterless ctor + `init`/settable properties. Object initializer expression. |
| `ctor` | Target uses constructor injection. Builder properties pass in declaration order. |
| `factory:Name` | Target exposes a static factory. Calls `T.Name(prop1, prop2, ...)`. |
| `custom` | Construction is non-trivial. Generator emits `protected abstract T CreateInstance()`; you implement it. |

This is OCP at the construction site. Adding a new strategy means a new branch in `BuilderEmitter`, not new attributes or new base classes.

## Trade-offs Accepted

| Trade-off | Decision |
|---|---|
| Mode 2 developers must implement `InstantiateAsync` | Construction with typed errors is application-specific by definition. |
| Manual graph builders need more code | Full control of `Resolve` timing is required. |
| `Reference<T>` adds a layer of indirection | Required to single-flight before the value exists. |
| Generator hides `Reference<T>` from Mode 1 | Most builders never need to see it; expose only what's needed. |
| `TypedBuildException` instead of covariant override | `netstandard2.0` doesn't support covariant returns. |

## What This Pattern Is Not

- **Not a DI container.** It produces one specific object per invocation. Lifetimes, scopes, registrations are out of scope.
- **Not a mapper.** Properties are explicitly set by the caller, not auto-mapped from a DTO.
- **Not a test fixture factory.** Builders are useful in tests, but they are designed for production — thread safety, validation, and async construction are production concerns.
- **Not a constructor replacement for trivial types.** If your type has three nullable strings and no I/O, just use a constructor.

Use a builder when at least one of {async, validation, graph, single-flight} is required. Use a constructor otherwise.
