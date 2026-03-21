# FrenchExDev.Net.Requirements

A DSL for tracking requirements as C# types. Requirements are abstract classes, not Jira tickets. Acceptance criteria are abstract methods with typed parameters. The compiler enforces the chain: Requirements -> Specifications -> Implementations -> Tests.

## The Problem

String-based requirement tracking is fragile at every link in the chain:

```csharp
// Jira-style: every link is a string, every string can break silently
[Requirement("PROJ-142")]                  // Typo? Compiles fine.
[AcceptanceCriterion("PROJ-142", index: 2)] // AC reordered? Index is stale.
[TestsFor("PROJ-142")]                     // Ticket deleted? Ghost reference.
```

- **Typos compile.** `"PROJ-142"` vs `"PROJ-143"` -- no compiler error, no IDE warning.
- **Renames break silently.** Rename a requirement in the tracker and every string reference is stale.
- **Deletions leave ghost references.** Delete a ticket, tests still reference it, nobody notices.
- **Index-based ACs are fragile.** Reorder acceptance criteria and `index: 2` now means something different.
- **No IDE navigation.** You cannot Ctrl+Click a string to jump to its definition.

## The Solution

Requirements ARE types. `typeof()` and `nameof()` replace string IDs. The compiler catches broken links. The IDE navigates the chain.

```csharp
// Type-safe: every link is a type or member reference
[ForRequirement(typeof(OrderFeature))]                         // Rename-safe
[Verifies(typeof(OrderFeature), nameof(OrderFeature.UserCanPlaceOrder))] // Refactor-safe
[TestsFor(typeof(OrderFeature))]                               // Delete = compile error
```

- **Typos do not compile.** `typeof(OrderFeatur)` is a compiler error.
- **Renames propagate.** Rename `OrderFeature` and every `typeof()` updates automatically.
- **Deletions are caught.** Delete the type and every reference becomes a compile error.
- **ACs are methods.** `nameof(UserCanPlaceOrder)` is refactor-safe -- no index, no string.
- **Full IDE navigation.** Ctrl+Click on `OrderFeature` jumps to the requirement definition.

## Requirement Hierarchy

All requirement types derive from `RequirementMetadata`:

```csharp
public abstract class RequirementMetadata
{
    public abstract string Title { get; }
    public abstract RequirementPriority Priority { get; }
    public abstract string Owner { get; }
}

public abstract class Epic : RequirementMetadata { }

public abstract class Feature<TParent> : RequirementMetadata
    where TParent : Epic { }

public abstract class Feature : RequirementMetadata { }  // root-level, no parent

public abstract class Story<TParent> : RequirementMetadata
    where TParent : RequirementMetadata { }

public abstract class RequirementTask<TParent> : RequirementMetadata
    where TParent : RequirementMetadata
{
    public abstract int EstimatedHours { get; }
}

public abstract class Bug : RequirementMetadata
{
    public abstract BugSeverity Severity { get; }
}
```

The generic constraints are structural, not conventional. `Feature<TParent>` requires `TParent : Epic` -- you cannot write `Feature<Story<X>>`. The compiler enforces the hierarchy.

Supporting enums:

```csharp
public enum RequirementPriority { Critical, High, Medium, Low, Backlog }
public enum BugSeverity { Critical, Major, Minor, Cosmetic }
```

## AcceptanceCriterionResult

A readonly struct that represents whether an acceptance criterion is satisfied:

```csharp
public readonly struct AcceptanceCriterionResult : IEquatable<AcceptanceCriterionResult>
{
    public bool IsSatisfied { get; }
    public string FailureReason { get; }

    public static AcceptanceCriterionResult Satisfied();
    public static AcceptanceCriterionResult Failed(string reason);

    public static implicit operator bool(AcceptanceCriterionResult r) => r.IsSatisfied;
}
```

The implicit `bool` conversion means you can write `Assert.True(result)` or use it directly in `if` statements. Full equality support (`==`, `!=`, `Equals`, `GetHashCode`) is implemented.

## Attributes

Three attributes link the chain from specifications to implementations to tests:

| Attribute | Target | Purpose |
|-----------|--------|---------|
| `[ForRequirement(typeof(T), nameof(T.AC))]` | Class, Interface, Method | Links a spec or implementation to a requirement and optionally to a specific AC |
| `[Verifies(typeof(T), nameof(T.AC))]` | Method | Declares that a test method verifies a specific acceptance criterion |
| `[TestsFor(typeof(T))]` | Class | Declares that a test class covers a requirement |

