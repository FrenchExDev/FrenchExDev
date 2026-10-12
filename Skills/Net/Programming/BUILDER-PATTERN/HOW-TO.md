# BUILDER-PATTERN — How To

Recipes for the common cases. Each one is paste-ready.

## Mode 1 — Simple Builder With `[Builder]`

For domain objects whose construction is mechanical and whose only failure mode is "validation didn't pass".

```csharp
[Builder]
public partial class UserBuilder
{
    // generator emits: protected string? Name { get; private set; }
    //                  public UserBuilder WithName(string?) { ... }
    //                  ... and so on for every declared property
}

var result = await new UserBuilder()
    .WithName("Alice")
    .WithEmail("alice@example.com")
    .WithAge(30)
    .BuildAsync();

User user = result.ValueOrThrow().Resolved();
```

If validation fails, `BuildAsync` returns a failure result whose `Error` is the `BuildException` payload (default: `InvalidOperationException` with the formatted error message).

## Adding Validation Rules

Override the generated `virtual` hooks. Return `null` for OK, or yield exceptions for failures.

```csharp
[Builder]
public partial class UserBuilder
{
    protected override IEnumerable<Exception>? ValidateEmail(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            yield return new ArgumentNullException(nameof(Email), "Email is required");
        else if (!value.Contains('@'))
            yield return new FormatException($"'{value}' is not a valid email");
    }

    protected override IEnumerable<Exception>? ValidateAge(int? value)
    {
        if (value is null) yield break;
        if (value < 0) yield return new ArgumentOutOfRangeException(nameof(Age), "Age cannot be negative");
        if (value > 150) yield return new ArgumentOutOfRangeException(nameof(Age), "Age is implausibly large");
    }
}
```

Every error accumulates. The caller gets all of them in one `ValidationResult`.

## Per-Item Validation for Collections

When a property is `List<T>`, the generator emits both `ValidateProp(value)` and `ValidatePropItem(item, index)`. Override the item hook for per-element rules:

```csharp
protected override IEnumerable<Exception>? ValidateLineItemDescriptionsItem(string item, int index)
{
    if (string.IsNullOrWhiteSpace(item))
        yield return new ArgumentException($"Line item at index {index} cannot be blank");
}
```

The error's `MemberName` becomes `"LineItemDescriptions[3]"` so callers know exactly which row failed.

## Choosing an Instantiation Strategy

The default is `init` — object initializer. Switch when the target's shape requires it.

```csharp
[Builder]                                  // default: init
[Builder(Instantiation = "init")]          // explicit
[Builder(Instantiation = "ctor")]          // ctor injection: new T(P1, P2, ...)
[Builder(Instantiation = "factory:Create")] // T.Create(P1, P2, ...)
[Builder(Instantiation = "custom")]        // emits abstract CreateInstance — you implement it
```

`custom` mode emits an abstract builder. Provide the concrete subclass yourself:

```csharp
[Builder(Instantiation = "custom")]
public abstract partial class OrderBuilder
{
    // generator emits: protected abstract Order CreateInstance();
}

public sealed class OrderBuilderImpl : OrderBuilder
{
    protected override Order CreateInstance() => /* your construction logic */;
}
```

## Mode 2 — Exception-Aware Builder

Use when the builder can fail with a domain-meaningful error (not found, quota exceeded, conflict). The failure type becomes part of the API signature.

```csharp
public sealed class OrderCreationException : Exception
{
    public OrderCreationException(string message) : base(message) { }
}

[Builder(Exception = typeof(OrderCreationException))]
public partial class OrderBuilder : AbstractBuilder<Order, OrderCreationException>
{
    private readonly IOrderRepository _orders;
    public OrderBuilder(IOrderRepository orders) => _orders = orders;

    // YOU implement this — the base class declares it abstract.
    protected override async Task<Result<Order, OrderCreationException>> InstantiateAsync(
        CancellationToken ct)
    {
        var customer = await _orders.FindCustomerAsync(CustomerId!.Value, ct);
        if (customer is null)
            return Result<Order, OrderCreationException>.Failure(
                new OrderCreationException("Customer not found"));

        return Result<Order, OrderCreationException>.Success(new Order(customer, Lines!));
    }
}

// Caller:
Result<Order, OrderCreationException> result = await new OrderBuilder(orders)
    .WithCustomerId(id)
    .WithLines(lines)
    .BuildAsync(ct);

return result.Match(
    onSuccess: order => Created(order),
    onFailure: ex    => UnprocessableEntity(ex.Message));
```

## Building a Circular Object Graph (Manual Builder)

