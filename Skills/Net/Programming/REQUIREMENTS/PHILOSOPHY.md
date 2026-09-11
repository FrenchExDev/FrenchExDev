# REQUIREMENTS — Philosophy

## Type-Safe Over String-Based

Requirements are expressed as **C# types**, not ticket IDs or string labels. Every traceability link uses `typeof()` and `nameof()`:

```csharp
[Verifies(typeof(OrderFeature), nameof(OrderFeature.UserCanPlaceOrder))]
public async Task User_can_place_order() { ... }
```

Why: Typos are compile errors. Renames propagate automatically via IDE refactor. Deletions are caught at every reference site. No stale links, no ghost references. Full IDE navigation (Ctrl+Click on requirement type).

Compare with string-based: `"PROJ-142"` typos compile silently, renaming a requirement doesn't update test tags, deleting a requirement leaves orphaned references.

## Requirements Are Abstract Classes, Not Data

Requirements are modeled as **abstract classes** in a hierarchy, not as database records or YAML:

```csharp
public abstract class OrderFeature : Feature<DomainEpic>
{
    public override string Title => "Order Placement";
    public override RequirementPriority Priority => RequirementPriority.High;
    public abstract AcceptanceCriterionResult UserCanPlaceOrder(UserId user, ResourceId product);
}
```

The hierarchy is enforced by generic constraints:
- `Feature<TParent> where TParent : Epic` — features live under epics
- `Story<TParent> where TParent : RequirementMetadata` — stories attach to any requirement
- `RequirementTask<TParent> where TParent : RequirementMetadata` — tasks with estimated hours
- `Bug` — standalone defect with severity

## Acceptance Criteria Are Methods

ACs are **abstract methods** returning `AcceptanceCriterionResult`, not strings or checklists. Parameters use domain concept types (`UserId`, `Email`, `ResourceId`) to make signatures self-documenting.

Why methods: they are refactorable, navigable, and discoverable by the source generator. `nameof(UserCanPlaceOrder)` is stable across renames — `nameof()` updates automatically.

## 4-Layer Traceability Chain

```
Requirements  -->  Specifications  -->  Implementation  -->  Tests
  (types)         ([ForRequirement]    ([ForRequirement]    ([TestsFor] +
                   interfaces)          classes)             [Verifies])
```

Every link is a compile-time reference. The `RequirementCoverageAnalyzer` (REQ100–REQ302) detects gaps at each layer.

See: `Requirements/README.md`, `Requirements/doc/PHILOSOPHY.md`

## MetaConcept Integration

All 3 traceability attributes have `[MetaConcept]` companions:
- `ForRequirementConcept` — concept for requirement-to-spec/impl links
- `VerifiesConcept` — concept for test-to-AC links
- `TestsForConcept` — concept for test-class-to-requirement links

This makes requirements discoverable by the Dsl framework's concept system, enabling cross-DSL validation.

## Domain Concepts Are Lightweight Value Types

`Email`, `UserId`, `RoleId`, `ResourceId`, `TokenId` are `readonly struct` types. Zero external dependencies, zero allocation overhead, value semantics. They exist to make AC signatures type-safe and self-documenting.

See: `Requirements/src/FrenchExDev.Net.Requirements/DomainConcepts.cs`

## AcceptanceCriterionResult: Never Throw

`AcceptanceCriterionResult` follows the same philosophy as `Result<T>` in DDD — express success/failure as values, never exceptions:

```csharp
AcceptanceCriterionResult.Satisfied();
AcceptanceCriterionResult.Failed("Price must be positive");
```

Implicit bool conversion enables clean assertions in tests: `result.ShouldBeTrue()`.
