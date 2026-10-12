# Builder -- How-To Guide

## 1. Simple builder (no domain errors)

Use `[Builder]` when construction failures are exceptional (unexpected). The builder throws or returns a `Result` failure on validation errors.

### Step 1 -- Annotate your class

```csharp
[Builder]
public partial class ProductBuilder
{
    // The source generator adds these as protected properties with private set:
    //   protected string? Name { get; private set; }
    //   protected decimal? Price { get; private set; }
    //   protected List<string>? Tags { get; private set; }
    //
    // And public fluent methods:
    //   public ProductBuilder WithName(string? value) { Name = value; return this; }
    //   public ProductBuilder WithPrice(decimal? value) { Price = value; return this; }
    //   public ProductBuilder WithTags(List<string>? value) { Tags = value; return this; }
    //
    // And a strategy-driven CreateInstance():
    //   protected virtual Product CreateInstance() => new Product { Name = Name, Price = Price, Tags = Tags };
}
```

### Step 2 -- Override validation (optional)

The generator creates a virtual `Validate{Prop}` method for every property. Return `null` (default) to pass, or `yield return` exceptions to fail.

```csharp
[Builder]
public partial class ProductBuilder
{
    protected override IEnumerable<Exception>? ValidateName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            yield return new ArgumentException("Name is required.");
        else if (value.Length > 200)
            yield return new ArgumentException("Name must be 200 characters or fewer.");
    }

    protected override IEnumerable<Exception>? ValidatePrice(decimal? value)
    {
        if (value is null)
            yield return new ArgumentNullException(nameof(Price));
        else if (value <= 0)
            yield return new ArgumentOutOfRangeException(nameof(Price), "Price must be positive.");
    }

    // Collection: per-item validation with index
    protected override IEnumerable<Exception>? ValidateTagsItem(string item, int index)
    {
        if (string.IsNullOrWhiteSpace(item))
            yield return new ArgumentException($"Tag at index {index} cannot be blank.");
    }
}
```

### Step 3 -- Use it

Use the generated `With*()` fluent methods to set properties:

```csharp
var builder = new ProductBuilder()
    .WithName("Noise-Cancelling Headphones")
    .WithPrice(299.99m)
    .WithTags(new() { "audio", "wireless" });

var result = await builder.BuildAsync();

if (result.IsSuccess)
{
    var product = result.ValueOrThrow().Resolved();
    Console.WriteLine(product.Name);
}
else
{
    var dr = result.ValidationResult!;
    Console.WriteLine(dr.ErrorMessage);   // "Name is required."
    // dr.MemberNames -> ["ProductBuilder.Name"]
}
```

With the default `init` strategy, the generator creates the target object using an object initializer (`new Product { Name = Name, Price = Price, Tags = Tags }`). No developer code needed for `CreateInstance()`.

---

## 2. Instantiation strategies

The `[Builder]` attribute accepts an `Instantiation` parameter that controls how `CreateInstance()` builds the target object.

### `init` (default) -- Object initializer

```csharp
[Builder]
public partial class ProductBuilder { }
// Generated: protected virtual Product CreateInstance() => new Product { Name = Name, Price = Price };
```

### `ctor` -- Constructor

```csharp
[Builder(Instantiation = "ctor")]
public partial class ProductBuilder { }
// Generated: protected virtual Product CreateInstance() => new Product(Name, Price);
```

### `factory:MethodName` -- Static factory method

```csharp
[Builder(Instantiation = "factory:Create")]
public partial class ProductBuilder { }
// Generated: protected virtual Product CreateInstance() => Product.Create(Name, Price);
```

### `custom` -- Developer-implemented

```csharp
[Builder(Instantiation = "custom")]
public abstract partial class ProductBuilder
{
    // Generated: protected abstract Product CreateInstance();
    // The builder class is emitted as abstract -- you must implement CreateInstance():
    protected override Product CreateInstance()
    {
        return new Product(Name!, Price!.Value, ComputeChecksum(Name!));
    }
}
```

All strategies except `custom` emit `CreateInstance()` as `virtual`, so you can override it if needed.

---

## 3. Exception-aware builder (domain errors)

Use `[Builder(Exception = typeof(MyException))]` when failures are expected business outcomes (not found, quota exceeded, conflict). Returns `Result<T, TException>` -- no exception thrown.

### Step 1 -- Define your domain exception

```csharp
public sealed class OrderCreationException : Exception
{
    public OrderCreationException(string message) : base(message) { }
}
```

### Step 2 -- Annotate and implement

The generator emits properties, `With*()` methods, validation hooks, `ValidateAsync`, `CreateInstance()`, and a default `TypedBuildException`. But you **must** implement `InstantiateAsync` -- it is declared `abstract` on `AbstractBuilder<T, TException>`.