The generator does not emit graph builders. Write a manual `AbstractBuilder<T>` and follow the protocol:

1. `reference.Resolve(instance)` **immediately** after creating the instance, before building children.
2. Pass `visitedObjects` into every nested `BuildAsync` call.

```csharp
public class PersonBuilder : AbstractBuilder<Person>
{
    private readonly string _name;
    private readonly List<ChildBuilder> _children = new();

    public PersonBuilder(string name) => _name = name;

    public PersonBuilder AddChild(string name)
    {
        _children.Add(new ChildBuilder(name, this));
        return this;
    }

    protected override async Task<Result<Reference<Person>>> Instantiate(
        Reference<Person> reference, VisitedObjects visited, CancellationToken ct)
    {
        var person = new Person { Name = _name };
        reference.Resolve(person);   // ← rule 1: resolve early

        foreach (var cb in _children)
        {
            var child = await cb.BuildAsync(visited, ct);   // ← rule 2: thread visited
            person.Children.Add(child.ValueOrThrow().Resolved());
        }

        return Result<Reference<Person>>.Success(reference);
    }
}
```

The `ChildBuilder` calls back into `PersonBuilder.BuildAsync(visited, ct)` to set its `Parent` reference. Because `PersonBuilder` is already in `visited`, the call returns the in-flight reference immediately — no infinite loop.

## Single-Flight in Action

A builder instance is a unit of work. N concurrent callers see one build:

```csharp
var builder = new ExpensiveReportBuilder().WithReportDate(DateTime.Today);

var tasks = Enumerable.Range(0, 20).Select(_ => builder.BuildAsync()).ToArray();
var results = await Task.WhenAll(tasks);

// All 20 results contain the same instance.
Assert.Equal(1, builder.InstantiateCalls);
Assert.All(results, r => Assert.Same(
    results[0].ValueOrThrow().Resolved(),
    r.ValueOrThrow().Resolved()));
```

Single-flight is automatic. Do not add your own caching layer in front.

## Dictionary Properties

For `Dictionary<string, string>`, the generator emits a configure-lambda overload:

```csharp
builder.WithHeaders(h => h
    .With("Content-Type", "application/json")
    .With("Accept", "text/html"));
```

For `Dictionary<string, ComposeService>` where `ComposeServiceBuilder` exists, the generator emits a deferred-build variant. Use the bulk overload or the single-entry convenience:

```csharp
// bulk
builder.WithServices(s => s
    .With("web", new ComposeServiceBuilder().WithImage("nginx").WithPort(80))
    .With("db",  new ComposeServiceBuilder().WithImage("postgres")));

// single entry
builder.WithService("web", svc => svc.WithImage("nginx").WithPort(80));
```

The nested builders run during the parent's `Instantiate` phase, sharing `VisitedObjects` and the cancellation token.

## Calling a Builder From a Source Generator

Reference `FrenchExDev.Net.Builder.SourceGenerator.Lib`. Build a `BuilderEmitModel`, call `BuilderEmitter.Emit(model)`, write the resulting source.

```csharp
var model = new BuilderEmitModel(
    ns: "MyApp.Generated",
    targetClassName: "MyTarget",
    builderClassName: "MyTargetBuilder",
    properties: new[]
    {
        new BuilderPropertyModel("Name", "string", "string?"),
        new BuilderPropertyModel("Items", "List<int>", "List<int>?"),
    },
    preamble: "    private readonly IClient _client;\n" +
              "    public MyTargetBuilder(IClient client) => _client = client;");

string source = BuilderEmitter.Emit(model);
context.AddSource("MyTargetBuilder.g.cs", source);
```

For per-property customisation use `WithMethodAttributes`, `WithMethodBodyPrefix`, and `InstantiationExpression`.

## Anti-Patterns

| Don't | Why |
|---|---|
| Throw exceptions inside `Validate*` hooks | Validation accumulates errors as values; throwing skips the rest. Yield exceptions instead. |
| Add `try/catch` around `BuildAsync` to handle domain failures | Use Mode 2 with a typed exception. The signature should declare what can go wrong. |
| Cache build results outside the builder | Single-flight is built in. An external cache produces stale references. |
| Build a graph without calling `reference.Resolve` early | Nested builders that call back will see an unresolved reference and infinite-loop. |
| Call `BuildAsync()` without threading `visitedObjects` in graph builders | Cycle detection breaks. Each call gets a fresh `VisitedObjects`. |
| Override the sealed `Instantiate` bridge | It is sealed for a reason — it threads `Reference<T>`. Override `CreateInstance` instead. |
