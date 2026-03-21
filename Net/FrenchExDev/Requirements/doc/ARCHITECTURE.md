# Requirements -- Architecture

## 1. The Type-Safe Chain

The Requirements DSL enforces a five-layer traceability chain at compile time:

```
Requirements  ->  Specifications  ->  Implementation  ->  Tests  ->  Quality Gates
  (types)        (interfaces)        (classes)           (xUnit)    (analyzers)
```

Every link in the chain uses `typeof()` and `nameof()` -- actual C# compiler-checked references, not strings, not ticket IDs. If a requirement class is renamed, every specification, implementation, and test that references it becomes a compile error.

```
Layer 1: Requirement     abstract class RbacFeature : Feature<PlatformEpic>
                           abstract AcceptanceCriterionResult UserCanAssignRole(UserId user, RoleId role);

Layer 2: Specification   [ForRequirement(typeof(RbacFeature), nameof(RbacFeature.UserCanAssignRole))]
                         Task AssignRoleAsync(UserId user, RoleId role);

Layer 3: Implementation  [ForRequirement(typeof(RbacFeature))]
                         class RbacService : IRbacSpec { ... }

Layer 4: Tests           [TestsFor(typeof(RbacFeature))]
                         class RbacFeatureTests
                         {
                             [Verifies(typeof(RbacFeature), nameof(RbacFeature.UserCanAssignRole))]
                             public void AssignRole_works() { ... }
                         }

Layer 5: Quality Gates   REQ100-REQ302 analyzers detect gaps in the chain
```

The key insight: `typeof(RbacFeature)` is a compile-time reference. Rename `RbacFeature` and every `[ForRequirement(typeof(RbacFeature))]` in the codebase fails to compile. `nameof(RbacFeature.UserCanAssignRole)` does the same for individual acceptance criteria. There is no runtime resolution, no string matching, no stale references that silently pass.

---

## 2. Requirement Hierarchy

All requirements share a single abstract root and branch through generic constraints:

```csharp
public abstract class RequirementMetadata
{
    public abstract string Title { get; }
    public abstract RequirementPriority Priority { get; }
    public abstract string Owner { get; }
}

public abstract class Epic : RequirementMetadata { }

public abstract class Feature<TParent> : RequirementMetadata where TParent : Epic { }

public abstract class Feature : RequirementMetadata { }

public abstract class Story<TParent> : RequirementMetadata where TParent : RequirementMetadata { }

public abstract class RequirementTask<TParent> : RequirementMetadata where TParent : RequirementMetadata
{
    public abstract int EstimatedHours { get; }
}

public abstract class Bug : RequirementMetadata
{
    public abstract BugSeverity Severity { get; }
}
```

### Hierarchy rules enforced by the compiler

| Type | Generic constraint | Meaning |
|---|---|---|
| `Epic` | none | Top-level grouping, no parent |
| `Feature<TParent>` | `where TParent : Epic` | Must live under an Epic |
| `Feature` | none | Standalone feature, no parent required |
| `Story<TParent>` | `where TParent : RequirementMetadata` | Can live under any requirement |
| `RequirementTask<TParent>` | `where TParent : RequirementMetadata` | Can live under any requirement |
| `Bug` | none | Standalone defect report |

`Feature<TParent>` constrains `TParent : Epic` -- the compiler rejects `Feature<SomeStory>`. `Story<TParent>` and `RequirementTask<TParent>` constrain `TParent : RequirementMetadata` -- they can attach anywhere in the tree.

### Acceptance criteria are abstract methods

Any abstract method on a requirement class that returns `AcceptanceCriterionResult` is an acceptance criterion. The source generator discovers these by inspecting the symbol's members. Parameters use domain concept types (`UserId`, `RoleId`, etc.) to make the criterion self-documenting:

```csharp
public abstract AcceptanceCriterionResult UserCanAssignRole(UserId user, RoleId role);
```

