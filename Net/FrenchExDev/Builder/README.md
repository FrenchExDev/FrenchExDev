# FrenchExDev Builder

A **source-generator-powered async object construction framework** for .NET.

It solves a specific problem: building complex domain objects that require **async operations** (database calls, HTTP requests, I/O), **validation before construction**, and **safe handling of circular object graphs**.

Standard object constructors can't be `async`. The Builder pattern fills that gap, but writing thread-safe, cycle-safe, validated builders by hand is verbose and error-prone. This package automates all of that via a Roslyn source generator.

---

## Packages

| Package | Purpose |
|---|---|
| `FrenchExDev.Net.Builder` | `AbstractBuilder<T>` and `AbstractBuilder<T, TException>` base classes |
| `FrenchExDev.Net.Builder.Attributes` | `[Builder]` attribute (`netstandard2.0`) |
| `FrenchExDev.Net.Builder.SourceGenerator` | Roslyn incremental source generator |
| `FrenchExDev.Net.Builder.SourceGenerator.Lib` | Reusable builder emission library (no Roslyn dependency) |
| `FrenchExDev.Net.Builder.Testing` | Test helpers |

---

## The Two Modes

### Mode 1 -- Simple (`[Builder]`)

Use when failures are unexpected (exceptional). The builder surface returns a `Result<ValidationResult>` to signal validation failures; if validation passes, construction runs and the built object is cached inside the builder.

The generator emits everything: input properties, `With*()` fluent methods, validation hooks, `ValidateAsync`, `BuildException`, the sealed `Instantiate` bridge, and a `CreateInstance()` method that creates the target object. By default, `CreateInstance()` uses an object initializer -- no developer code needed.

```csharp
[Builder]
public partial class UserBuilder
{
    // Source generator adds:
    //   protected string? Name { get; private set; }
    //   protected string? Email { get; private set; }
    //   protected int? Age { get; private set; }
    //   public UserBuilder WithName(string? value) { ... }
    //   public UserBuilder WithEmail(string? value) { ... }
    //   public UserBuilder WithAge(int? value) { ... }
    //   protected virtual User CreateInstance() => new User { Name = Name, Email = Email, Age = Age };
}
```

```csharp
var builder = new UserBuilder()
    .WithName("Alice")
    .WithEmail("alice@example.com")
    .WithAge(30);

Result<Reference<User>> result = await builder.BuildAsync();
// result.IsSuccess == true
// builder internally caches the built User
```

### Mode 2 -- Exception-Aware (`[Builder(Exception = typeof(MyException))]`)

Use when failures are domain-expected (user not found, quota exceeded). Returns `Result<T, TException>` instead of a raw validation result.

The builder inherits from `AbstractBuilder<T, TException>`. The generator still emits properties, `With*()` methods, validation hooks, `ValidateAsync`, the `Instantiate` bridge, and `CreateInstance()`. Additionally it generates a default `TypedBuildException` override.

The base class declares `protected abstract Task<Result<TClass, TException>> InstantiateAsync(CancellationToken)`. The developer **must** implement this method -- it is abstract on `AbstractBuilder<T, TException>`, not generated.

The `BuildException` method is `sealed` on `AbstractBuilder<T, TException>` and delegates to `TypedBuildException`.

```csharp
[Builder(Exception = typeof(OrderCreationException))]
public partial class OrderBuilder : AbstractBuilder<Order, OrderCreationException>
{
    // Generated: With*(), ValidateAsync, CreateInstance(), TypedBuildException, etc.

    // YOU implement this (abstract from base class):
    protected override async Task<Result<Order, OrderCreationException>> InstantiateAsync(
        CancellationToken ct)
    {
        var customer = await _db.FindCustomerAsync(CustomerId!.Value, ct);
        if (customer is null)
            return Result<Order, OrderCreationException>.Failure(
                new OrderCreationException("Customer not found"));

        return Result<Order, OrderCreationException>.Success(new Order(customer, Lines!));
    }
}
```

```csharp
var builder = new OrderBuilder()
    .WithCustomerId(customerId)
    .WithLines(myLines);

Result<Order, OrderCreationException> result = await builder.BuildAsync(ct);
// result.Value  -- success path
// result.Error  -- OrderCreationException on failure
```

---

## Instantiation Strategies

