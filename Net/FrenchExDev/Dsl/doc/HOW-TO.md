# How To — FrenchExDev.Net.Dsl

Step-by-step guides for building DSLs using the Dsl framework.

---

## 1. Create a Simple DSL Concept

A DSL concept needs two things: an **attribute** (declarative) and a **companion class** (behavioral).

### Step 1: Create the companion class

```csharp
using FrenchExDev.Net.Dsl;

namespace MyDsl.Concepts;

public sealed class ServiceConcept : MetaConcept
{
    public override string Name => "Service";
    public override Type AttributeType => typeof(ServiceAttribute);
}
```

### Step 2: Create the attribute

```csharp
using FrenchExDev.Net.Dsl;
using MyDsl.Concepts;

namespace MyDsl;

[MetaConcept(typeof(ServiceConcept))]
[AttributeUsage(AttributeTargets.Class)]
public sealed class ServiceAttribute : Attribute
{
    [MetaProperty("Name", "string", Required = true)]
    public string Name { get; set; }

    public ServiceAttribute(string name) { Name = name; }
}
```

### Step 3: Developers use it

```csharp
[Service("OrderService")]
public class OrderService { /* ... */ }
```

The MetamodelRegistry source generator will automatically discover `ServiceAttribute` and include `"Service"` in the generated registry.

---

## 2. Add Properties to a Concept

Use `[MetaProperty]` on properties within your attribute class to describe configuration slots:

```csharp
[MetaConcept(typeof(EndpointConcept))]
[AttributeUsage(AttributeTargets.Class)]
public sealed class EndpointAttribute : Attribute
{
    [MetaProperty("Route", "string", Required = true)]
    public string Route { get; set; }

    [MetaProperty("Method", "string", DefaultValue = "GET")]
    public string Method { get; set; } = "GET";

    [MetaProperty("RequiresAuth", "bool")]
    public bool RequiresAuth { get; set; }

    public EndpointAttribute(string route) { Route = route; }
}
```

Usage:

```csharp
[Endpoint("/api/orders", Method = "POST", RequiresAuth = true)]
public class CreateOrderEndpoint { /* ... */ }
```

The generated `MetamodelRegistry` will include all three properties with their types and required flags.

---

## 3. Add Validation Constraints

Constraints are real C# static methods referenced by `nameof()`. They receive a `ConceptValidationContext` describing the decorated class.

### Step 1: Add `[MetaConstraint]` to the attribute

```csharp
[MetaConcept(typeof(ControllerConcept))]
[MetaConstraint("MustHaveRouteMethod", nameof(MustHaveRouteMethodConstraint),
    Message = "Controller must have at least one method with [Route]")]
[AttributeUsage(AttributeTargets.Class)]
public sealed class ControllerAttribute : Attribute
{
    [MetaProperty("Name", "string", Required = true)]
    public string Name { get; set; }

    public ControllerAttribute(string name) { Name = name; }

    // The constraint method — real C#, debuggable, testable
    public static ConstraintResult MustHaveRouteMethodConstraint(ConceptValidationContext ctx)
    {
        foreach (var method in ctx.Methods)
        {
            foreach (var attr in method.AttributeNames)
            {
                if (attr == "Route") return ConstraintResult.Satisfied();
            }
        }
        return ConstraintResult.Failed("Controller must have at least one method with [Route]");
    }
}
```

### Step 2: Test the constraint

```csharp
[Fact]
public void Satisfied_when_route_method_exists()
{
    var ctx = new ConceptValidationContext
    {
        ConceptName = "Controller",
        TypeName = "OrderController",
        Methods = new List<ConceptMethodInfo>
        {
            new ConceptMethodInfo
            {
                Name = "GetOrders",
                ReturnTypeName = "IActionResult",
                AttributeNames = new List<string> { "Route", "HttpGet" }
            }
        }
    };

    var result = ControllerAttribute.MustHaveRouteMethodConstraint(ctx);
    Assert.True(result.IsSatisfied);
}

[Fact]
public void Failed_when_no_route_methods()
{
    var ctx = new ConceptValidationContext
    {
        ConceptName = "Controller",
        TypeName = "EmptyController",
        Methods = new List<ConceptMethodInfo>()
    };

    var result = ControllerAttribute.MustHaveRouteMethodConstraint(ctx);
    Assert.False(result.IsSatisfied);
    Assert.Contains("must have at least one method", result.Message);
}
```

### Step 3: Run constraints at design time

```csharp
using FrenchExDev.Net.Dsl.Design;

var result = MetaConstraintRunner.RunConstraints(typeof(ControllerAttribute), context);
if (!result.IsSatisfied)
    Console.WriteLine($"Validation failed: {result.Message}");
```

