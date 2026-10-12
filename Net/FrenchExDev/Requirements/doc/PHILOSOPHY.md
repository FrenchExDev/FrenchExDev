# Philosophy: Why Requirements Are Types

## The Problem with String-Based Tracking

Every mainstream project management tool -- Jira, Linear, Azure DevOps, GitHub Issues -- tracks requirements as strings. A ticket ID is a string. An acceptance criterion is a numbered line in a text field. A test references a ticket by pasting its ID into a comment or attribute.

Here is the typical approach:

```csharp
// Step 1: Someone writes "PROJ-142: User can place orders" in Jira
// Step 2: Someone writes AC #2: "Admin can cancel orders" in a text field
// Step 3: A developer tags a test

[Requirement("PROJ-142")]
[AcceptanceCriterion("PROJ-142", index: 2)]
public void Admin_can_cancel_order() { /* ... */ }
```

Every link in this chain is a string, and every string can break silently:

| Failure mode | What happens | When you find out |
|---|---|---|
| Typo in ID | `"PROJ-142"` vs `"PROJ-143"` -- compiles, passes CI | Never (or during an audit, months later) |
| AC reordered | AC #2 was "cancel orders", now it is "refund orders" | Never (test still passes, but verifies the wrong thing) |
| Ticket renamed | "User can place orders" becomes "Customer can submit orders" | Never (string references are stale, nobody updates them) |
| Ticket deleted | PROJ-142 is archived or deleted | Never (test still references a ghost) |
| Navigate | Ctrl+Click on `"PROJ-142"` | Nothing happens. Open a browser, search Jira. |

The fundamental problem is not that these tools are bad. It is that strings are not checkable. A compiler cannot verify that a string matches a requirement. An IDE cannot refactor a string.

Now here is the same chain with types:

```csharp
// Step 1: Define the requirement as a type
public abstract class OrderFeature : Feature<DomainEpic>
{
    public override string Title => "Order Placement";
    public override RequirementPriority Priority => RequirementPriority.High;
    public override string Owner => "commerce-team";

    public abstract AcceptanceCriterionResult UserCanPlaceOrder(UserId user, ResourceId product);
    public abstract AcceptanceCriterionResult AdminCanCancelOrder(UserId admin, RoleId adminRole, ResourceId order);
}

// Step 2: Tag the test
[TestsFor(typeof(OrderFeature))]
public class OrderFeatureTests
{
    [Verifies(typeof(OrderFeature), nameof(OrderFeature.AdminCanCancelOrder))]
    public void Admin_can_cancel_order() { /* ... */ }
}
```

| Failure mode | What happens | When you find out |
|---|---|---|
| Typo in type | `typeof(OrderFeatur)` | Compile error. Immediately. |
| AC reordered | Methods have names, not indices | Not applicable. `nameof(AdminCanCancelOrder)` is stable. |
| Requirement renamed | Rename `OrderFeature` via IDE refactor | Every `typeof()` and `nameof()` updates automatically. |
| Requirement deleted | Delete `OrderFeature` | Compile error at every reference. Immediately. |
| Navigate | Ctrl+Click on `OrderFeature` | Jumps to the abstract class definition. |

## Requirements ARE Types

The shift is not just syntactic. It is conceptual.

In string-based systems, a requirement is metadata: a title, a description, a status, stored somewhere outside the code. The code references it by ID. The requirement and the code are two separate things connected by a fragile string.

In this DSL, a requirement IS a type. It is not metadata attached to a marker class. It is not a comment. It is not an attribute on an empty class. The requirement type itself has behavior -- its abstract methods are its acceptance criteria.

```csharp
public abstract class OrderFeature : Feature<DomainEpic>
{
    // These are not decorations. These ARE the acceptance criteria.
    public abstract AcceptanceCriterionResult UserCanPlaceOrder(UserId user, ResourceId product);
    public abstract AcceptanceCriterionResult OrderFailsForInvalidProduct(UserId user, ResourceId badProduct);
    public abstract AcceptanceCriterionResult AdminCanCancelOrder(UserId admin, RoleId adminRole, ResourceId order);
}
```

`OrderFeature` is not a container for metadata about some external requirement. It IS the requirement. The abstract methods are not annotations describing acceptance criteria. They ARE the acceptance criteria. The compiler knows they exist. The IDE can navigate to them. Roslyn analyzers can inspect them.

## Why Abstract Methods for Acceptance Criteria

An acceptance criterion must answer four questions: What is it called? What are its inputs? What does it produce? Is it verified?

Abstract methods answer all four:

### 1. The method name IS the identifier

```csharp
public abstract AcceptanceCriterionResult AdminCanAssignRoles(
    UserId actingUser, UserId targetUser, RoleId role);
```