### Enums

```csharp
public enum RequirementPriority { Critical, High, Medium, Low, Backlog }
public enum BugSeverity { Critical, Major, Minor, Cosmetic }
```

---

## 3. AcceptanceCriterionResult

A readonly struct that represents whether an acceptance criterion is met:

```csharp
public readonly struct AcceptanceCriterionResult : IEquatable<AcceptanceCriterionResult>
{
    public bool IsSatisfied { get; }
    public string FailureReason { get; }

    private AcceptanceCriterionResult(bool satisfied, string failureReason) { ... }

    public static AcceptanceCriterionResult Satisfied() => new(true, string.Empty);
    public static AcceptanceCriterionResult Failed(string reason) => new(false, reason);

    public static implicit operator bool(AcceptanceCriterionResult r) => r.IsSatisfied;
    // == , !=, Equals, GetHashCode -- full value equality
}
```

### Why a struct, not a class

1. **No heap allocation.** Acceptance criteria are checked frequently in tests. A struct lives on the stack.
2. **Value semantics.** `Satisfied() == Satisfied()` is true without reference identity. Two results with the same `IsSatisfied` and `FailureReason` are equal.
3. **No null.** A `default(AcceptanceCriterionResult)` is `IsSatisfied = false, FailureReason = null` -- a failed result, which is the safe default.

### Factory methods

The constructor is private. Callers must go through `Satisfied()` or `Failed(reason)`. This makes intent explicit at call sites and prevents construction of a "satisfied with a failure reason" state.

### Implicit bool

```csharp
AcceptanceCriterionResult result = ...;
if (result) { /* satisfied */ }
Assert.True(result);
```

The implicit conversion to `bool` returns `IsSatisfied`, so results work directly in conditionals and xUnit assertions.

### Value equality

`IEquatable<AcceptanceCriterionResult>` plus `==`/`!=` operators. Equality is defined as `IsSatisfied` equality AND `FailureReason` string equality. `GetHashCode` combines both fields.

---

## 4. Attribute Architecture

Three attributes form the cross-cutting links between requirements, specifications, implementations, and tests.

### ForRequirementAttribute

```csharp
[MetaConcept(typeof(ForRequirementConcept))]
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface | AttributeTargets.Method, AllowMultiple = true)]
public sealed class ForRequirementAttribute : Attribute
{
    public Type RequirementType { get; }
    public string? AcceptanceCriterion { get; }

    public ForRequirementAttribute(Type requirementType, string? acceptanceCriterion = null) { ... }
}
```

- **Targets:** classes (implementations), interfaces (specifications), methods (individual spec/impl methods)
- **AllowMultiple:** true -- a single method can satisfy multiple acceptance criteria
- **AcceptanceCriterion:** optional -- when null, links to the requirement as a whole; when set, links to a specific AC method by name

### VerifiesAttribute

```csharp
[MetaConcept(typeof(VerifiesConcept))]
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class VerifiesAttribute : Attribute
{
    public Type RequirementType { get; }
    public string AcceptanceCriterionName { get; }

    public VerifiesAttribute(Type requirementType, string acceptanceCriterionName) { ... }
}
```

- **Targets:** methods only -- each test method verifies a specific acceptance criterion
- **AllowMultiple:** true -- one test can verify multiple ACs
- **AcceptanceCriterionName:** required (not optional) -- `[Verifies]` always points at a specific AC

### TestsForAttribute

```csharp
[MetaConcept(typeof(TestsForConcept))]
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class TestsForAttribute : Attribute
{
    public Type RequirementType { get; }

    public TestsForAttribute(Type requirementType) { ... }
}
```

- **Targets:** classes only -- marks a test class as the test suite for a requirement
- **AllowMultiple:** true -- a test class can cover multiple requirements

### MetaConcept decoration

