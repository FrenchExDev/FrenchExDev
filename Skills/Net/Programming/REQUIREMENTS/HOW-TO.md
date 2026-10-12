# REQUIREMENTS — How-To

## Defining a New Epic

```csharp
public abstract class MyDomainEpic : Epic
{
    public override string Title => "My Domain";
    public override RequirementPriority Priority => RequirementPriority.High;
    public override string Owner => "Team Name";
}
```

Epics are top-level groupings. They have no parent.

## Defining a Feature with Acceptance Criteria

```csharp
public abstract class OrderPlacementFeature : Feature<MyDomainEpic>
{
    public override string Title => "Order Placement";
    public override RequirementPriority Priority => RequirementPriority.High;
    public override string Owner => "Commerce Team";

    // Each abstract method is an acceptance criterion
    public abstract AcceptanceCriterionResult UserCanPlaceOrder(
        UserId user, ResourceId product);

    public abstract AcceptanceCriterionResult OrderFailsWithInvalidProduct(
        UserId user, ResourceId invalidProduct);
}
```

Use domain concept types (`UserId`, `ResourceId`) in AC parameters, not raw `string` or `Guid`.

## Linking Specifications to Requirements

Create an interface annotated with `[ForRequirement]`:

```csharp
[ForRequirement(typeof(OrderPlacementFeature))]
public interface IOrderService
{
    [ForRequirement(typeof(OrderPlacementFeature),
        nameof(OrderPlacementFeature.UserCanPlaceOrder))]
    Task<OrderResult> PlaceOrderAsync(UserId user, ResourceId product);
}
```

- `[ForRequirement]` on the interface links it to the feature
- `[ForRequirement]` on methods links them to specific ACs via `nameof()`

## Linking Implementation to Requirements

```csharp
[ForRequirement(typeof(OrderPlacementFeature))]
public class OrderService : IOrderService
{
    [ForRequirement(typeof(OrderPlacementFeature),
        nameof(OrderPlacementFeature.UserCanPlaceOrder))]
    public Task<OrderResult> PlaceOrderAsync(UserId user, ResourceId product)
    {
        // Implementation
    }
}
```

## Linking Tests to Acceptance Criteria

```csharp
[TestsFor(typeof(OrderPlacementFeature))]
public class OrderPlacementTests
{
    [Fact]
    [Verifies(typeof(OrderPlacementFeature),
        nameof(OrderPlacementFeature.UserCanPlaceOrder))]
    public async Task User_can_place_order()
    {
        // Arrange, Act, Assert
    }

    [Fact]
    [Verifies(typeof(OrderPlacementFeature),
        nameof(OrderPlacementFeature.OrderFailsWithInvalidProduct))]
    public async Task Order_fails_with_invalid_product()
    {
        // ...
    }
}
```

- `[TestsFor]` on the test class links it to the requirement type
- `[Verifies]` on each `[Fact]` links it to a specific AC via `nameof()`

## Adding a New Domain Concept Type

In `Requirements/src/FrenchExDev.Net.Requirements/DomainConcepts.cs`:

```csharp
public readonly struct TenantId
{
    public Guid Value { get; }
    public TenantId(Guid value) => Value = value;
    public override string ToString() => Value.ToString();
}
```

Rules: `readonly struct`, `Value` property, `ToString()` override.

## Defining a Story or Task

```csharp
// Story under a feature
public abstract class CheckoutFlowStory : Story<OrderPlacementFeature>
{
    public override string Title => "Checkout Flow";
    public abstract AcceptanceCriterionResult CartConvertsToOrder(UserId user);
}

// Task with estimated hours
public abstract class PaymentIntegrationTask : RequirementTask<CheckoutFlowStory>
{
    public override string Title => "Integrate Payment Gateway";
    public override decimal EstimatedHours => 8;
}
```

## Interpreting Analyzer Diagnostics

| Diagnostic | Meaning | Fix |
|-----------|---------|-----|
| **REQ100** | Feature has no spec interface | Add `[ForRequirement(typeof(...))]` interface |
| **REQ101** | AC has no spec method | Add `[ForRequirement(..., nameof(AC))]` to interface method |
| **REQ102** | Spec references non-existent requirement | Fix `typeof()` reference |
| **REQ200** | Spec has no implementation | Implement the interface |
| **REQ300** | Requirement has no test class | Add `[TestsFor(typeof(...))]` test class |
| **REQ301** | AC has no test | Add `[Verifies(..., nameof(AC))]` test method |
| **REQ302** | Test references non-existent AC | Fix `nameof()` reference |

See: `Requirements/doc/HOW-TO.md`
