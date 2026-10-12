# Builder -- Architecture

## Layer map

```
+----------------------------------------------------------------+
|  Developer code                                                 |
|  -----------------------------------------------------------   |
|  [Builder]                    [Builder(Exception = typeof(E))]  |
|  public partial class         public partial class              |
|    OrderBuilder                 InvoiceBuilder                  |
|  {                            {                                 |
|    // nothing needed            override InstantiateAsync(ct)   |
|    // or override                 -> Result<Invoice,E>          |
|    //   CreateInstance()      }                                 |
|  }                                                              |
+----------------------------------------------------------------+
|  Source generator (BuilderGenerator)                            |
|  -----------------------------------------------------------   |
|  Emits OrderBuilder.g.cs / InvoiceBuilder.g.cs:                 |
|    * protected {T}? Prop { get; private set; }  <- properties   |
|    * public {Class}Builder With{Prop}(v) { ... return this; }   |
|    * virtual Validate{Prop}(value)      <- overridable guards   |
|    * virtual Validate{Prop}Item(v, i)   <- per-item for lists   |
|    * override ValidateAsync(ct)         <- calls all guards     |
|    * override BuildException(vr) or TypedBuildException(vr)     |
|    * sealed override Instantiate(...)   <- bridge to creation   |
|    * CreateInstance()                   <- strategy-driven       |
+----------------------------------------------------------------+
|  AbstractBuilder<T>  /  AbstractBuilder<T, TException>          |
|  -----------------------------------------------------------   |
|  BuildAsync(VisitedObjects?, ct)                                |
|    1. VisitedObjects.IsVisited(this, this)? -> return cached    |
|    2. _reference.IsResolved?     -> return cached Reference<T>  |
|    3. acquire SemaphoreSlim(1,1)                                |
|    4. double-check inside lock                                  |
|    5. ValidateAsync(ct)                                         |
|    6. validation failed? -> return failure                      |
|    7. Instantiate(reference, visitedObjects, ct)                |
|    8. release semaphore                                         |
|    +-- return Result<Reference<T>>                              |
+----------------------------------------------------------------+
|  Infrastructure types                                           |
|  -----------------------------------------------------------   |
|  Reference<T>       one-shot thread-safe resolve                |
|  VisitedObjects     ConcurrentDictionary (ref equality on       |
|                     builder instance)                           |
|  ValidationResult   ConcurrentDictionary<MemberName, Queue<Ex>> |
|  MemberName         record(Name, DeclaringType)                 |
+----------------------------------------------------------------+
```

---

## Key types

### `AbstractBuilder<T>`

```csharp
public abstract class AbstractBuilder<T> : IBuilder<T> where T : notnull
```

| Member | Role |
|---|---|
| `_reference : Reference<T>` | Caches the built instance after first successful build |
| `_buildLock : SemaphoreSlim(1,1)` | Ensures only one concurrent instantiation |
| `BuildAsync(VisitedObjects?, ct)` | Public async entry point |
| `abstract ValidateAsync(ct)` | Overridden by generator |
| `abstract Instantiate(ref, visited, ct)` | Sealed by generator; calls `CreateInstance()` |
| `abstract BuildException(vr)` | Overridden by generator |

### `AbstractBuilder<T, TException>`

Extends `AbstractBuilder<T>`. Adds:

| Member | Role |
|---|---|
| `BuildAsync(ct) -> Result<T, TException>` | Typed public entry point; calls `ValidateAsync()` then `InstantiateAsync()` |
| `sealed override BuildException` | Delegates to `TypedBuildException` |
| `abstract TypedBuildException(vr)` | Overridden by generator (default) or developer |
| `abstract InstantiateAsync(ct)` | Developer **must** implement; returns `Result<T, TException>` |

Note: `BuildAsync` in Mode 2 bypasses the `Instantiate` bridge entirely. It calls `ValidateAsync()` directly, then if validation passes, calls `InstantiateAsync()`. The sealed `Instantiate` bridge and `CreateInstance()` are still generated (for the `AbstractBuilder<T>` contract) but are not called in the Mode 2 flow.

### `Reference<T>`

```csharp
public sealed record Reference<T> where T : notnull
```

One-shot resolve. Throws `InvalidOperationException` if resolved twice. Thread-safe via `lock`.

```csharp
// Usage inside Instantiate:
var order = new Order(customerId, lines);
reference.Resolve(order);          // locks and marks resolved

// Later, anyone holding the reference:
var built = reference.Resolved();  // throws if not yet resolved
```

### `VisitedObjects`

```csharp
public sealed class VisitedObjects
```

`ConcurrentDictionary<object, IBuilder>` keyed by **reference equality**. The key is the **builder instance** (`this`), not the built value. When `IsVisited(this, this)` is called:
- First call -> `TryAdd` succeeds -> returns `false` (not yet visited, proceed normally)
- Subsequent calls -> `TryAdd` fails -> returns `true` (already visited, return cached ref)

```
Thread A: PersonBuilder.BuildAsync(visitedObjects)
  -> IsVisited(this, this) = false  <- adds builder instance to dict
  -> begins Instantiate ...
    -> ChildBuilder.BuildAsync(visitedObjects)
      -> IsVisited(this, this) = false <- adds child builder to dict
      -> begins Instantiate ...
        -> PersonBuilder.BuildAsync(visitedObjects)     <- re-entrant!
          -> IsVisited(this, this) = true <- same builder instance already there
          -> returns _reference (already partially resolved)
```

### `ValidationResult`

```csharp
public sealed class ValidationResult
```