All three attributes carry `[MetaConcept(typeof(...))]`, linking them to their corresponding concept classes (`ForRequirementConcept`, `VerifiesConcept`, `TestsForConcept`). These concept classes extend `FrenchExDev.Net.Dsl.MetaConcept` and carry the concept's `Name` and `AttributeType`. This makes the attributes discoverable and validatable by the DSL infrastructure.

```
ForRequirementAttribute  -->  [MetaConcept(typeof(ForRequirementConcept))]
VerifiesAttribute        -->  [MetaConcept(typeof(VerifiesConcept))]
TestsForAttribute        -->  [MetaConcept(typeof(TestsForConcept))]
```

---

## 5. Source Generator: RequirementRegistryGenerator

`RequirementRegistryGenerator` is an `IIncrementalGenerator` that discovers requirement types and emits a runtime registry.

### Discovery pipeline

```csharp
[Generator(LanguageNames.CSharp)]
public sealed class RequirementRegistryGenerator : IIncrementalGenerator
```

**Step 1 -- Syntax filter.** The `CreateSyntaxProvider` predicate selects `ClassDeclarationSyntax` nodes that have an `abstract` modifier. This is cheap and runs on every keystroke.

```csharp
predicate: static (node, _) => node is ClassDeclarationSyntax cds
    && cds.Modifiers.Any(m => m.Text == "abstract"),
```

**Step 2 -- Semantic transform (`ExtractRequirement`).** For each candidate, the generator:

1. Gets the `INamedTypeSymbol` and confirms `IsAbstract`.
2. Walks the inheritance chain (`symbol.BaseType`, then `baseType.BaseType`, etc.) looking for the known base classes.
3. Matches on `OriginalDefinition.ToDisplayString()` using `StartsWith`:
   - `FrenchExDev.Net.Requirements.Epic` -> Kind = `Epic`
   - `FrenchExDev.Net.Requirements.Feature<` -> Kind = `Feature`, extracts `TypeArguments[0]` as parent
   - `FrenchExDev.Net.Requirements.Feature` -> Kind = `Feature`, no parent
   - `FrenchExDev.Net.Requirements.Story<` -> Kind = `Story`, extracts parent
   - `FrenchExDev.Net.Requirements.RequirementTask<` -> Kind = `Task`, extracts parent
   - `FrenchExDev.Net.Requirements.Bug` -> Kind = `Bug`
4. Returns `null` (filtered out) if no known base is found.

### Acceptance criteria extraction

The generator iterates `symbol.GetMembers().OfType<IMethodSymbol>()` and collects every method where `IsAbstract == true` and `ReturnType.Name == "AcceptanceCriterionResult"`. The method names become the AC list.

### Title extraction

The generator looks for a property override named `Title`. If it finds a `PropertyDeclarationSyntax` with an `ExpressionBody` containing a `LiteralExpressionSyntax`, it extracts the string literal's `ValueText`. Otherwise, it falls back to the class name.

### Output

The collected `RequirementEmitModel` list is passed to `RequirementRegistryEmitter.Emit()` to produce `RequirementRegistry.g.cs`.

---

## 6. Emission: RequirementRegistryEmitter

`RequirementRegistryEmitter` lives in `FrenchExDev.Net.Requirements.SourceGenerator.Lib` -- a plain `netstandard2.0` library with **no Roslyn dependency**. This separation means the emission logic can be tested without loading the Roslyn workspace.

### RequirementEmitModel

```csharp
public sealed class RequirementEmitModel
{
    public string TypeFullName { get; set; } = "";
    public string Kind { get; set; } = "";
    public string Title { get; set; } = "";
    public string? ParentTypeFullName { get; set; }
    public List<string> AcceptanceCriteria { get; set; } = new List<string>();
}
```

### Emitted code structure

`RequirementRegistryEmitter.Emit()` produces a single file in namespace `FrenchExDev.Net.Requirements.Generated` containing:

**RequirementRegistry** -- a static class with a single property:

```csharp
public static IReadOnlyDictionary<Type, RequirementInfo> All { get; }
```

The dictionary is initialized inline with one entry per discovered requirement:

```csharp
[typeof(MyApp.Requirements.RbacFeature)] = new RequirementInfo(
    typeof(MyApp.Requirements.RbacFeature),
    RequirementKind.Feature,
    "RBAC feature",
    typeof(MyApp.Requirements.PlatformEpic),
    new string[] { "UserCanAssignRole", "UserCanRevokeRole" }),
```

**RequirementInfo** -- a sealed class holding:

| Property | Type | Description |
|---|---|---|
| `Type` | `Type` | The requirement's CLR type |
| `Kind` | `RequirementKind` | Epic, Feature, Story, Task, or Bug |
| `Title` | `string` | Extracted from the `Title` property override |
| `Parent` | `Type?` | The generic type argument (e.g., the Epic for a Feature), or null |
| `AcceptanceCriteria` | `string[]` | Names of abstract methods returning `AcceptanceCriterionResult` |

**RequirementKind** -- an enum:

```csharp
public enum RequirementKind { Epic, Feature, Story, Task, Bug }
```

---

## 7. Analyzers: REQ1xx - REQ3xx

The analyzers enforce chain completeness. They are grouped by the layer they check:

### REQ1xx -- Requirements to Specifications

| ID | Description |
|---|---|
| REQ100 | Feature has no specification -- no `[ForRequirement(typeof(Feature))]` on any interface |
| REQ101 | Acceptance criterion has no specification method -- no `[ForRequirement(typeof(Feature), nameof(Feature.AC))]` on any interface method |

### REQ2xx -- Specifications to Implementations

| ID | Description |
|---|---|
| REQ200 | Specification has no implementation -- no class implements the `[ForRequirement]`-decorated interface |

### REQ3xx -- Requirements to Tests

| ID | Description |
|---|---|
| REQ300 | Feature has no tests -- no `[TestsFor(typeof(Feature))]` on any test class |
| REQ301 | Acceptance criterion has no `[Verifies]` test -- no test method carries `[Verifies(typeof(Feature), nameof(Feature.AC))]` |
| REQ302 | Stale reference -- a `[ForRequirement]`, `[TestsFor]`, or `[Verifies]` references a type or method that no longer exists |

### Current state

`RequirementCoverageAnalyzer` is implemented as a placeholder. It registers `REQ100` in `SupportedDiagnostics` and configures the analysis context (excluding generated code, enabling concurrent execution), but the `Initialize` method does not yet register symbol/syntax actions. The full implementation is planned for a future phase.

```csharp
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class RequirementCoverageAnalyzer : DiagnosticAnalyzer
{
    public static readonly DiagnosticDescriptor REQ100 = new DiagnosticDescriptor(
        id: "REQ100",
        title: "Feature has no specification",
        messageFormat: "{0} has {1} acceptance criteria but no specification interface references it",
        category: "Requirements",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);
    // ...
}
```

---

## 8. Project Dependencies

```
FrenchExDev.Net.Dsl                         (external, netstandard2.0;net10.0)
    ^
    |
    +-- FrenchExDev.Net.Requirements         (netstandard2.0;net10.0)
    |       ^
    |       +-- FrenchExDev.Net.Requirements.Testing    (netstandard2.0;net10.0)
    |
    +-- FrenchExDev.Net.Requirements.Attributes   (netstandard2.0;net10.0)


FrenchExDev.Net.Requirements.SourceGenerator.Lib   (netstandard2.0, no deps)
    ^
    |
    +-- FrenchExDev.Net.Requirements.SourceGenerator   (netstandard2.0, Roslyn)


FrenchExDev.Net.Requirements.Analyzers             (netstandard2.0, Roslyn)


FrenchExDev.Net.Requirements.Tests                 (net10.0)
    +-- Requirements
    +-- Requirements.Attributes
    +-- Requirements.Testing
    +-- Requirements.SourceGenerator       (Analyzer reference)
    +-- Requirements.SourceGenerator.Lib   (Analyzer reference)
    +-- Requirements.Analyzers             (Analyzer reference)
```