```csharp
[Builder(Exception = typeof(OrderCreationException))]
public partial class OrderBuilder
{
    // Generator adds:
    //   protected Guid? CustomerId { get; private set; }
    //   protected List<OrderLineItem>? Lines { get; private set; }
    //   public OrderBuilder WithCustomerId(Guid? value) { ... }
    //   public OrderBuilder WithLines(List<OrderLineItem>? value) { ... }

    protected override IEnumerable<Exception>? ValidateLines(List<OrderLineItem>? value)
    {
        if (value is null || value.Count == 0)
            yield return new ArgumentException("Order must have at least one line.");
    }

    protected override IEnumerable<Exception>? ValidateLinesItem(
        OrderLineItem item, int index)
    {
        if (item.Quantity <= 0)
            yield return new ArgumentException($"Line {index}: quantity must be positive.");
    }

    // YOU implement this (abstract from AbstractBuilder<T, TException>):
    protected override async Task<Result<Order, OrderCreationException>> InstantiateAsync(
        CancellationToken ct)
    {
        var customer = await _db.FindCustomerAsync(CustomerId!.Value, ct);
        if (customer is null)
            return Result<Order, OrderCreationException>.Failure(
                new OrderCreationException($"Customer {CustomerId} not found."));

        if (!customer.IsActive)
            return Result<Order, OrderCreationException>.Failure(
                new OrderCreationException("Customer account is suspended."));

        var order = new Order(customer, Lines!);
        return Result<Order, OrderCreationException>.Success(order);
    }
}
```

Note: `BuildException` is `sealed` on `AbstractBuilder<T, TException>` and delegates to `TypedBuildException`. The generator provides a default `TypedBuildException` that converts the `ValidationResult` to an exception message. Override `TypedBuildException` if you need custom error formatting.

### Step 3 -- Use it

```csharp
var builder = new OrderBuilder(_db)
    .WithCustomerId(Guid.Parse("..."))
    .WithLines(new() { new OrderLineItem(productId, quantity: 3) });

Result<Order, OrderCreationException> result = await builder.BuildAsync(ct);

result.Match(
    onSuccess: order => Console.WriteLine($"Order {order.Id} created"),
    onFailure: ex  => Console.WriteLine($"Failed: {ex.Message}")
);
```

---

## 4. Manual builder (graph with back-references)

When you need to build circular object graphs (e.g. `Person -> Child -> Parent`), skip `[Builder]` and inherit directly from `AbstractBuilder<T>`. The key rule: call `reference.Resolve(instance)` **before** building nested objects so that re-entrant calls find the reference already resolved.

```csharp
// Domain
public class Department
{
    public string Name { get; }
    public List<Employee> Members { get; } = new();
    public Department(string name) { Name = name; }
}

public class Employee
{
    public string Name { get; }
    public Department Department { get; private set; } = null!;
    public Employee(string name) { Name = name; }
    public void AssignDepartment(Department d) { Department = d; }
}

// Builders
public class DepartmentBuilder : AbstractBuilder<Department>
{
    private readonly string _name;
    private readonly List<EmployeeBuilder> _memberBuilders = new();

    public DepartmentBuilder(string name) { _name = name; }

    public DepartmentBuilder AddMember(string employeeName)
    {
        _memberBuilders.Add(new EmployeeBuilder(employeeName, this));
        return this;
    }

    protected override Exception BuildException(Result<ValidationResult> vr)
        => new InvalidOperationException(
            vr.ValueOrThrow().ToDataAnnotationsValidationResult().ErrorMessage);

    protected override Task<Result<ValidationResult>> ValidateAsync(CancellationToken ct)
        => Task.FromResult(Result<ValidationResult>.Success(new ValidationResult()));

    protected override async Task<Result<Reference<Department>>> Instantiate(
        Reference<Department> reference,
        VisitedObjects visitedObjects,
        CancellationToken ct)
    {
        var dept = new Department(_name);
        reference.Resolve(dept);   // resolve EARLY so employees can find it

        foreach (var memberBuilder in _memberBuilders)
        {
            var r = await memberBuilder.BuildAsync(visitedObjects, ct);
            if (!r.IsSuccess) return Result<Reference<Department>>.Failure(r.ValidationResult!);
            dept.Members.Add(r.ValueOrThrow().Resolved());
        }

        return Result<Reference<Department>>.Success(reference);
    }
}

public class EmployeeBuilder : AbstractBuilder<Employee>
{
    private readonly string _name;
    private readonly DepartmentBuilder _deptBuilder;

    public EmployeeBuilder(string name, DepartmentBuilder deptBuilder)
    { _name = name; _deptBuilder = deptBuilder; }

    protected override Exception BuildException(Result<ValidationResult> vr)
        => new InvalidOperationException(
            vr.ValueOrThrow().ToDataAnnotationsValidationResult().ErrorMessage);

    protected override Task<Result<ValidationResult>> ValidateAsync(CancellationToken ct)
        => Task.FromResult(Result<ValidationResult>.Success(new ValidationResult()));

    protected override async Task<Result<Reference<Employee>>> Instantiate(
        Reference<Employee> reference,
        VisitedObjects visitedObjects,
        CancellationToken ct)
    {
        var employee = new Employee(_name);
        reference.Resolve(employee);

        // Calls back into DepartmentBuilder -- VisitedObjects prevents re-instantiation
        var deptResult = await _deptBuilder.BuildAsync(visitedObjects, ct);
        employee.AssignDepartment(deptResult.ValueOrThrow().Resolved());

        return Result<Reference<Employee>>.Success(reference);
    }
}
```

