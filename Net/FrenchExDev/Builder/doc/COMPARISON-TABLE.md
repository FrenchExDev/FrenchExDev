# Builder — Comparison Table

## Builder modes at a glance

| | `[Builder]` | `[Builder(Exception = ...)]` | Manual `AbstractBuilder<T>` |
|---|---|---|---|
| **Base class** | `AbstractBuilder<T>` | `AbstractBuilder<T, TException>` | `AbstractBuilder<T>` |
| **Developer implements** | Nothing (or override `CreateInstance()` with `custom` strategy) | `override Task<Result<T,E>> InstantiateAsync(ct)` (abstract from base) | `override Task<Result<Reference<T>>> Instantiate(ref, visited, ct)` |
| **Failure surface** | `Result<ValidationResult>` | `Result<T, TException>` | `Result<ValidationResult>` |
| **When to use** | I/O-heavy construction, unexpected errors | Domain-expected failures (not found, conflict) | Circular graphs, custom `Reference<T>` timing |
| **Validation hooks** | Generated | Generated | Manual |
| **Input properties** | Generated (`protected`, `private set`) | Generated (`protected`, `private set`) | Manual |
| **`reference.Resolve` timing** | Managed by generator bridge | Managed by generator bridge | **Developer controls** |
| **`CreateInstance()` strategy** | `init`/`ctor`/`factory:X`/`custom` | Same (but Mode 2 uses `InstantiateAsync` flow) | N/A |

---

## Validation approaches

| Approach | Where | Scope |
|---|---|---|
| `ValidateName(value)` | Override in builder class | Single property |
| `ValidateTagsItem(item, i)` | Override in builder class | Per collection element |
| `ValidateAsync(ct)` | Override the whole method | Cross-property, async checks |
| `InstantiateAsync(ct)` (Mode 2) | Return `Result.Failure(ex)` | Post-validation, domain rule |

Example mixing approaches:

```csharp
[Builder(Exception = typeof(BookingException))]
public partial class BookingBuilder
{
    // Property-level: purely structural
    protected override IEnumerable<Exception>? ValidateStartDate(DateOnly? value)
    {
        if (value is null) yield return new ArgumentNullException(nameof(StartDate));
    }

    protected override IEnumerable<Exception>? ValidateEndDate(DateOnly? value)
    {
        if (value is null) yield return new ArgumentNullException(nameof(EndDate));
    }

    // Cross-property: override ValidateAsync for range check
    protected override async Task<Result<ValidationResult>> ValidateAsync(CancellationToken ct)
    {
        var vr = (await base.ValidateAsync(ct)).ValueOrThrow();

        if (StartDate is not null && EndDate is not null && EndDate <= StartDate)
            vr.AddError(new MemberName("EndDate", typeof(BookingBuilder)),
                new ArgumentException("End date must be after start date."));

        return Result<ValidationResult>.Success(vr);
    }

    // Domain-level: async check in InstantiateAsync (abstract from base class)
    protected override async Task<Result<Booking, BookingException>> InstantiateAsync(
        CancellationToken ct)
    {
        var available = await _calendar.IsAvailableAsync(RoomId!.Value, StartDate!.Value, EndDate!.Value, ct);
        if (!available)
            return Result<Booking, BookingException>.Failure(
                new BookingException("Room is not available for the requested period."));

        return Result<Booking, BookingException>.Success(
            new Booking(RoomId!.Value, StartDate!.Value, EndDate!.Value));
    }
}
```

---

## Error representation

| `ValidationResult` member | Type | Content |
|---|---|---|
| `IsSuccess` | `bool` | `true` when no errors added |
| `AddError(MemberName, Exception)` | void | Adds one error for a member |
| `AddErrors(MemberName, IEnumerable<Exception>)` | void | Bulk add for one member |
| `AddErrors(IEnumerable<(MemberName, Exception)>)` | void | Add from tuple sequence |
| `ToDataAnnotationsValidationResult()` | `ValidationResult` | Converts to `System.ComponentModel.DataAnnotations` format |