All three are decorated with `[MetaConcept]`, making them discoverable by the DSL framework's concept system. Each has a corresponding concept class (`ForRequirementConcept`, `VerifiesConcept`, `TestsForConcept`) that extends `MetaConcept`.

```csharp
[MetaConcept(typeof(ForRequirementConcept))]
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface | AttributeTargets.Method,
    AllowMultiple = true)]
public sealed class ForRequirementAttribute : Attribute
{
    public Type RequirementType { get; }
    public string? AcceptanceCriterion { get; }
}

[MetaConcept(typeof(VerifiesConcept))]
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class VerifiesAttribute : Attribute
{
    public Type RequirementType { get; }
    public string AcceptanceCriterionName { get; }
}

[MetaConcept(typeof(TestsForConcept))]
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class TestsForAttribute : Attribute
{
    public Type RequirementType { get; }
}
```

## The Chain: From Requirement to Test

### 1. Define the requirement

Abstract classes define the "what". Acceptance criteria are abstract methods -- their parameters define the inputs, their return type (`AcceptanceCriterionResult`) enforces verifiability.

```csharp
public abstract class DomainEpic : Epic
{
    public override string Title => "Domain Management";
    public override RequirementPriority Priority => RequirementPriority.High;
    public override string Owner => "platform-team";
}

public abstract class OrderFeature : Feature<DomainEpic>
{
    public override string Title => "Order Placement";
    public override RequirementPriority Priority => RequirementPriority.High;
    public override string Owner => "commerce-team";

    public abstract AcceptanceCriterionResult UserCanPlaceOrder(UserId user, ResourceId product);
    public abstract AcceptanceCriterionResult OrderFailsForInvalidProduct(UserId user, ResourceId badProduct);
    public abstract AcceptanceCriterionResult AdminCanCancelOrder(UserId admin, RoleId adminRole, ResourceId order);
}
```

### 2. Specify the contract

Interfaces define the "how" at the boundary level. `[ForRequirement]` links the specification to the requirement.

```csharp
[ForRequirement(typeof(OrderFeature))]
public interface IOrderService
{
    Task<OrderResult> PlaceOrderAsync(UserId user, ResourceId product);
    Task CancelOrderAsync(UserId admin, ResourceId order);
}
```

### 3. Implement

The implementation class implements the interface. Methods can optionally be tagged with `[ForRequirement]` to link individual methods to specific ACs.

```csharp
public class OrderService : IOrderService
{
    [ForRequirement(typeof(OrderFeature), nameof(OrderFeature.UserCanPlaceOrder))]
    public Task<OrderResult> PlaceOrderAsync(UserId user, ResourceId product)
    {
        // implementation
    }

    [ForRequirement(typeof(OrderFeature), nameof(OrderFeature.AdminCanCancelOrder))]
    public Task CancelOrderAsync(UserId admin, ResourceId order)
    {
        // implementation
    }
}
```

### 4. Test

Test classes declare which requirement they cover. Individual test methods declare which acceptance criterion they verify.

```csharp
[TestsFor(typeof(OrderFeature))]
public class OrderFeatureTests
{
    [Fact]
    [Verifies(typeof(OrderFeature), nameof(OrderFeature.UserCanPlaceOrder))]
    public async Task User_can_place_order()
    {
        // arrange, act
        var result = AcceptanceCriterionResult.Satisfied();
        Assert.True(result);
    }

    [Fact]
    [Verifies(typeof(OrderFeature), nameof(OrderFeature.OrderFailsForInvalidProduct))]
    public async Task Order_fails_for_invalid_product()
    {
        var result = AcceptanceCriterionResult.Failed("Product not found");
        Assert.False(result);
        Assert.Equal("Product not found", result.FailureReason);
    }
}
```

### 5. Run quality gates

Quality gates inspect the `RequirementRegistry` (source-generated) and the test results to check:
- Every AC has at least one `[Verifies]` test
- Every requirement has at least one `[ForRequirement]` spec
- Pass rate, coverage, fuzz testing thresholds are met

## Source Generator Output

The `RequirementRegistryGenerator` is an incremental source generator that scans all abstract classes deriving from the requirement hierarchy and emits `RequirementRegistry.g.cs`:

```csharp
// <auto-generated/>
namespace FrenchExDev.Net.Requirements.Generated
{
    public static class RequirementRegistry
    {
        public static IReadOnlyDictionary<Type, RequirementInfo> All { get; } =
            new Dictionary<Type, RequirementInfo>
            {
                [typeof(SampleEpic)] = new RequirementInfo(
                    typeof(SampleEpic),
                    RequirementKind.Epic,
                    "Sample Epic",
                    null,
                    Array.Empty<string>()),
                [typeof(SampleFeature)] = new RequirementInfo(
                    typeof(SampleFeature),
                    RequirementKind.Feature,
                    "Sample Feature",
                    typeof(SampleEpic),
                    new string[] { "ThingWorks", "ThingHandlesErrors" }),
                // ...
            };
    }

    public sealed class RequirementInfo
    {
        public Type Type { get; }
        public RequirementKind Kind { get; }
        public string Title { get; }
        public Type? Parent { get; }
        public string[] AcceptanceCriteria { get; }
    }

    public enum RequirementKind { Epic, Feature, Story, Task, Bug }
}
```

The generator extracts the `Title` property value from expression-bodied overrides (string literals), detects the requirement kind by walking the base type chain, and collects all abstract methods returning `AcceptanceCriterionResult` as acceptance criteria.

## Analyzers

The `RequirementCoverageAnalyzer` provides compile-time diagnostics. Diagnostic IDs follow a structured scheme:

| ID | Description |
|--------|-------------|
| REQ100 | Feature has no specification -- an abstract requirement with ACs but no `[ForRequirement]` interface references it |
| REQ101 | Acceptance criterion has no specification method |
| REQ102 | Specification references non-existent requirement type |
| REQ200 | Specification has no implementation |
| REQ201 | Implementation references non-existent acceptance criterion |
| REQ202 | Implementation method not linked to any AC |
| REQ300 | Requirement has no test class |
| REQ301 | Acceptance criterion has no `[Verifies]` test method |
| REQ302 | `[Verifies]` references non-existent acceptance criterion |

REQ100 is currently implemented. REQ101-REQ302 are defined and will be added in a future phase.

## Domain Concepts

Lightweight value types live in the Requirements project itself (zero external dependencies). These give AC method signatures self-contained, precise types instead of raw primitives:

```csharp
public readonly struct UserId     // Guid Value, string FirstName, string LastName, Email Email
public readonly struct RoleId     // string Value
public readonly struct Email      // string Value
public readonly struct ResourceId // string Value
public readonly struct TokenId    // Guid Value
```

These types exist at the requirements level so that acceptance criteria can use them directly:

```csharp
public abstract AcceptanceCriterionResult AdminCanAssignRoles(
    UserId actingUser, UserId targetUser, RoleId role);
```

A SharedKernel project can reference `FrenchExDev.Net.Requirements` to reuse these same types in production code, keeping AC signatures and implementation signatures aligned.

## Project Structure

```
Requirements/
  FrenchExDev.Net.Requirements.slnx
  doc/
    ARCHITECTURE.md
    HOW-TO.md
    PHILOSOPHY.md
  src/
    FrenchExDev.Net.Requirements/              # Core types: hierarchy, AcceptanceCriterionResult, domain concepts
    FrenchExDev.Net.Requirements.Attributes/   # ForRequirement, Verifies, TestsFor + MetaConcept classes
    FrenchExDev.Net.Requirements.SourceGenerator/      # RequirementRegistryGenerator (incremental)
    FrenchExDev.Net.Requirements.SourceGenerator.Lib/  # RequirementRegistryEmitter + RequirementEmitModel
    FrenchExDev.Net.Requirements.Analyzers/    # RequirementCoverageAnalyzer (REQ1xx-REQ3xx)
    FrenchExDev.Net.Requirements.Testing/      # Sample requirements for testing (SampleEpic, SampleFeature, SampleStory)
  test/
    FrenchExDev.Net.Requirements.Tests/        # xUnit tests
```

## Tests

10 tests passing across three test classes:

- **RequirementHierarchyTests** -- verifies the type hierarchy (`Epic` is `RequirementMetadata`, generic constraints compile correctly, AC method detection via reflection)
- **AcceptanceCriterionResultTests** -- `Satisfied()` is true, `Failed(reason)` is false with reason, equality semantics
- **AttributeTests** -- all three attributes (`ForRequirement`, `Verifies`, `TestsFor`) carry `[MetaConcept]` decoration
