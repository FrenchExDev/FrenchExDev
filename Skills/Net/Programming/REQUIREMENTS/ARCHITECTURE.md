# REQUIREMENTS — Architecture

## 7-Project Decomposition

| Project | Target | Purpose |
|---------|--------|---------|
| `FrenchExDev.Net.Requirements` | `netstandard2.0;net10.0` | Core types: `RequirementMetadata`, `Epic`, `Feature<T>`, `Story<T>`, `RequirementTask<T>`, `Bug`, `AcceptanceCriterionResult`, domain concepts |
| `FrenchExDev.Net.Requirements.Attributes` | `netstandard2.0;net10.0` | `[ForRequirement]`, `[Verifies]`, `[TestsFor]` + MetaConcept companions |
| `FrenchExDev.Net.Requirements.SourceGenerator` | `netstandard2.0` | `RequirementRegistryGenerator` (IIncrementalGenerator) |
| `FrenchExDev.Net.Requirements.SourceGenerator.Lib` | `netstandard2.0` | `RequirementRegistryEmitter` + `RequirementEmitModel` (Roslyn-free) |
| `FrenchExDev.Net.Requirements.Analyzers` | `netstandard2.0` | `RequirementCoverageAnalyzer` (REQ100–REQ302 diagnostics) |
| `FrenchExDev.Net.Requirements.Testing` | `netstandard2.0;net10.0` | Sample requirement types for tests and examples |
| `FrenchExDev.Net.Requirements.Tests` | `net10.0` | xUnit tests |

## Type Hierarchy

```
RequirementMetadata (abstract base)
  |-- Epic                            (top-level, no parent)
  |-- Feature<TParent : Epic>         (must live under an Epic)
  |-- Feature                         (standalone, no parent)
  |-- Story<TParent : RequirementMetadata>     (attaches to any requirement)
  |-- RequirementTask<TParent : RequirementMetadata>  (adds EstimatedHours)
  +-- Bug                             (standalone, adds Severity)
```

All requirement types are abstract. Properties: `Title`, `Priority`, `Owner`.

## Attribute Taxonomy

| Attribute | Target | AllowMultiple | Properties | MetaConcept |
|-----------|--------|---------------|------------|-------------|
| `[ForRequirement(typeof(T))]` | Class, Interface, Method | Yes | `RequirementType`, `AcceptanceCriterion?` | `ForRequirementConcept` |
| `[Verifies(typeof(T), nameof(AC))]` | Method | Yes | `RequirementType`, `AcceptanceCriterionName` | `VerifiesConcept` |
| `[TestsFor(typeof(T))]` | Class | Yes | `RequirementType` | `TestsForConcept` |

## 4-Layer Traceability Chain

```
Layer 1: REQUIREMENT (abstract class)
  public abstract class OrderFeature : Feature<DomainEpic>
  {
      public abstract AcceptanceCriterionResult UserCanPlaceOrder(UserId user, ResourceId product);
  }

Layer 2: SPECIFICATION (interface + [ForRequirement])
  [ForRequirement(typeof(OrderFeature))]
  public interface IOrderService
  {
      [ForRequirement(typeof(OrderFeature), nameof(OrderFeature.UserCanPlaceOrder))]
      Task<OrderResult> PlaceOrderAsync(UserId user, ResourceId product);
  }

Layer 3: IMPLEMENTATION (class + [ForRequirement])
  [ForRequirement(typeof(OrderFeature))]
  public class OrderService : IOrderService
  {
      [ForRequirement(typeof(OrderFeature), nameof(OrderFeature.UserCanPlaceOrder))]
      public Task<OrderResult> PlaceOrderAsync(UserId user, ResourceId product) { ... }
  }

Layer 4: TESTS (class + [TestsFor], method + [Verifies])
  [TestsFor(typeof(OrderFeature))]
  public class OrderFeatureTests
  {
      [Fact]
      [Verifies(typeof(OrderFeature), nameof(OrderFeature.UserCanPlaceOrder))]
      public async Task User_can_place_order() { ... }
  }
```

## RequirementRegistryGenerator

Scans abstract classes deriving from the requirement hierarchy:
1. Walk base type chain to detect kind (Epic, Feature, Story, Task, Bug)
2. Extract parent type from generic type argument
3. Extract title from `Title` property override
4. Collect abstract methods returning `AcceptanceCriterionResult` as ACs
5. Emit `RequirementRegistry.g.cs` with `RequirementInfo` per type

Follows the 2-step SG pattern: `.SourceGenerator` extracts, `.SourceGenerator.Lib` emits.

## Analyzer Diagnostics

| ID | Severity | Description |
|----|----------|-------------|
| REQ100 | Warning | Feature has no specification (`[ForRequirement]` interface missing) |
| REQ101 | Warning | AC has no specification method |
| REQ102 | Error | Spec references non-existent requirement type |
| REQ200 | Warning | Spec has no implementation |
| REQ201 | Error | Implementation references non-existent AC |
| REQ202 | Warning | Implementation method not linked to any AC |
| REQ300 | Warning | Requirement has no test class (`[TestsFor]` missing) |
| REQ301 | Warning | AC has no `[Verifies]` test method |
| REQ302 | Error | `[Verifies]` references non-existent AC |

See: `Requirements/src/FrenchExDev.Net.Requirements.Analyzers/RequirementCoverageAnalyzer.cs`

## Key Files

- `Requirements/src/FrenchExDev.Net.Requirements/RequirementMetadata.cs` — base class + hierarchy
- `Requirements/src/FrenchExDev.Net.Requirements/AcceptanceCriterionResult.cs` — Satisfied/Failed result
- `Requirements/src/FrenchExDev.Net.Requirements/DomainConcepts.cs` — UserId, Email, etc.
- `Requirements/src/FrenchExDev.Net.Requirements.Attributes/ForRequirementAttribute.cs`
- `Requirements/src/FrenchExDev.Net.Requirements.Attributes/VerifiesAttribute.cs`
- `Requirements/src/FrenchExDev.Net.Requirements.Attributes/TestsForAttribute.cs`
- `Requirements/src/.../SourceGenerator/RequirementRegistryGenerator.cs`
- `Requirements/src/.../Analyzers/RequirementCoverageAnalyzer.cs`
- `Requirements/README.md` — canonical documentation
