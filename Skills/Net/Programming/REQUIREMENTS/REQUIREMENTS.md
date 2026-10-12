# REQUIREMENTS — Requirements

Non-negotiable rules for the Requirements DSL.

## Requirement Types

- **Requirements must be abstract classes.** Never concrete. They carry metadata (`Title`, `Priority`, `Owner`) and abstract AC methods.
- **Generic parent constraints are mandatory for hierarchy.** `Feature<TParent> where TParent : Epic`, `Story<TParent> where TParent : RequirementMetadata`. Use standalone `Feature` (no parent) only for orphan features with no epic.

## Acceptance Criteria

- **ACs must be abstract methods returning `AcceptanceCriterionResult`.** Never properties, never strings, never indices.
- **AC parameters must use domain concept types.** `UserId`, `Email`, `ResourceId`, `RoleId`, `TokenId` — not raw `string` or `Guid`. This makes signatures self-documenting.
- **`AcceptanceCriterionResult` must never throw.** Use `Satisfied()` or `Failed(reason)`. Same philosophy as `Result<T>` in the DDD skill.

## Traceability Attributes

- **All 3 attributes must have `[MetaConcept]` companions.** `ForRequirementConcept`, `VerifiesConcept`, `TestsForConcept`. No naked attributes.
- **`[ForRequirement]` on specifications must target interfaces.** Implementation classes also get `[ForRequirement]` but the spec contract is the interface.
- **`[Verifies]` must reference a real AC method via `nameof()`.** Never a string literal. The analyzer (REQ302) will flag non-existent references.
- **`[TestsFor]` goes on the test class.** `[Verifies]` goes on individual `[Fact]` methods. Both use `typeof()` for compile-time safety.

## Domain Concepts

- **Domain concept types must be `readonly struct`.** `Email`, `UserId`, `RoleId`, `ResourceId`, `TokenId`. Zero allocation, value semantics, no nullability surprises.
- **New domain concepts go in `DomainConcepts.cs`.** Keep them in the core project (`FrenchExDev.Net.Requirements`).

## Source Generator

- **`RequirementRegistryGenerator` follows the 2-step SG pattern.** Extraction in `.SourceGenerator`, emission in `.SourceGenerator.Lib`. No Roslyn types leak into the emitter.
- **Generated `RequirementRegistry` must be static.** `RequirementRegistry.All` returns `IReadOnlyDictionary<Type, RequirementInfo>`.

## Analyzers

- **All REQ100–REQ302 diagnostics must be implemented.** Gaps in the traceability chain must be flagged as warnings or errors.
- **REQ*02 diagnostics (references to non-existent things) are Errors.** They indicate broken compile-time links that must be fixed.
- **REQ*00 and REQ*01 diagnostics are Warnings.** They indicate missing coverage, not broken code.