Key observations:

- **Requirements** depends only on **Dsl**. Zero other external dependencies.
- **Requirements.Attributes** depends only on **Dsl** (for `MetaConceptAttribute` and `MetaConcept`).
- **SourceGenerator.Lib** has **no dependencies at all** -- pure `netstandard2.0`, no Roslyn, no Dsl.
- **SourceGenerator** depends on Roslyn (`Microsoft.CodeAnalysis.CSharp`) and **SourceGenerator.Lib**.
- **Analyzers** depends on Roslyn only.
- **Testing** depends on **Requirements** (to provide sample types like `SampleEpic`, `SampleFeature`, `SampleStory`).
- The test project wires the source generator and analyzers via `OutputItemType="Analyzer" ReferenceOutputAssembly="false"`.

---

## 9. Design Decisions

### Why abstract classes, not records

The project targets `netstandard2.0`. Records are a C# 9 feature that requires `net5.0+` for the `IsExternalInit` hack or a polyfill. Abstract classes work everywhere, including older .NET Framework consumers. They also naturally model the hierarchy: records cannot be abstract base types with derived sealed records in netstandard2.0 without gymnastics.

### Why RequirementTask, not Task

`System.Threading.Tasks.Task` is one of the most commonly imported types in any C# project. Naming the requirement class `Task` would cause ambiguity errors in every file that uses `async/await`. `RequirementTask<TParent>` avoids the clash entirely while remaining descriptive.

### Why domain concepts live in the Requirements project

Types like `UserId`, `RoleId`, `Email`, `ResourceId`, and `TokenId` are defined directly in `FrenchExDev.Net.Requirements`. This keeps acceptance criterion signatures self-contained -- a requirement class file is fully readable without chasing imports across projects. The Requirements project has zero dependencies beyond Dsl, so these domain concepts add no coupling.

### Why readonly structs for domain concepts

`UserId`, `RoleId`, `Email`, `ResourceId`, `TokenId` are all `readonly struct`. They exist purely for type safety in AC signatures -- to prevent passing a `string` user ID where a `RoleId` is expected. They carry no behavior, no validation, no inheritance. Structs are the right fit: value semantics, no allocation, no null.

### Why three separate attributes instead of one

`ForRequirementAttribute`, `VerifiesAttribute`, and `TestsForAttribute` could theoretically be a single attribute with a "usage" discriminator. Three separate attributes give:

1. **Different `AttributeTargets`:** `ForRequirement` goes on classes, interfaces, and methods. `TestsFor` goes on classes only. `Verifies` goes on methods only. A single attribute would need `AttributeTargets.All` and lose compile-time target validation.
2. **Different required parameters:** `Verifies` requires `AcceptanceCriterionName` (not optional). `ForRequirement` has it optional. A single attribute with optional parameters would allow invalid combinations.
3. **Distinct MetaConcepts:** Each attribute maps to its own `MetaConcept` subclass, enabling independent DSL validation rules per concept.

### Why AllowMultiple = true on all attributes

A single interface method may satisfy ACs from multiple requirements. A single test class may cover multiple features. A single test method may verify multiple ACs. `AllowMultiple = true` allows all of these without workarounds.

### Why the SourceGenerator.Lib separation

The emitter (`RequirementRegistryEmitter`) is a plain class that takes `IReadOnlyList<RequirementEmitModel>` and returns a `string`. It has no Roslyn dependency. This means:
1. It can be unit-tested without loading a Roslyn workspace.
2. It can be reused by other generators (the same pattern used by `BuilderEmitter` in the Builder project).
3. The Roslyn-dependent generator assembly stays small, reducing analyzer load time.