Usage:

```csharp
var dept = await new DepartmentBuilder("Engineering")
    .AddMember("Alice")
    .AddMember("Bob")
    .BuildAsync();

var d = dept.ValueOrThrow().Resolved();
// d.Name                       == "Engineering"
// d.Members[0].Name            == "Alice"
// d.Members[0].Department      == d          back-reference intact
// d.Members[1].Department      == d          back-reference intact
```

---

## 5. Concurrent builds -- single-flight

The `SemaphoreSlim(1,1)` inside every builder ensures that even with 20 concurrent callers, the object is **built exactly once**. No extra work needed -- this is automatic.

```csharp
var builder = new ExpensiveReportBuilder()
    .WithReportDate(DateTime.Today);

// Fire 20 concurrent requests (e.g. 20 HTTP handlers all want the same report)
var tasks = Enumerable.Range(0, 20)
    .Select(_ => builder.BuildAsync())
    .ToList();

var results = await Task.WhenAll(tasks);

// All 20 got the same reference -- object built once
Assert.All(results, r => Assert.True(r.IsSuccess));
Assert.Equal(1, builder.InstantiateCalls);
```

---

## 6. Validation with multiple errors

`ValidationResult` accumulates **all** errors -- not just the first. Good for user-facing form validation where you want to show every problem at once.

```csharp
[Builder]
public partial class RegistrationBuilder
{
    protected override IEnumerable<Exception>? ValidateUsername(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            yield return new ArgumentException("Username is required.");
        else if (value.Length < 3)
            yield return new ArgumentException("Username must be at least 3 characters.");
        else if (!Regex.IsMatch(value, @"^[a-z0-9_]+$"))
            yield return new ArgumentException("Username may only contain a-z, 0-9, and underscores.");
    }

    protected override IEnumerable<Exception>? ValidateEmail(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            yield return new ArgumentException("Email is required.");
        else if (!value.Contains('@'))
            yield return new FormatException("Email is not valid.");
    }
}
```

```csharp
var builder = new RegistrationBuilder()
    .WithUsername("A!")
    .WithEmail("notanemail");

var result = await builder.BuildAsync();

// result.IsSuccess == false
var dr = result.ValidationResult!;
// dr.ErrorMessage:
//   "Username must be at least 3 characters.; ..."
//   "Email is not valid."
// dr.MemberNames:
//   ["RegistrationBuilder.Username", "RegistrationBuilder.Email"]
```

---

## 7. Collection item validation with index

The generator emits `Validate{Prop}Item(item, index)` for any collection property. Use it for per-element validation.

```csharp
[Builder]
public partial class InvoiceBuilder
{
    // Generated: protected List<InvoiceLine>? Lines { get; private set; }

    protected override IEnumerable<Exception>? ValidateLinesItem(InvoiceLine line, int index)
    {
        if (line.Quantity <= 0)
            yield return new ArgumentException($"Lines[{index}]: quantity must be positive.");
        if (line.UnitPrice < 0)
            yield return new ArgumentException($"Lines[{index}]: unit price cannot be negative.");
    }
}
```

Error member names will be formatted as `"InvoiceBuilder.Lines[2]"` -- ready to pass to a validator framework.

---

## 8. Dictionary properties

For dictionary-typed properties, the generator emits lambda overloads for fluent construction.

### Simple dictionary (no value builder)

```csharp
builder.WithHeaders(h => h
    .With("Content-Type", "application/json")
    .With("Accept", "text/html"));
```

### Builder-aware dictionary

When the value type has a known builder, you get deferred building and a singular convenience method:

```csharp
// Bulk configuration
builder.WithServices(s => s
    .With("web", b => b.WithImage("nginx:latest").WithPort(80))
    .With("api", b => b.WithImage("myapp:1.0").WithPort(8080)));

// Or one at a time
builder
    .WithService("web", b => b.WithImage("nginx:latest").WithPort(80))
    .WithService("api", b => b.WithImage("myapp:1.0").WithPort(8080));
```

Value builders are stored and built during the parent's `Instantiate` phase, with `visitedObjects` passed through for cycle safety.