---

## 4. Define Metamodel Inheritance

Use `[MetaInherits]` when one concept IS-A another at the metamodel level. This is separate from C# class inheritance.

### Example: AggregateRoot IS-A Entity

```csharp
// Entity companion
public sealed class EntityConcept : MetaConcept
{
    public override string Name => "Entity";
    public override Type AttributeType => typeof(EntityAttribute);
}

// AggregateRoot companion — declares Entity as parent
public sealed class AggregateRootConcept : MetaConcept
{
    public override string Name => "AggregateRoot";
    public override Type AttributeType => typeof(AggregateRootAttribute);

    // Mirror the [MetaInherits] declaration
    public override IReadOnlyList<Type> SuperTypes => new[] { typeof(EntityConcept) };
}

// The attribute with [MetaInherits]
[MetaConcept(typeof(AggregateRootConcept))]
[MetaInherits(typeof(EntityConcept))]
[AttributeUsage(AttributeTargets.Class)]
public sealed class AggregateRootAttribute : Attribute { /* ... */ }
```

The generated `MetamodelRegistry` will record `Inherits: ["Entity"]` for the AggregateRoot concept.

---

## 5. Define Containment Rules

Override `CanContain()` on the companion to restrict what child concepts are allowed:

```csharp
public sealed class AggregateRootConcept : MetaConcept
{
    public override string Name => "AggregateRoot";
    public override Type AttributeType => typeof(AggregateRootAttribute);

    public override bool CanContain(MetaConcept child)
    {
        // Only entities and value objects can live inside an aggregate
        return child is EntityConcept || child is ValueObjectConcept;
    }
}
```

Test it:

```csharp
[Fact]
public void AggregateRoot_can_contain_entity()
{
    var agg = new AggregateRootConcept();
    Assert.True(agg.CanContain(new EntityConcept()));
}

[Fact]
public void AggregateRoot_cannot_contain_command()
{
    var agg = new AggregateRootConcept();
    Assert.False(agg.CanContain(new CommandConcept()));
}
```

---

## 6. Add Lifecycle Hooks

Override lifecycle methods on the companion to hook into the source generator pipeline:

```csharp
public sealed class AggregateRootConcept : MetaConcept
{
    public override string Name => "AggregateRoot";
    public override Type AttributeType => typeof(AggregateRootAttribute);

    // Called when the SG discovers a class with [AggregateRoot]
    public override void OnDiscovered(ConceptValidationContext context)
    {
        // Register in a cross-cutting context, log, etc.
    }

    // Called before constraint methods run
    public override void OnBeforeValidation(ConceptValidationContext context)
    {
        // Pre-processing
    }

    // Called after all constraints have been evaluated
    public override void OnAfterValidation(ConceptValidationContext context, ConstraintResult result)
    {
        // Post-processing, audit, etc.
    }
}
```

---

## 7. Use MetaReference for Associations

Declare directed associations between concepts:

```csharp
[MetaConcept(typeof(CompositionConcept))]
[AttributeUsage(AttributeTargets.Property)]
public sealed class CompositionAttribute : Attribute
{
    // This reference says: Composition points to an Entity, is containment
    [MetaReference("Target", "Entity", IsContainment = true, Multiplicity = "1")]
    public Type TargetType { get; set; }
}
```

For bidirectional references, use `Opposite`:

```csharp
[MetaReference("Parent", "Category", Multiplicity = "0..1", Opposite = "Children")]
public Type ParentCategory { get; set; }
```

---

## 8. Work with ConstraintResult

`ConstraintResult` is an immutable value type:

```csharp
// Success
var ok = ConstraintResult.Satisfied();
Assert.True(ok.IsSatisfied);
Assert.Null(ok.Message);

// Failure
var fail = ConstraintResult.Failed("Name is required");
Assert.False(fail.IsSatisfied);
Assert.Equal("Name is required", fail.Message);

// Aggregate multiple results (fails if ANY fails)
var combined = ConstraintResult.Aggregate(new[]
{
    ConstraintResult.Satisfied(),
    ConstraintResult.Failed("error 1"),
    ConstraintResult.Failed("error 2"),
});
Assert.False(combined.IsSatisfied);
Assert.Equal("error 1; error 2", combined.Message);

// Equality
Assert.Equal(ConstraintResult.Satisfied(), ConstraintResult.Satisfied());
```

---

## 9. Build a Complete DSL (End-to-End Example)

Here's a complete mini-DSL for defining API endpoints, showing every feature.

### Companions

