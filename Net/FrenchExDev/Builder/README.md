# FrenchExDev Builder

A **source-generator–powered async object construction framework** for .NET.

It solves a specific problem: building complex domain objects that require **async operations** (database calls, HTTP requests, I/O), **validation before construction**, and **safe handling of circular object graphs**.

Standard object constructors can't be `async`. The Builder pattern fills that gap, but writing thread-safe, cycle-safe, validated builders by hand is verbose and error-prone. This package automates all of that via a Roslyn source generator.

---

## Packages

| Package | Purpose |
|---|---|
| `FrenchExDev.Net.Builder` | `AbstractBuilder<T>` and `AbstractBuilder<T, TException>` base classes |
| `FrenchExDev.Net.Builder.Attributes` | `[Builder]` attribute (`netstandard2.0`) |
| `FrenchExDev.Net.Builder.SourceGenerator` | Roslyn incremental source generator |
| `FrenchExDev.Net.Builder.Testing` | Test helpers |

---

## The Two Modes

### Mode 1 — Simple (`[Builder]`)

Use when failures are unexpected (exceptional). The builder surface returns a `Result<ValidationResult>` to signal validation failures; if validation passes, construction runs and the built object is cached inside the builder.

```csharp
[Builder]
public partial class UserBuilder
{
    // Source generator adds the input properties:
    //   public string? Name { get; set; }
    //   public string? Email { get; set; }
    //   public int? Age { get; set; }

    private partial async Task<User> CreateAsync(CancellationToken ct)
    {
        var avatar = await _httpClient.GetAvatarAsync(Email!, ct);
        return new User(Name!, Email!, Age!.Value, avatar);
    }
}
```

```csharp
var builder = new UserBuilder { Name = "Alice", Email = "alice@example.com", Age = 30 };
Result<ValidationResult> result = await builder.BuildAsync();
// result.IsSuccess == true
// builder internally caches the built User
```

### Mode 2 — Exception-Aware (`[Builder(Exception = typeof(MyException))]`)

Use when failures are domain-expected (user not found, quota exceeded). Returns `Result<T, TException>` instead of a raw validation result.

```csharp
[Builder(Exception = typeof(OrderCreationException))]
public partial class OrderBuilder
{
    // Source generator adds:
    //   public Guid? CustomerId { get; set; }
    //   public List<OrderLineItem>? Lines { get; set; }

    protected override OrderCreationException BuildException(Result<ValidationResult> vr)
        => new OrderCreationException(vr.ValueOrThrow().ToDataAnnotationsValidationResult().ErrorMessage);

    protected override partial async Task<Result<Order, OrderCreationException>> InstantiateAsync(CancellationToken ct)
    {
        var customer = await _db.FindCustomerAsync(CustomerId!.Value, ct);
        if (customer is null)
            return Result<Order, OrderCreationException>.Failure(new OrderCreationException("Customer not found"));

        return Result<Order, OrderCreationException>.Success(new Order(customer, Lines!));
    }
}
```

```csharp
var builder = new OrderBuilder { CustomerId = customerId, Lines = myLines };
Result<Order, OrderCreationException> result = await builder.BuildAsync(ct);
// result.Value  — success path
// result.Error  — OrderCreationException on failure
```

---

## What the Source Generator Generates

Given:

```csharp
[Builder]
public partial class InvoiceBuilder
{
    public string? CustomerName { get; set; }
    public string? CustomerEmail { get; set; }
    public List<string>? LineItemDescriptions { get; set; }
}
```

The generator emits `InvoiceBuilder.g.cs`:

```csharp
// ── Input properties ──────────────────────────────────────────────────
public string? CustomerName { get; set; }
public string? CustomerEmail { get; set; }
public List<string>? LineItemDescriptions { get; set; }

// ── Per-property validation (virtual — override to add rules) ─────────
protected virtual IEnumerable<Exception>? ValidateCustomerName(string? value) => null;
protected virtual IEnumerable<Exception>? ValidateCustomerEmail(string? value) => null;
protected virtual IEnumerable<Exception>? ValidateLineItemDescriptions(List<string>? value) => null;

// For the collection: per-item validation with index
protected virtual IEnumerable<Exception>? ValidateLineItemDescriptionsItem(string item, int index) => null;

// ── ValidateAsync (calls all virtual methods) ─────────────────────────
protected override Task<Result<ValidationResult>> ValidateAsync(CancellationToken ct = default)
{
    var __result = new ValidationResult();
    var __type = typeof(InvoiceBuilder);

    foreach (var __err in ValidateCustomerName(CustomerName) ?? Array.Empty<Exception>())
        __result.AddError(new MemberName(nameof(CustomerName), __type), __err);

    foreach (var __err in ValidateCustomerEmail(CustomerEmail) ?? Array.Empty<Exception>())
        __result.AddError(new MemberName(nameof(CustomerEmail), __type), __err);

    foreach (var __err in ValidateLineItemDescriptions(LineItemDescriptions) ?? Array.Empty<Exception>())
        __result.AddError(new MemberName(nameof(LineItemDescriptions), __type), __err);

    if (LineItemDescriptions is not null)
    {
        var __idx = 0;
        foreach (var __item in LineItemDescriptions)
        {
            foreach (var __err in ValidateLineItemDescriptionsItem(__item, __idx) ?? Array.Empty<Exception>())
                __result.AddError(new MemberName($"LineItemDescriptions[{__idx}]", __type), __err);
            __idx++;
        }
    }

    return Task.FromResult(Result<ValidationResult>.Success(__result));
}

// ── BuildException + sealed Instantiate bridge ────────────────────────
protected override Exception BuildException(Result<ValidationResult> validationResult)
    => new InvalidOperationException(...);

protected sealed override async Task<Result<Reference<Invoice>>> Instantiate(
    Reference<Invoice> reference, VisitedObjects visitedObjects, CancellationToken ct)
{
    var __value = await CreateAsync(ct).ConfigureAwait(false);
    reference.Resolve(__value);
    return Result<Reference<Invoice>>.Success(reference);
}

// ── You must implement this ───────────────────────────────────────────
protected partial Task<Invoice> CreateAsync(CancellationToken ct = default);
```

---

## Validation Overrides

Override the generated `virtual` methods to add domain rules:

```csharp
[Builder]
public partial class InvoiceBuilder
{
    protected override IEnumerable<Exception>? ValidateCustomerEmail(string? value)
    {
        if (value is null)
            yield return new ArgumentNullException(nameof(CustomerEmail), "Email is required");
        else if (!value.Contains('@'))
            yield return new FormatException($"'{value}' is not a valid email");
    }

    protected override IEnumerable<Exception>? ValidateLineItemDescriptionsItem(string item, int index)
    {
        if (string.IsNullOrWhiteSpace(item))
            yield return new ArgumentException($"Line item at index {index} cannot be blank");
    }

    private partial async Task<Invoice> CreateAsync(CancellationToken ct)
    {
        var pdfBytes = await _pdfService.GenerateAsync(LineItemDescriptions!, ct);
        return new Invoice(CustomerName!, CustomerEmail!, LineItemDescriptions!, pdfBytes);
    }
}
```

If validation fails, `CreateAsync` is **never called**. Errors accumulate in `ValidationResult` with member names like `"InvoiceBuilder.CustomerEmail"` and `"LineItemDescriptions[2]"`.

---

## Circular Object Graph

The most powerful feature is safely building graphs with circular references (e.g., `Person → Child → Parent` back-reference).

The key rule: call `reference.Resolve(instance)` **early** — before building nested objects — so that when nested builders call back into the parent, they find its reference already resolved and skip re-instantiation.

```csharp
// Domain models
public class Person
{
    public string Name { get; set; }
    public List<Child> Children { get; } = new();
}

public class Child
{
    public string Name { get; set; }
    public Person Parent { get; set; }   // back-reference
}

// Builders (manual — no [Builder] attribute, full control over reference timing)
public class PersonBuilder : AbstractBuilder<Person>
{
    private readonly string _name;
    private readonly List<ChildBuilder> _childBuilders = new();

    public PersonBuilder(string name) => _name = name;

    public PersonBuilder AddChild(string name)
    {
        _childBuilders.Add(new ChildBuilder(name, this));
        return this;
    }

    protected override async Task<Result<Reference<Person>>> Instantiate(
        Reference<Person> reference, VisitedObjects visitedObjects, CancellationToken ct)
    {
        var person = new Person { Name = _name };
        reference.Resolve(person);   // ← resolve EARLY so children can find this reference

        foreach (var childBuilder in _childBuilders)
        {
            var childResult = await childBuilder.BuildAsync(visitedObjects, ct);
            person.Children.Add(childResult.ValueOrThrow().Resolved());
        }

        return Result<Reference<Person>>.Success(reference);
    }
}

public class ChildBuilder : AbstractBuilder<Child>
{
    private readonly string _name;
    private readonly PersonBuilder _parentBuilder;

    public ChildBuilder(string name, PersonBuilder parentBuilder)
    {
        _name = name;
        _parentBuilder = parentBuilder;
    }

    protected override async Task<Result<Reference<Child>>> Instantiate(
        Reference<Child> reference, VisitedObjects visitedObjects, CancellationToken ct)
    {
        var child = new Child { Name = _name };
        reference.Resolve(child);

        // Calls back into PersonBuilder — VisitedObjects prevents re-instantiation
        var parentResult = await _parentBuilder.BuildAsync(visitedObjects, ct);
        child.Parent = parentResult.ValueOrThrow().Resolved();

        return Result<Reference<Child>>.Success(reference);
    }
}
```