The name `AdminCanAssignRoles` is the identifier. Not `"AC-3"`. Not `index: 2`. Not a string in a Jira field. A name that the compiler knows, that `nameof()` can reference, that an IDE can rename across the entire codebase in one operation.

### 2. Parameters define the inputs precisely

Compare:

```
// Jira AC: "An admin user can assign roles to other users"
```

What is an "admin user"? A string? A GUID? A user with a specific role? What is "other users"? What is a "role"?

```csharp
public abstract AcceptanceCriterionResult AdminCanAssignRoles(
    UserId actingUser, UserId targetUser, RoleId role);
```

Now it is unambiguous. The acting user is a `UserId`. The target is a `UserId`. The role is a `RoleId`. These are typed -- you cannot pass a `RoleId` where a `UserId` is expected. The AC signature IS the specification of what "admin", "user", and "role" mean in this context.

### 3. The return type enforces verifiability

Every AC returns `AcceptanceCriterionResult`. This is not optional. You cannot define an abstract method returning `void` and call it an acceptance criterion -- the source generator only collects methods returning `AcceptanceCriterionResult`.

```csharp
// This is an AC (collected by the source generator):
public abstract AcceptanceCriterionResult UserCanPlaceOrder(UserId user, ResourceId product);

// This is NOT an AC (ignored by the source generator):
public abstract void DoSomething();
```

The return type is a gate: if you cannot express the criterion as something that is either `Satisfied()` or `Failed(reason)`, it is not an acceptance criterion.

### 4. `nameof()` is refactor-safe

When a test verifies an AC:

```csharp
[Verifies(typeof(OrderFeature), nameof(OrderFeature.AdminCanCancelOrder))]
public void Admin_can_cancel_order() { /* ... */ }
```

Renaming `AdminCanCancelOrder` to `AdminCanVoidOrder` updates the `nameof()` expression everywhere. No broken links. No stale strings. No manual search-and-replace.

## Why Generic Constraints

The requirement hierarchy uses generic constraints to enforce structural rules:

```csharp
public abstract class Feature<TParent> : RequirementMetadata
    where TParent : Epic { }

public abstract class Story<TParent> : RequirementMetadata
    where TParent : RequirementMetadata { }

public abstract class RequirementTask<TParent> : RequirementMetadata
    where TParent : RequirementMetadata { }
```

`Feature<TParent>` requires `TParent : Epic`. This means:

```csharp
// Compiles: a feature belongs to an epic
public abstract class OrderFeature : Feature<DomainEpic> { /* ... */ }

// Does NOT compile: a feature cannot belong to a story
public abstract class OrderFeature : Feature<SomeStory> { /* ... */ }
//                                          ~~~~~~~~~ CS0311: constraint violation
```

This is not a naming convention. It is not a runtime check. It is not a linter rule. The compiler itself rejects invalid hierarchies. You cannot accidentally create a `Feature` that belongs to a `Bug`, or a `Story` that belongs to a `RequirementTask`.

The unparameterized `Feature` (no generic argument) exists for root-level features that do not belong to any epic:

```csharp
public abstract class Feature : RequirementMetadata { }
```

This is an explicit design choice: root-level features are a separate type, not a `Feature<Nothing>`.

## The Unbreakable Chain

The chain from requirement to test is enforced at every link. Each layer forces the existence of the next.

### Add an AC -> analyzer warns: no spec method

Define a new acceptance criterion:

```csharp
public abstract class OrderFeature : Feature<DomainEpic>
{
    // existing:
    public abstract AcceptanceCriterionResult UserCanPlaceOrder(UserId user, ResourceId product);
    // new:
    public abstract AcceptanceCriterionResult UserCanTrackOrder(UserId user, ResourceId order);
}
```

The `RequirementCoverageAnalyzer` (REQ100/REQ101) detects that `UserCanTrackOrder` has no corresponding specification method. Warning at compile time.

### Add a spec method -> compiler forces: implement the interface

Add the spec:

```csharp
[ForRequirement(typeof(OrderFeature))]
public interface IOrderService
{
    Task<OrderResult> PlaceOrderAsync(UserId user, ResourceId product);
    Task<TrackingInfo> TrackOrderAsync(UserId user, ResourceId order); // new
}
```

Every class implementing `IOrderService` now has a compile error: `'OrderService' does not implement interface member 'IOrderService.TrackOrderAsync'`. The compiler, not a linter, forces the implementation.

### Implement the interface -> analyzer warns: no test

The implementation exists but REQ300/REQ301 warns: `UserCanTrackOrder` has no `[Verifies]` test method.

### Delete an AC -> compile error everywhere