Member names in the output:

| Situation | Member name format |
|---|---|
| Scalar property `Name` | `"ProductBuilder.Name"` |
| Collection property `Tags[2]` | `"ProductBuilder.Tags[2]"` |
| Manual `AddError` with custom type | `"{FullTypeName}.{Name}"` |

---

## `Result<T>` usage summary

The `Result` package is used throughout:

| Location | Type | Meaning |
|---|---|---|
| `ValidateAsync` return | `Result<ValidationResult>` | Success = no validation errors; Failure = unexpected error running validation |
| `BuildAsync` (non-generic) | `Result<Reference<T>>` (internal) | Plumbing only — developer never sees `Reference<T>` |
| `BuildAsync` (generic) | `Result<T, TException>` | Success = built object; Failure = typed domain exception |
| `Instantiate` return | `Result<Reference<T>>` | Internal; generator bridge calls `CreateInstance()` |
| `InstantiateAsync` return | `Result<T, TException>` | Developer implements this (Mode 2); returns typed success or failure |

---

## Concurrency model

```
Time →

Thread 1: BuildAsync() ─────────── ValidateAsync ─── Instantiate ─── ✓ resolved
                          ↑ lock acquired                      ↑ lock released

Thread 2: BuildAsync() ───────────────── wait on lock ─────────────────── ✓ cache hit
Thread 3: BuildAsync() ───────────────── wait on lock ─────────────────── ✓ cache hit
Thread 4: BuildAsync() ──────────────────── wait on lock ─────────────── ✓ cache hit
```

All threads receive the same `Reference<T>` instance. `Instantiate` is called exactly once.

---

## Circular graph protocol

```
DepartmentBuilder.BuildAsync(visited)
│
├─ visited.IsVisited(deptBuilder) = false  ← added to dict
│
├─ dept = new Department("Engineering")
├─ reference.Resolve(dept)                 ← dept is now findable
│
├─ EmployeeBuilder("Alice").BuildAsync(visited)
│   ├─ visited.IsVisited(empBuilder) = false ← added
│   ├─ emp = new Employee("Alice")
│   ├─ reference.Resolve(emp)
│   │
│   └─ DepartmentBuilder.BuildAsync(visited)  ← re-entrant!
│       ├─ visited.IsVisited(deptBuilder) = true ← already there
│       └─ returns _reference               ← already resolved = dept
│
└─ emp.AssignDepartment(dept)              ✓ circular reference intact
```

**Rule:** Always call `reference.Resolve(instance)` **before** calling `BuildAsync` on any nested builder.

---

## Supported collection types for per-item validation

| C# type | Detected as collection |
|---|---|
| `T[]` | Yes |
| `IEnumerable<T>` | Yes |
| `ICollection<T>` | Yes |
| `IList<T>` | Yes |
| `List<T>` | Yes |
| `IReadOnlyList<T>` | Yes |
| `IReadOnlyCollection<T>` | Yes |
| `Dictionary<K,V>` | No |
| `HashSet<T>` | No |
| `ImmutableArray<T>` | No |

For unsupported collection types, override `ValidateAsync` directly.

---

## Target framework compatibility

| Feature | `netstandard2.0` | `net10.0` |
|---|---|---|
| Core builder machinery | Yes | Yes |
| Source generator | Yes (runs in compiler) | Yes |
| `record MemberName(...)` | Yes (polyfill) | Yes |
| `ReferenceEqualityComparer` | Yes (polyfill) | Yes |
| `[init]` properties | Yes (polyfill) | Yes |
| Covariant return types | No — uses `TypedBuildException` bridge | Yes (sealed override delegates) |
| `ArgumentNullException.ThrowIfNull` | Replaced with explicit null check | Yes |
| `KeyValuePair` deconstruction in foreach | Replaced with explicit `.Key`/`.Value` | Yes |