The `[Builder]` attribute accepts an `Instantiation` parameter that controls how the generated `CreateInstance()` method creates the target object. Four strategies are available:

### `init` (default) -- Object initializer

```csharp
[Builder]                              // default: "init"
[Builder(Instantiation = "init")]      // explicit: same as default
```

Generated `CreateInstance()`:

```csharp
protected virtual T CreateInstance()
{
    return new T
    {
        Name = Name,
        Price = Price,
    };
}
```

Requires the target type to have a parameterless constructor and settable (or init) properties.

### `ctor` -- Constructor

```csharp
[Builder(Instantiation = "ctor")]
```

Generated `CreateInstance()`:

```csharp
protected virtual T CreateInstance()
{
    return new T(Name, Price);
}
```

Passes all builder properties as constructor arguments, in declaration order.

### `factory:MethodName` -- Static factory method

```csharp
[Builder(Instantiation = "factory:Create")]
```

Generated `CreateInstance()`:

```csharp
protected virtual T CreateInstance()
{
    return T.Create(Name, Price);
}
```

Calls the named static method on the target type with all properties as arguments.

### `custom` -- Developer-implemented

```csharp
[Builder(Instantiation = "custom")]
```

Generated `CreateInstance()`:

```csharp
protected abstract T CreateInstance();
```

The builder class is emitted as `abstract`. The developer must provide a concrete subclass (or implement `CreateInstance()` in the partial class) with the creation logic.

All strategies except `custom` emit `CreateInstance()` as `virtual`, so the developer can override it if needed.

---

## What the Source Generator Generates

Given:

```csharp
[Builder]
public partial class InvoiceBuilder
{
    // Developer writes nothing here -- the generator does it all
}
```

The generator emits `InvoiceBuilder.g.cs`:

```csharp
// ── Input properties ──────────────────────────────────────────────────
protected string? CustomerName { get; private set; }
public InvoiceBuilder WithCustomerName(string? value) { CustomerName = value; return this; }

protected string? CustomerEmail { get; private set; }
public InvoiceBuilder WithCustomerEmail(string? value) { CustomerEmail = value; return this; }

protected List<string>? LineItemDescriptions { get; private set; }
public InvoiceBuilder WithLineItemDescriptions(List<string>? value) { LineItemDescriptions = value; return this; }

// ── Per-property validation (virtual -- override to add rules) ─────────
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

// ── BuildException ────────────────────────────────────────────────────
protected override Exception BuildException(Result<ValidationResult> validationResult)
    => new InvalidOperationException(
        validationResult.ValueOrThrow().ToDataAnnotationsValidationResult().ErrorMessage);

// ── Instantiate (sealed bridge) ───────────────────────────────────────
protected sealed override async Task<Result<Reference<Invoice>>> Instantiate(
    Reference<Invoice> reference, VisitedObjects visitedObjects, CancellationToken ct)
{
    var __value = CreateInstance();
    reference.Resolve(__value);
    return Result<Reference<Invoice>>.Success(reference);
}

// ── CreateInstance (strategy-driven) ──────────────────────────────────
protected virtual Invoice CreateInstance()
{
    return new Invoice
    {
        CustomerName = CustomerName,
        CustomerEmail = CustomerEmail,
        LineItemDescriptions = LineItemDescriptions,
    };
}
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
}
```

If validation fails, `CreateInstance` is **never called**. Errors accumulate in `ValidationResult` with member names like `"InvoiceBuilder.CustomerEmail"` and `"LineItemDescriptions[2]"`.

---

## Dictionary Properties

When a builder property is typed as `Dictionary<TKey, TValue>`, the generator emits additional overloads for fluent dictionary construction.

### Simple dictionaries (`DictionaryBuilder<K, V>`)

For dictionaries where the value type has no known builder:

```csharp
// Generated:
public MyBuilder WithHeaders(Action<DictionaryBuilder<string, string>> configure)
{
    var __b = new DictionaryBuilder<string, string>();
    configure(__b);
    Headers = __b.Build();
    return this;
}
```

Usage:

```csharp
builder.WithHeaders(h => h
    .With("Content-Type", "application/json")
    .With("Accept", "text/html"));
```

### Builder-aware dictionaries (`DictionaryBuilder<K, V, TVBuilder>`)