```csharp
namespace MyApi.Dsl.Concepts;

using FrenchExDev.Net.Dsl;

public sealed class ApiControllerConcept : MetaConcept
{
    public override string Name => "ApiController";
    public override Type AttributeType => typeof(ApiControllerAttribute);

    public override bool CanContain(MetaConcept child)
        => child is ApiActionConcept;
}

public sealed class ApiActionConcept : MetaConcept
{
    public override string Name => "ApiAction";
    public override Type AttributeType => typeof(ApiActionAttribute);
}
```

### Attributes

```csharp
namespace MyApi.Dsl;

using FrenchExDev.Net.Dsl;
using MyApi.Dsl.Concepts;

[MetaConcept(typeof(ApiControllerConcept))]
[MetaConstraint("MustHaveAction", nameof(MustHaveActionConstraint),
    Message = "API controller must have at least one [ApiAction] method")]
[AttributeUsage(AttributeTargets.Class)]
public sealed class ApiControllerAttribute : Attribute
{
    [MetaProperty("Route", "string", Required = true)]
    public string Route { get; set; }

    [MetaProperty("Version", "int", DefaultValue = "1")]
    public int Version { get; set; } = 1;

    public ApiControllerAttribute(string route) { Route = route; }

    public static ConstraintResult MustHaveActionConstraint(ConceptValidationContext ctx)
    {
        foreach (var m in ctx.Methods)
        {
            foreach (var a in m.AttributeNames)
            {
                if (a == "ApiAction") return ConstraintResult.Satisfied();
            }
        }
        return ConstraintResult.Failed("API controller must have at least one [ApiAction] method");
    }
}

[MetaConcept(typeof(ApiActionConcept))]
[AttributeUsage(AttributeTargets.Method)]
public sealed class ApiActionAttribute : Attribute
{
    [MetaProperty("HttpMethod", "string", Required = true)]
    public string HttpMethod { get; set; }

    [MetaProperty("Route", "string")]
    public string Route { get; set; }

    public ApiActionAttribute(string httpMethod) { HttpMethod = httpMethod; }
}
```

### Developer usage

```csharp
[ApiController("/api/v1/orders", Version = 1)]
public partial class OrdersController
{
    [ApiAction("GET")]
    public Task<IEnumerable<Order>> GetAll() { /* ... */ }

    [ApiAction("POST", Route = "create")]
    public Task<Order> Create(CreateOrderRequest request) { /* ... */ }

    [ApiAction("GET", Route = "{id}")]
    public Task<Order> GetById(Guid id) { /* ... */ }
}
```

### What the source generator sees

The MetamodelRegistry will contain:

```
Concepts["ApiController"] = {
    Name: "ApiController",
    Properties: [
        { Name: "Route", Type: "string", Required: true },
        { Name: "Version", Type: "int", Required: false }
    ],
    Constraints: [
        { Name: "MustHaveAction", MethodName: "MustHaveActionConstraint" }
    ]
}

Concepts["ApiAction"] = {
    Name: "ApiAction",
    Properties: [
        { Name: "HttpMethod", Type: "string", Required: true },
        { Name: "Route", Type: "string", Required: false }
    ]
}
```

A custom source generator for this DSL would then read these M1 usages and generate API routing code, OpenAPI specs, client SDKs, etc.

### Tests

```csharp
[Fact]
public void ApiController_has_MetaConcept()
{
    var attr = Attribute.GetCustomAttribute(
        typeof(ApiControllerAttribute), typeof(MetaConceptAttribute));
    Assert.NotNull(attr);
}

[Fact]
public void Constraint_passes_with_action_method()
{
    var ctx = new ConceptValidationContext
    {
        ConceptName = "ApiController",
        TypeName = "OrdersController",
        Methods = new List<ConceptMethodInfo>
        {
            new ConceptMethodInfo
            {
                Name = "GetAll",
                ReturnTypeName = "Task<IEnumerable<Order>>",
                AttributeNames = new List<string> { "ApiAction" }
            }
        }
    };

    Assert.True(ApiControllerAttribute.MustHaveActionConstraint(ctx).IsSatisfied);
}

[Fact]
public void Constraint_fails_without_action_method()
{
    var ctx = new ConceptValidationContext
    {
        ConceptName = "ApiController",
        TypeName = "EmptyController",
        Methods = new List<ConceptMethodInfo>()
    };

    var result = ApiControllerAttribute.MustHaveActionConstraint(ctx);
    Assert.False(result.IsSatisfied);
}

[Fact]
public void ApiController_can_contain_ApiAction()
{
    var controller = new ApiControllerConcept();
    Assert.True(controller.CanContain(new ApiActionConcept()));
}
```

---

## 10. Consume the MetamodelRegistry at Runtime

The generated `MetamodelRegistry.g.cs` is available in projects that reference DSL attribute assemblies. Use it to inspect the metamodel:

```csharp
using FrenchExDev.Net.Dsl.Generated;

// List all registered concepts
foreach (var (name, descriptor) in MetamodelRegistry.Concepts)
{
    Console.WriteLine($"Concept: {name}");
    Console.WriteLine($"  Attribute: {descriptor.AttributeType.Name}");
    Console.WriteLine($"  Companion: {descriptor.ConceptType.Name}");

    foreach (var prop in descriptor.Properties)
        Console.WriteLine($"  Property: {prop.Name} ({prop.Type}, required={prop.Required})");

    foreach (var constraint in descriptor.Constraints)
        Console.WriteLine($"  Constraint: {constraint.Name} -> {constraint.MethodName}");

    if (descriptor.Inherits.Count > 0)
        Console.WriteLine($"  Inherits: {string.Join(", ", descriptor.Inherits)}");
}
```

---

## 11. Run Constraints at Design Time

Use `MetaConstraintRunner` from `FrenchExDev.Net.Dsl.Design` to validate models outside of compilation:

```csharp
using FrenchExDev.Net.Dsl;
using FrenchExDev.Net.Dsl.Design;

// Build context from your model (via reflection, Roslyn, or manually)
var context = new ConceptValidationContext
{
    ConceptName = "AggregateRoot",
    TypeName = "Order",
    Properties = new List<ConceptPropertyInfo>
    {
        new ConceptPropertyInfo
        {
            Name = "Id",
            TypeName = "OrderId",
            AttributeNames = new List<string> { "EntityId" }
        },
        new ConceptPropertyInfo
        {
            Name = "Lines",
            TypeName = "IReadOnlyList<OrderLine>",
            AttributeNames = new List<string> { "Composition" }
        }
    },
    Methods = new List<ConceptMethodInfo>
    {
        new ConceptMethodInfo
        {
            Name = "HasLines",
            ReturnTypeName = "Result",
            AttributeNames = new List<string> { "Invariant" }
        }
    }
};

// Run all constraints declared on AggregateRootAttribute
var result = MetaConstraintRunner.RunConstraints(typeof(AggregateRootAttribute), context);

if (result.IsSatisfied)
    Console.WriteLine("All constraints satisfied.");
else
    Console.WriteLine($"Validation failed: {result.Message}");
```

---

## 12. Project Setup Checklist

When creating a new DSL project that uses `FrenchExDev.Net.Dsl`:

### Attributes project (`.Attributes`)

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFrameworks>netstandard2.0;net10.0</TargetFrameworks>
    <Nullable>enable</Nullable>
    <LangVersion>latest</LangVersion>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\..\..\Dsl\src\FrenchExDev.Net.Dsl\FrenchExDev.Net.Dsl.csproj" />
  </ItemGroup>
</Project>
```

### Source generator project (`.SourceGenerator`)

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>netstandard2.0</TargetFramework>
    <Nullable>enable</Nullable>
    <LangVersion>latest</LangVersion>
    <IsRoslynComponent>true</IsRoslynComponent>
    <EnforceExtendedAnalyzerRules>true</EnforceExtendedAnalyzerRules>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.CodeAnalysis.CSharp" PrivateAssets="all" />
    <PackageReference Include="Microsoft.CodeAnalysis.Analyzers" PrivateAssets="all" />
  </ItemGroup>
</Project>
```

### Test project (`.Tests`)

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
  </ItemGroup>
  <ItemGroup>
    <!-- Reference the Dsl SG as an analyzer to get MetamodelRegistry.g.cs -->
    <ProjectReference Include="..\..\..\..\Dsl\src\FrenchExDev.Net.Dsl.SourceGenerator\..."
                      OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
  </ItemGroup>
</Project>
```

### Naming conventions

| What | Pattern | Example |
|------|---------|---------|
| Attribute class | `{Name}Attribute` | `ContentPartAttribute` |
| Companion class | `{Name}Concept` | `ContentPartConcept` |
| Companion folder | `Concepts/` | `src/MyDsl.Attributes/Concepts/` |
| Constraint method | `{RuleName}Constraint` | `MustHaveFieldConstraint` |
| Namespace | `{Project}.Attributes` / `{Project}.Attributes.Concepts` | `FrenchExDev.Net.Ddd.Attributes` |

### netstandard2.0 constraints

When targeting `netstandard2.0` (required for attributes projects), avoid:

- `required` keyword
- `init` properties
- `[]` array expressions (use `Array.Empty<T>()` or `new T[] { }`)
- File-scoped namespaces (use `namespace X { }` block form)
- Records (use classes)
- Default interface methods

Use `get; set;` properties, block-scoped namespaces, and explicit constructors.
