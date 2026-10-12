# BUILDER-PATTERN — Architecture

The async builder pattern is implemented as a thin runtime base class plus a source generator. This file describes the moving parts and how they fit together.

## Package Layout

```
Builder/
├── FrenchExDev.Net.Builder                       runtime: AbstractBuilder<T>, Reference<T>, VisitedObjects, ValidationResult
├── FrenchExDev.Net.Builder.Attributes            [Builder] attribute (netstandard2.0;net10.0)
├── FrenchExDev.Net.Builder.SourceGenerator       Roslyn IIncrementalGenerator (netstandard2.0)
├── FrenchExDev.Net.Builder.SourceGenerator.Lib   string-based emitter, no Roslyn dependency (netstandard2.0)
└── FrenchExDev.Net.Builder.Testing               test helpers
```

The `.Lib` split is the open/closed seam. Other source generators (BinaryWrapper, ComposeBundle, GitLab.DockerCompose, Diem) reuse `BuilderEmitter` to emit domain-specific builders without forking the implementation.

## Runtime Types

### `AbstractBuilder<T>` — Mode 1

The base class every Mode 1 builder inherits from. Sealed, generic over the target type.

State:
- `_reference : Reference<T>` — the cached one-shot resolvable cell
- `_buildLock : SemaphoreSlim(1, 1)` — single-flight gate

Public surface:
- `Task<Result<Reference<T>>> BuildAsync(VisitedObjects? visited = null, CancellationToken ct = default)`

Algorithm inside `BuildAsync`:

```
1. Get or create VisitedObjects.
2. If VisitedObjects.IsVisited(this, this) → return cached _reference.
3. If _reference.IsResolved → return _reference.
4. Acquire _buildLock.
5. Re-check IsResolved (double-check inside the lock).
6. ValidateAsync().
7. If validation failed → throw BuildException(validationResult).
8. Instantiate(_reference, visited, ct) — sealed bridge from generated code.
9. Release _buildLock.
10. Return Result<Reference<T>>.Success(_reference).
```

The double-check at step 5 is mandatory: between step 3 and step 4, another caller may have completed the build. Skipping the double-check causes redundant validation and breaks single-flight semantics.

### `AbstractBuilder<T, TException>` — Mode 2

Extends `AbstractBuilder<T>`. Adds:

- `protected abstract Task<Result<T, TException>> InstantiateAsync(CancellationToken ct)` — developer must implement
- `protected sealed override Exception BuildException(...)` → delegates to `protected virtual TypedBuildException(...)` returning `TException`
- Public `Task<Result<T, TException>> BuildAsync(CancellationToken ct)` — wraps the base class to translate the result type

Why two methods? `BuildException` is sealed because the base class needs to call it polymorphically with covariance, but `netstandard2.0` does not support covariant return types. `TypedBuildException` is the developer-facing override point.

### `Reference<T>`

A one-shot, thread-safe resolvable cell. Three operations:

- `IsResolved : bool` — has the cell been filled?
- `Resolve(T value)` — fill the cell. Throws if already resolved.
- `Resolved() : T` — read the value. Throws if not yet resolved.

Used both internally (the builder allocates one before construction) and externally (callers receive `Result<Reference<T>>` and call `.Resolved()`).

### `VisitedObjects`

A `ConcurrentDictionary<object, object>` keyed by **reference equality** (`ReferenceEqualityComparer.Instance`).

The key is the **builder instance** (`this`), not the built value. This is critical: at the time of cycle detection the value does not yet exist, so reference equality on builders is the only thing available.

API:
- `bool IsVisited(object key, object owner)` — register and report
- `IsVisited` returns `true` on subsequent calls with the same key

When passed to `BuildAsync`, the same `VisitedObjects` instance flows through the entire object graph build. Every nested builder shares the same dictionary.

### `ValidationResult`

A `ConcurrentDictionary<MemberName, ConcurrentQueue<Exception>>`. Methods:

- `AddError(MemberName name, Exception ex)` — append to the queue
- `IsValid : bool`
- `ToDataAnnotationsValidationResult()` — interop bridge to `System.ComponentModel.DataAnnotations`

`MemberName` carries `(Name, OwnerType)`. For collection items the name is `"PropName[3]"`. For nested builders it is `"OwnerType.PropName"`.

`ValidationResult.MemberNames` is **never null** — always `Enumerable.Empty<string>()` at minimum.

## Source Generator

### Pipeline

`BuilderGenerator : IIncrementalGenerator` does:

1. **Syntax filter** — find class declarations with attribute lists.
2. **Symbol filter** — keep only those decorated with `[Builder]` (full name match `FrenchExDev.Net.Builder.Attributes.BuilderAttribute`).
3. **Property extraction** — read each declared property, classify as scalar / collection / dictionary, capture nullable annotation, type symbol, attributes.
4. **Model assembly** — populate a `BuilderEmitModel` with `BuilderPropertyModel` items.
5. **Emission** — call `BuilderEmitter.Emit(model)` from the `.Lib` package.
6. **AddSource** — write `{ClassName}.g.cs` into the compilation.

The split is critical: the generator project depends on Roslyn; the `.Lib` project depends on nothing. Anyone (other generators, unit tests) can call `BuilderEmitter` directly with a hand-built `BuilderEmitModel`.

### What Gets Emitted

For `[Builder] public partial class FooBuilder` targeting `Foo` with properties `{Name : string?, Items : List<int>}`:

```csharp
partial class FooBuilder : AbstractBuilder<Foo>
{
    // input properties
    protected string? Name { get; private set; }
    public FooBuilder WithName(string? value) { Name = value; return this; }

    protected List<int>? Items { get; private set; }
    public FooBuilder WithItems(List<int>? value) { Items = value; return this; }

    // virtual validation hooks (empty default)
    protected virtual IEnumerable<Exception>? ValidateName(string? value) => null;
    protected virtual IEnumerable<Exception>? ValidateItems(List<int>? value) => null;
    protected virtual IEnumerable<Exception>? ValidateItemsItem(int item, int index) => null;

    // ValidateAsync override — calls every hook, accumulates by MemberName
    protected override Task<Result<ValidationResult>> ValidateAsync(CancellationToken ct = default) { ... }

    // BuildException override
    protected override Exception BuildException(Result<ValidationResult> r) { ... }

    // sealed bridge — calls CreateInstance
    protected sealed override async Task<Result<Reference<Foo>>> Instantiate(
        Reference<Foo> reference, VisitedObjects visited, CancellationToken ct)
    {
        var v = CreateInstance();
        reference.Resolve(v);
        return Result<Reference<Foo>>.Success(reference);
    }

    // strategy-driven creator (init by default)
    protected virtual Foo CreateInstance() => new Foo { Name = Name, Items = Items };
}
```

For Mode 2, the bridge instead calls `await InstantiateAsync(ct)`, which the developer implements.

### Dictionary Properties

When a property is `Dictionary<K, V>`, the generator emits a `WithProp(Action<DictionaryBuilder<K, V>> configure)` overload. When the value type `V` has a known builder `VBuilder`, the generator emits the deferred-build variant `Action<DictionaryBuilder<K, V, VBuilder>>` plus a single-entry `WithKey(string key, Action<VBuilder>)` overload.

Deferred-build dictionaries hold builder instances and run them during the parent's `Instantiate` phase, threading `visitedObjects` and `cancellationToken` through.

## Extension Seams in `.Lib`

`BuilderEmitter` exposes three escape hatches that downstream generators use:

| Field | Purpose |
|---|---|
| `BuilderEmitModel.Preamble` | Raw C# block injected after the class opening brace, before input properties. Used for fields, constructors, nested types. |
| `BuilderPropertyModel.WithMethodAttributes` | Attribute lines emitted before each `With*` method (e.g. `[SinceVersion("2.0")]`). |
| `BuilderPropertyModel.WithMethodBodyPrefix` | Raw C# lines at the start of `With*` body (e.g. `EnsureNotBuilt();`). |
| `BuilderPropertyModel.InstantiationExpression` | Custom expression substituted for the property name in `CreateInstance` (e.g. `"Tags?.AsReadOnly()"`). |

These are the only customisation points. Anything more invasive should be done by emitting your own builder class outside the emitter.

## Concurrency Model

| Aspect | Mechanism |
|---|---|
| Single-flight per builder | `SemaphoreSlim(1, 1)` + double-check pattern |
| Cycle detection across builders | `VisitedObjects.IsVisited(this, this)` |
| Reference equality on builders | `ReferenceEqualityComparer.Instance` |
| Validation error accumulation | `ConcurrentDictionary<MemberName, ConcurrentQueue<Exception>>` |
| Reference fill | `Reference<T>` is one-shot; second `Resolve` throws |

The combination guarantees: (a) any builder builds its target exactly once, (b) any cycle terminates without infinite recursion, (c) any validation error reaches the caller without being lost to a race.