Thread-safe error accumulator. Internally a `ConcurrentDictionary<MemberName, ConcurrentQueue<Exception>>`.

```csharp
var vr = new ValidationResult();

vr.AddError(new MemberName("Email", typeof(UserBuilder)),
    new FormatException("Not a valid email address"));

vr.AddError(new MemberName("Lines[2]", typeof(OrderBuilder)),
    new ArgumentException("Line cannot be empty"));

vr.IsSuccess   // false
vr.ToDataAnnotationsValidationResult().ErrorMessage
// -> "Not a valid email address; Line cannot be empty"
// -> MemberNames: ["UserBuilder.Email", "OrderBuilder.Lines[2]"]
```

---

## Data flow: successful build

```
caller
  |
  v
BuildAsync(visitedObjects, ct)
  |
  +-- IsVisited(this, this)? --Yes--> return _reference  (cycle short-circuit)
  |
  +-- IsResolved? --Yes--> return _reference  (cache hit)
  |
  +-- acquire _buildLock
  |     |
  |     +-- IsResolved? (double-check) --Yes--> release, return _reference
  |     |
  |     +-- ValidateAsync(ct)
  |     |     +-- calls Validate{Prop}(value) for each property
  |     |        calls Validate{Prop}Item(item, i) for each collection item
  |     |
  |     +-- validation failed? --Yes--> release, return Result.Failure(...)
  |     |
  |     +-- Instantiate(reference, visitedObjects, ct)
  |           +-- builds deferred dictionary properties (if any)
  |           +-- calls CreateInstance() (strategy-driven)
  |           +-- reference.Resolve(builtInstance)
  |
  +-- release _buildLock, return Result.Success(reference)
```

---

## Generated code anatomy

Given:

```csharp
[Builder]
public partial class ProductBuilder
{
    // Developer writes nothing -- generator emits everything
}
```

The generator emits `ProductBuilder.g.cs`:

```csharp
// <auto-generated/>
#nullable enable

public partial class ProductBuilder
    : global::FrenchExDev.Net.Builder.AbstractBuilder<global::Product>
{
    // -- Input properties --------------------------------------------------
    protected string? Name { get; private set; }
    public ProductBuilder WithName(string? value) { Name = value; return this; }
    protected decimal? Price { get; private set; }
    public ProductBuilder WithPrice(decimal? value) { Price = value; return this; }
    protected List<string>? Tags { get; private set; }
    public ProductBuilder WithTags(List<string>? value) { Tags = value; return this; }

    // -- Per-property validation -------------------------------------------
    protected virtual IEnumerable<Exception>? ValidateName(string? value) => null;
    protected virtual IEnumerable<Exception>? ValidatePrice(decimal? value) => null;
    protected virtual IEnumerable<Exception>? ValidateTags(List<string>? value) => null;
    protected virtual IEnumerable<Exception>? ValidateTagsItem(string item, int index) => null;

    // -- ValidateAsync -----------------------------------------------------
    protected override Task<Result<ValidationResult>> ValidateAsync(
        CancellationToken cancellationToken = default)
    {
        var __result = new ValidationResult();
        var __type = typeof(ProductBuilder);

        foreach (var __err in ValidateName(Name) ?? Array.Empty<Exception>())
            __result.AddError(new MemberName(nameof(Name), __type), __err);

        foreach (var __err in ValidatePrice(Price) ?? Array.Empty<Exception>())
            __result.AddError(new MemberName(nameof(Price), __type), __err);

        foreach (var __err in ValidateTags(Tags) ?? Array.Empty<Exception>())
            __result.AddError(new MemberName(nameof(Tags), __type), __err);

        if (Tags is not null)
        {
            var __TagsIdx = 0;
            foreach (var __TagsItem in Tags)
            {
                foreach (var __err in ValidateTagsItem(__TagsItem, __TagsIdx) ?? Array.Empty<Exception>())
                    __result.AddError(new MemberName($"{nameof(Tags)}[{__TagsIdx}]", __type), __err);
                __TagsIdx++;
            }
        }

        return Task.FromResult(Result<ValidationResult>.Success(__result));
    }

    // -- BuildException ----------------------------------------------------
    protected override Exception BuildException(Result<ValidationResult> validationResult)
        => new InvalidOperationException(
            validationResult.ValueOrThrow().ToDataAnnotationsValidationResult().ErrorMessage);

    // -- Instantiate (sealed bridge) ---------------------------------------
    protected sealed override async Task<Result<Reference<Product>>> Instantiate(
        Reference<Product> reference,
        VisitedObjects visitedObjects,
        CancellationToken cancellationToken = default)
    {
        var __value = CreateInstance();
        reference.Resolve(__value);
        return Result<Reference<Product>>.Success(reference);
    }

    // -- CreateInstance (strategy-driven) ----------------------------------
    protected virtual Product CreateInstance()
    {
        return new Product
        {
            Name = Name,
            Price = Price,
            Tags = Tags,
        };
    }
}
```

---

## Thread-safety guarantees

| Scenario | Mechanism | Guarantee |
|---|---|---|
| Two threads call `BuildAsync` simultaneously | `SemaphoreSlim(1,1)` + double-check | Built exactly once |
| Re-entrant call from within `Instantiate` | `VisitedObjects` dict (keyed by builder instance) | Returns same `Reference<T>` immediately |
| Multiple threads read a resolved reference | `Reference<T>` lock on resolve | Safe; subsequent reads lock-free after resolve |
| Validation accumulates errors from parallel tasks | `ConcurrentDictionary` + `ConcurrentQueue` | No lost errors, no corruption |