Remove `UserCanPlaceOrder` from the abstract class. Every `nameof(OrderFeature.UserCanPlaceOrder)` in every `[Verifies]` attribute across the entire codebase becomes a compile error. Every `[ForRequirement(typeof(OrderFeature), nameof(OrderFeature.UserCanPlaceOrder))]` on every implementation method becomes a compile error. You cannot delete a requirement without confronting every piece of code that references it.

This is the fundamental difference from string-based systems. In Jira, deleting a ticket is a click. The code does not know. Here, deleting a requirement is a deliberate act that the compiler forces you to propagate through every layer.

## Why Not Jira / Linear / Azure DevOps / GitHub Issues

These tools track work. They answer: Who is doing what? When is it due? What is the status?

They do not track behavior. A Jira ticket titled "User can assign roles" says nothing about:

- What "user" means (any user? an admin? a user with a specific role?)
- What "assign" means (add a role? replace all roles? toggle a role?)
- What "role" means (a string? an enum? a domain concept with permissions?)

An abstract method says all three:

```csharp
public abstract AcceptanceCriterionResult AdminCanAssignRoles(
    UserId actingUser, UserId targetUser, RoleId role);
```

- "User" is a `UserId` -- specifically the acting user, who is an admin (the method name says so).
- "Assign" means: given an acting user, a target user, and a role, produce a verifiable result.
- "Role" is a `RoleId` -- a typed domain concept, not a string.

This DSL does not replace Jira. Jira tracks work (who, when, status). This DSL tracks behavior (what, precisely, the system must do). They are complementary. A Jira ticket can reference `typeof(OrderFeature)` in its description. But the source of truth for what the system must do lives in the code, where the compiler can enforce it.

## Why Lightweight Domain Concepts

The `FrenchExDev.Net.Requirements` project defines value types:

```csharp
public readonly struct UserId
{
    public Guid Value { get; }
    public string FirstName { get; }
    public string LastName { get; }
    public Email Email { get; }
}

public readonly struct RoleId     { public string Value { get; } }
public readonly struct Email      { public string Value { get; } }
public readonly struct ResourceId { public string Value { get; } }
public readonly struct TokenId    { public Guid Value { get; } }
```

These live in the Requirements project for a specific reason: AC method signatures must be self-contained. If `AdminCanAssignRoles` takes a `UserId`, the definition of `UserId` must live where `AdminCanAssignRoles` is defined -- in the Requirements project.

This creates a deliberate dependency direction:

```
Requirements (defines UserId, RoleId, etc.)
    ^
    |
SharedKernel (references Requirements, reuses the same types)
    ^
    |
Domain / Application (uses SharedKernel types in implementations)
```

The domain concepts are intentionally lightweight -- readonly structs with no behavior, no validation, no dependencies. They define shape, not rules. Validation logic belongs in the implementation layer.

This means an AC signature like:

```csharp
public abstract AcceptanceCriterionResult AdminCanAssignRoles(
    UserId actingUser, UserId targetUser, RoleId role);
```

...uses the exact same `UserId` and `RoleId` types that the implementation layer uses. The AC signature and the implementation signature speak the same language. There is no mapping layer, no impedance mismatch, no "translate from requirement types to domain types" step.

## The Bootstrap Question

The Requirements DSL can track its own requirements.

```csharp
public abstract class RequirementsDslEpic : Epic
{
    public override string Title => "Requirements DSL";
    public override RequirementPriority Priority => RequirementPriority.Critical;
    public override string Owner => "platform-team";
}

public abstract class TypeSafeTrackingFeature : Feature<RequirementsDslEpic>
{
    public override string Title => "Type-Safe Requirement Tracking";
    public override RequirementPriority Priority => RequirementPriority.Critical;
    public override string Owner => "platform-team";

    public abstract AcceptanceCriterionResult RequirementsAreTypes();
    public abstract AcceptanceCriterionResult AcceptanceCriteriaAreAbstractMethods();
    public abstract AcceptanceCriterionResult CompilerCatchesBrokenLinks();
    public abstract AcceptanceCriterionResult IdeNavigatesTheChain();
}
```

This is not circular. It is the same property that makes a language self-hosting: a compiler written in its own language compiles itself. A requirements DSL that tracks its own requirements validates itself.

If you can define `AcceptanceCriteriaAreAbstractMethods` as an abstract method in the Requirements DSL, and then write a `[Verifies(typeof(TypeSafeTrackingFeature), nameof(TypeSafeTrackingFeature.AcceptanceCriteriaAreAbstractMethods))]` test that proves it works -- the DSL has demonstrated its own core claim. The test proves that acceptance criteria are abstract methods by being verified through an abstract method that is an acceptance criterion.

This self-referential quality is not a curiosity. It is a correctness argument. If the DSL could not track its own requirements, that would be evidence that it is incomplete -- there would exist a class of requirements that it cannot express. The fact that it can track itself means there is no fundamental limitation in its expressiveness for behavioral requirements.
