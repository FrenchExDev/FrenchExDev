# Builder — How-To Guide

## 1. Simple builder (no domain errors)

Use `[Builder]` when construction failures are exceptional (unexpected). The builder throws or returns a `Result` failure on validation errors.

### Step 1 — Annotate your class

```csharp
[Builder]
public partial class ProductBuilder
{
    // The source generator adds these as public properties with { get; set; }:
    //   public string? Name { get; set; }
    //   public decimal? Price { get; set; }
    //   public List<string>? Tags { get; set; }
}
```

### Step 2 — Override validation (optional)

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
            yield return new ArgumentException("Name must be ≤ 200 characters.");
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

### Step 3 — Implement CreateAsync

```csharp
[Builder]
public partial class ProductBuilder
{
    private readonly IImageService _images;

    public ProductBuilder(IImageService images) => _images = images;

    private partial async Task<Product> CreateAsync(CancellationToken ct)
    {
        // Validation already passed. All properties are trusted here.
        var thumbnail = await _images.GenerateThumbnailAsync(Name!, ct);
        return new Product(Name!, Price!.Value, Tags ?? [], thumbnail);
    }
}
```

### Step 4 — Use it

Both styles work — plain property setters and method chaining (fluent):

```csharp
// Object initializer style
var builder = new ProductBuilder(imageService)
{
    Name = "Noise-Cancelling Headphones",
    Price = 299.99m,
    Tags = new() { "audio", "wireless" }
};

// Fluent style (generated With{Prop} methods)
var builder = new ProductBuilder(imageService)
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
    // dr.MemberNames → ["ProductBuilder.Name"]
}
```

---

## 2. Exception-aware builder (domain errors)

Use `[Builder(Exception = typeof(MyException))]` when failures are expected business outcomes (not found, quota exceeded, conflict). Returns `Result<T, TException>` — no exception thrown.

### Step 1 — Define your domain exception

```csharp
public sealed class OrderCreationException : Exception
{
    public OrderCreationException(string message) : base(message) { }
}
```

### Step 2 — Annotate and implement

```csharp
[Builder(Exception = typeof(OrderCreationException))]
public partial class OrderBuilder
{
    // Generator adds:
    //   public Guid? CustomerId { get; set; }
    //   public List<OrderLineItem>? Lines { get; set; }

    // Override how validation errors become your exception
    protected override OrderCreationException TypedBuildException(
        Result<ValidationResult> validationResult)
        => new OrderCreationException(
            validationResult.ValueOrThrow().ToDataAnnotationsValidationResult().ErrorMessage);

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

    protected override partial async Task<Result<Order, OrderCreationException>> InstantiateAsync(
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

### Step 3 — Use it

```csharp
var builder = new OrderBuilder(_db)
{
    CustomerId = Guid.Parse("..."),
    Lines = new() { new OrderLineItem(productId, quantity: 3) }
};

Result<Order, OrderCreationException> result = await builder.BuildAsync(ct);

result.Match(
    onSuccess: order => Console.WriteLine($"Order {order.Id} created"),
    onFailure: ex  => Console.WriteLine($"Failed: {ex.Message}")
);
```

---

## 3. Manual builder (graph with back-references)

When you need to build circular object graphs (e.g. `Person → Child → Parent`), skip `[Builder]` and inherit directly from `AbstractBuilder<T>`. The key rule: call `reference.Resolve(instance)` **before** building nested objects so that re-entrant calls find the reference already resolved.

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
        reference.Resolve(dept);   // ← resolve EARLY so employees can find it

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

        // Calls back into DepartmentBuilder — VisitedObjects prevents re-instantiation
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
// d.Members[0].Department      == d          ✓ back-reference
// d.Members[1].Department      == d          ✓ back-reference
```

---

## 4. Concurrent builds — single-flight

The `SemaphoreSlim(1,1)` inside every builder ensures that even with 20 concurrent callers, the object is **built exactly once**. No extra work needed — this is automatic.

```csharp
var builder = new ExpensiveReportBuilder { ReportDate = DateTime.Today };

// Fire 20 concurrent requests (e.g. 20 HTTP handlers all want the same report)
var tasks = Enumerable.Range(0, 20)
    .Select(_ => builder.BuildAsync())
    .ToList();

var results = await Task.WhenAll(tasks);

// All 20 got the same reference — object built once
Assert.All(results, r => Assert.True(r.IsSuccess));
Assert.Equal(1, builder.InstantiateCalls);
```

---

## 5. Validation with multiple errors

`ValidationResult` accumulates **all** errors — not just the first. Good for user-facing form validation where you want to show every problem at once.

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
var builder = new RegistrationBuilder { Username = "A!", Email = "notanemail" };
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

## 6. Collection item validation with index

The generator emits `Validate{Prop}Item(item, index)` for any collection property. Use it for per-element validation.

```csharp
[Builder]
public partial class InvoiceBuilder
{
    // public List<InvoiceLine>? Lines { get; set; }

    protected override IEnumerable<Exception>? ValidateLinesItem(InvoiceLine line, int index)
    {
        if (line.Quantity <= 0)
            yield return new ArgumentException($"Lines[{index}]: quantity must be positive.");
        if (line.UnitPrice < 0)
            yield return new ArgumentException($"Lines[{index}]: unit price cannot be negative.");
    }

    private partial async Task<Invoice> CreateAsync(CancellationToken ct)
        => new Invoice(Lines!);
}
```

Error member names will be formatted as `"InvoiceBuilder.Lines[2]"` — ready to pass to a validator framework.