When the value type has a known builder (e.g., `ComposeServiceBuilder` for `ComposeService`), the generator emits a deferred-build pattern:

```csharp
// Generated field:
private DictionaryBuilder<string, ComposeService, ComposeServiceBuilder>? __ServicesBuilder;

// Bulk lambda overload:
public MyBuilder WithServices(Action<DictionaryBuilder<string, ComposeService, ComposeServiceBuilder>> configure)
{
    __ServicesBuilder = new DictionaryBuilder<string, ComposeService, ComposeServiceBuilder>();
    configure(__ServicesBuilder);
    return this;
}

// Single-entry convenience:
public MyBuilder WithService(string key, Action<ComposeServiceBuilder> configure)
{
    __ServicesBuilder ??= new DictionaryBuilder<string, ComposeService, ComposeServiceBuilder>();
    var __b = new ComposeServiceBuilder();
    configure(__b);
    __ServicesBuilder.With(key, __b);
    return this;
}
```

The dictionary builder stores `TValueBuilder` instances and builds them during the parent's `Instantiate` phase (inside the sealed bridge), passing `visitedObjects` and `cancellationToken` through for cycle safety.

---

## Utility Types

The `FrenchExDev.Net.Builder` package provides several utility types used by generated builders and available for manual builders:

| Type | Purpose |
|---|---|
| `BuilderList<T, TBuilder>` | A `List<TBuilder>` with `AsReferenceList()` (converts to `ReferenceList<T>`) and `New(Action<TBuilder>)` for fluent builder creation |
| `ReferenceList<T>` | A list of `Reference<T>` with lazy resolution -- implements `IReferenceList<T>`, supports `AsEnumerable()`, `Queryable`, `Any()`, `Where()`, `Select()` |
| `IReferenceList<T>` | Interface for `ReferenceList<T>` -- extends `IList<T>` with `AsEnumerable()`, `Queryable`, `ElementAt()`, `Any()`, `Add(Reference<T>)`, `Contains(Reference<T>)` |
| `DictionaryBuilder<K, V>` | Fluent builder for simple dictionaries -- `With(key, value)` then `Build()` |
| `DictionaryBuilder<K, V, TVBuilder>` | Fluent builder for dictionaries with builder-aware values -- stores `TVBuilder` instances for deferred building via `BuildAsync(visitedObjects, ct)` |
| `FailuresDictionary` | `Dictionary<MemberName, List<Exception>>` -- error accumulator for custom validation scenarios |

---

## Circular Object Graph

The most powerful feature is safely building graphs with circular references (e.g., `Person -> Child -> Parent` back-reference).

The key rule: call `reference.Resolve(instance)` **early** -- before building nested objects -- so that when nested builders call back into the parent, they find its reference already resolved and skip re-instantiation.

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

// Builders (manual -- no [Builder] attribute, full control over reference timing)
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
        reference.Resolve(person);   // resolve EARLY so children can find this reference

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

        // Calls back into PersonBuilder -- VisitedObjects prevents re-instantiation
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
// alice.Children[0].Parent      == alice
// alice.Children[1].Name        == "Claire"
// alice.Children[1].Parent      == alice
```

`VisitedObjects` is a `ConcurrentDictionary` keyed by reference equality. The key passed to `IsVisited` is the **builder instance** (`this`), not the built value. When `ChildBuilder` calls back into `PersonBuilder`, `VisitedObjects.IsVisited(this, this)` returns `true` and the already-resolved `Reference<Person>` is returned immediately -- no second instantiation.

---

## Concurrent Builds -- Single-Flight

A `SemaphoreSlim(1,1)` + double-check pattern ensures the object is **built exactly once** regardless of how many concurrent callers there are:

```csharp
var builder = new ExpensiveReportBuilder()
    .WithReportDate(DateTime.Today);

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

## Extension Points for Source Generator Authors

The `FrenchExDev.Net.Builder.SourceGenerator.Lib` package provides `BuilderEmitter` and its models (`BuilderEmitModel`, `BuilderPropertyModel`) for downstream source generators that need to emit builder classes. These are used by `ComposeBundleGenerator` and the `BinaryWrapper` source generator.

### `BuilderEmitModel.Preamble`

Raw C# source inserted after the class opening brace, before input properties. Use for custom fields, constructors, or nested types:

```csharp
new BuilderEmitModel(
    ns: "MyNamespace",
    targetClassName: "MyTarget",
    builderClassName: "MyTargetBuilder",
    properties: props,
    preamble: "    private readonly IClient _client;\n    public MyTargetBuilder(IClient client) => _client = client;");
```

### `BuilderPropertyModel.WithMethodAttributes`

List of attribute lines emitted before each `With*()` method. Useful for version annotations:

```csharp
new BuilderPropertyModel(
    name: "Image",
    typeFull: "string",
    nullableTypeFull: "string?",
    withMethodAttributes: new[] { "[SinceVersion(\"2.0\")]" });
```

### `BuilderPropertyModel.WithMethodBodyPrefix`

Raw C# lines inserted at the start of the `With*()` method body, before the property assignment:

```csharp
new BuilderPropertyModel(
    name: "Volumes",
    typeFull: "List<string>",
    nullableTypeFull: "List<string>?",
    withMethodBodyPrefix: "EnsureNotBuilt();");
```

### `BuilderPropertyModel.InstantiationExpression`

Custom expression used instead of the property name in `CreateInstance()`. When `null`, defaults to the property name:

```csharp
new BuilderPropertyModel(
    name: "Tags",
    typeFull: "List<string>",
    nullableTypeFull: "List<string>?",
    instantiationExpression: "Tags?.AsReadOnly()");
```

Generated in `CreateInstance()`:

```csharp
return new MyTarget
{
    Tags = Tags?.AsReadOnly(),   // instead of: Tags = Tags,
};
```

---

## Architecture

```
[Builder] attribute
       |
       v
BuilderGenerator (Roslyn Incremental)
  +-- scans for [BuilderAttribute] on class declarations
  +-- extracts properties from target type
  |     +-- scalar     -> generates ValidateProp(value)
  |     +-- collection -> generates ValidateProp(value) + ValidatePropItem(item, index)
  |     +-- dictionary -> generates WithProp(Action<DictionaryBuilder<...>>) overloads
  +-- emits {ClassName}Builder.g.cs
        +-- protected input properties (private set)
        +-- public With*() fluent methods
        +-- virtual Validate* methods  (override to add rules)
        +-- ValidateAsync override     (calls all virtual methods)
        +-- BuildException override    (or TypedBuildException for Mode 2)
        +-- sealed Instantiate bridge  (calls CreateInstance)
        +-- CreateInstance             (strategy-driven: init/ctor/factory/custom)

AbstractBuilder<T>                       AbstractBuilder<T, TException>
  +-- _reference : Reference<T>            +-- extends AbstractBuilder<T>
  +-- _buildLock : SemaphoreSlim(1,1)      +-- BuildAsync() -> Result<T, TException>
  +-- BuildAsync(VisitedObjects?, CT)      +-- sealed BuildException -> TypedBuildException
        1. VisitedObjects.IsVisited(this, this)? -> return cached reference
        2. _reference.IsResolved?   -> return cached reference
        3. acquire semaphore
        4. double-check inside lock
        5. ValidateAsync()
        6. validation failed? -> return failure
        7. Instantiate(reference, visitedObjects, ct)
        8. release semaphore
        +-- return Result<Reference<T>>

Reference<T>       one-shot thread-safe resolve; throws if resolved twice
VisitedObjects     ConcurrentDictionary keyed by builder instance (reference equality); prevents cycles
ValidationResult   ConcurrentDictionary<MemberName, ConcurrentQueue<Exception>>
```

---

## When to Use Which Mode

| Scenario | Mode |
|---|---|
| Domain object needs I/O to construct | `[Builder]` |
| Business failures are expected (not found, quota exceeded) | `[Builder(Exception = typeof(...))]` |
| Object graph with back-references | Manual `AbstractBuilder<T>` (control `Resolve` timing) |
| Multiple concurrent callers need the same object | Either -- single-flight is automatic |
| Collections need per-item validation | `[Builder]` -- generator emits `ValidateXxxItem(item, index)` |
| Target type uses constructor injection | `[Builder(Instantiation = "ctor")]` |
| Target type has a static factory method | `[Builder(Instantiation = "factory:Create")]` |
| Construction logic is complex or async | `[Builder(Instantiation = "custom")]` or Mode 2 with `InstantiateAsync` |