```csharp
var result = await new PersonBuilder("Alice")
    .AddChild("Bob")
    .AddChild("Claire")
    .BuildAsync();

var alice = result.ValueOrThrow().Resolved();
// alice.Name                    == "Alice"
// alice.Children[0].Name        == "Bob"
// alice.Children[0].Parent      == alice  ✓
// alice.Children[1].Name        == "Claire"
// alice.Children[1].Parent      == alice  ✓
```

`VisitedObjects` is a `ConcurrentDictionary` keyed by reference equality. When `ChildBuilder` calls back into `PersonBuilder`, `VisitedObjects.IsVisited()` returns `true` and the already-resolved `Reference<Person>` is returned immediately — no second instantiation.

---

## Concurrent Builds — Single-Flight

A `SemaphoreSlim(1,1)` + double-check pattern ensures the object is **built exactly once** regardless of how many concurrent callers there are:

```csharp
var builder = new ExpensiveReportBuilder { ReportDate = DateTime.Today };

var tasks = Enumerable.Range(0, 20)
    .Select(_ => builder.BuildAsync())
    .ToArray();

var results = await Task.WhenAll(tasks);

// All 20 calls got the same instance
Assert.Equal(1, builder.InstantiateCalls);   // built exactly once
Assert.All(results, r => Assert.Same(
    results[0].ValueOrThrow().Resolved(),
    r.ValueOrThrow().Resolved()));
```

---

## Architecture

```
[Builder] attribute
       │
       ▼
BuilderGenerator (Roslyn Incremental)
  ├── scans for [BuilderAttribute] on class declarations
  ├── extracts public settable properties
  │     ├── scalar     → generates ValidateProp(value)
  │     └── collection → generates ValidateProp(value) + ValidatePropItem(item, index)
  └── emits {ClassName}Builder.g.cs
        ├── input properties
        ├── virtual Validate* methods  (override to add rules)
        ├── ValidateAsync override     (calls all virtual methods)
        ├── BuildException override
        └── sealed Instantiate bridge (calls CreateAsync or InstantiateAsync)

AbstractBuilder<T>                       AbstractBuilder<T, TException>
  ├── _reference : Reference<T>            ├── extends AbstractBuilder<T>
  ├── _buildLock : SemaphoreSlim(1,1)      └── BuildAsync() → Result<T, TException>
  └── BuildAsync(VisitedObjects?, CT)
        1. VisitedObjects.IsVisited? → return cached reference
        2. _reference.IsResolved?   → return cached reference
        3. acquire semaphore
        4. double-check inside lock
        5. ValidateAsync()
        6. validation failed? → return failure
        7. Instantiate(reference, visitedObjects, ct)
        8. release semaphore
        └── return Result<Reference<T>>

Reference<T>       one-shot thread-safe resolve; throws if resolved twice
VisitedObjects     ConcurrentDictionary with ReferenceEqualityComparer; prevents cycles
ValidationResult   ConcurrentDictionary<MemberName, ConcurrentQueue<Exception>>
```

---

## When to Use Which Mode

| Scenario | Mode |
|---|---|
| Domain object needs I/O to construct | `[Builder]` |
| Business failures are expected (not found, quota exceeded) | `[Builder(Exception = typeof(...))]` |
| Object graph with back-references | Manual `AbstractBuilder<T>` (control `Resolve` timing) |
| Multiple concurrent callers need the same object | Either — single-flight is automatic |
| Collections need per-item validation | `[Builder]` — generator emits `ValidateXxxItem(item, index)` |
