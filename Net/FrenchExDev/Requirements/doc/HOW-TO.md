# Requirements -- How-To Guide

Step-by-step guides using actual types from the implementation. Every example references real types from `FrenchExDev.Net.Requirements`.

---

## 1. Define an Epic

An Epic is the top-level grouping. It has no parent constraint.

```csharp
using FrenchExDev.Net.Requirements;

public abstract class PlatformScalabilityEpic : Epic
{
    public override string Title => "Platform scalability";
    public override RequirementPriority Priority => RequirementPriority.High;
    public override string Owner => "platform-team";
}
```

Three abstract properties must be overridden: `Title`, `Priority`, `Owner`. These come from `RequirementMetadata`, the root of the hierarchy.

`RequirementPriority` values: `Critical`, `High`, `Medium`, `Low`, `Backlog`.

The class must be `abstract`. The source generator discovers requirement types by looking for abstract classes that inherit from the known base types. A concrete class would not be picked up.

For reference, the Testing project provides `SampleEpic`:

```csharp
public abstract class SampleEpic : Epic
{
    public override string Title => "Sample Epic";
    public override RequirementPriority Priority => RequirementPriority.High;
    public override string Owner => "test-team";
}
```

---

## 2. Define a Feature with Acceptance Criteria

A Feature lives under an Epic. Acceptance criteria are abstract methods returning `AcceptanceCriterionResult`. Parameters use domain concept types to make each criterion self-documenting.

```csharp
using FrenchExDev.Net.Requirements;

public abstract class RbacFeature : Feature<PlatformScalabilityEpic>
{
    public override string Title => "Role-based access control";
    public override RequirementPriority Priority => RequirementPriority.Critical;
    public override string Owner => "security-team";

    public abstract AcceptanceCriterionResult UserCanAssignRole(UserId user, RoleId role);
    public abstract AcceptanceCriterionResult UserCanRevokeRole(UserId user, RoleId role);
    public abstract AcceptanceCriterionResult UnauthorizedUserIsRejected(UserId user, ResourceId resource);
}
```

The generic constraint `Feature<PlatformScalabilityEpic>` is enforced by the compiler -- `Feature<TParent>` requires `where TParent : Epic`. Passing a Story or Bug as the type argument would be a compile error.

Each `abstract AcceptanceCriterionResult` method is an acceptance criterion. The source generator collects these by checking `IsAbstract` and `ReturnType.Name == "AcceptanceCriterionResult"`.

For a standalone feature with no parent Epic, use the non-generic `Feature`:

```csharp
public abstract class StandaloneFeature : Feature
{
    public override string Title => "Standalone feature";
    public override RequirementPriority Priority => RequirementPriority.Medium;
    public override string Owner => "team-a";

    public abstract AcceptanceCriterionResult BasicFlowWorks(UserId user);
}
```

For reference, the Testing project provides `SampleFeature`:

```csharp
public abstract class SampleFeature : Feature<SampleEpic>
{
    public override string Title => "Sample Feature";
    public override RequirementPriority Priority => RequirementPriority.Medium;
    public override string Owner => "test-team";

    public abstract AcceptanceCriterionResult ThingWorks(UserId user);
    public abstract AcceptanceCriterionResult ThingHandlesErrors(UserId user, string badInput);
}
```

---

## 3. Define a Story Under a Feature

A Story attaches to any `RequirementMetadata` descendant via `Story<TParent>`. It can have its own acceptance criteria.

```csharp
using FrenchExDev.Net.Requirements;

public abstract class AssignRoleStory : Story<RbacFeature>
{
    public override string Title => "Assign role to user";
    public override RequirementPriority Priority => RequirementPriority.High;
    public override string Owner => "security-team";

    public abstract AcceptanceCriterionResult RoleAppearsInUserProfile(UserId user, RoleId role);
    public abstract AcceptanceCriterionResult AuditLogEntryCreated(UserId user, RoleId role);
}
```

`Story<TParent>` constrains `where TParent : RequirementMetadata`, so a Story can live under an Epic, a Feature, or even another Story.

For reference, the Testing project provides `SampleStory`:

```csharp
public abstract class SampleStory : Story<SampleFeature>
{
    public override string Title => "Sample Story";
    public override RequirementPriority Priority => RequirementPriority.Medium;
    public override string Owner => "test-team";

    public abstract AcceptanceCriterionResult SubTaskCompletes(UserId user);
}
```

---

## 4. Define a Bug

A Bug has no parent constraint but adds a required `Severity` property.

```csharp
using FrenchExDev.Net.Requirements;

public abstract class TokenExpirationBug : Bug
{
    public override string Title => "Token not invalidated after password change";
    public override RequirementPriority Priority => RequirementPriority.Critical;
    public override string Owner => "security-team";
    public override BugSeverity Severity => BugSeverity.Major;

    public abstract AcceptanceCriterionResult TokenIsInvalidatedOnPasswordChange(
        UserId user, TokenId token);
    public abstract AcceptanceCriterionResult ExpiredTokenReturns401(TokenId token);
}
```

`BugSeverity` values: `Critical`, `Major`, `Minor`, `Cosmetic`.

---

## 5. Define a Task

`RequirementTask<TParent>` attaches to any requirement and adds `EstimatedHours`.

```csharp
using FrenchExDev.Net.Requirements;

public abstract class SetupCiPipelineTask : RequirementTask<PlatformScalabilityEpic>
{
    public override string Title => "Set up CI pipeline for RBAC service";
    public override RequirementPriority Priority => RequirementPriority.Medium;
    public override string Owner => "devops-team";
    public override int EstimatedHours => 8;

    public abstract AcceptanceCriterionResult PipelineRunsOnPush();
    public abstract AcceptanceCriterionResult PipelineFailsOnTestFailure();
}
```

The class is named `RequirementTask`, not `Task`, to avoid conflict with `System.Threading.Tasks.Task`.

---

## 6. Link Specifications to Requirements

Specifications are interfaces decorated with `[ForRequirement]`. Each method maps to a specific acceptance criterion using `nameof()`.

```csharp
using FrenchExDev.Net.Requirements.Attributes;

[ForRequirement(typeof(RbacFeature))]
public interface IRbacSpec
{
    [ForRequirement(typeof(RbacFeature), nameof(RbacFeature.UserCanAssignRole))]
    Task AssignRoleAsync(UserId user, RoleId role);

    [ForRequirement(typeof(RbacFeature), nameof(RbacFeature.UserCanRevokeRole))]
    Task RevokeRoleAsync(UserId user, RoleId role);

    [ForRequirement(typeof(RbacFeature), nameof(RbacFeature.UnauthorizedUserIsRejected))]
    Task<bool> CheckAccessAsync(UserId user, ResourceId resource);
}
```

- The `[ForRequirement]` on the interface links the entire specification to the requirement.
- The `[ForRequirement(..., nameof(...))]` on each method links that method to a specific acceptance criterion.
- `nameof(RbacFeature.UserCanAssignRole)` is a compile-time reference. Renaming the AC method updates or breaks this reference.
- `AllowMultiple = true` means a method can satisfy multiple ACs if needed.

---

## 7. Link Implementations to Specifications

Implementation classes carry `[ForRequirement]` on the class and optionally on methods.

```csharp
using FrenchExDev.Net.Requirements.Attributes;

[ForRequirement(typeof(RbacFeature))]
public class RbacService : IRbacSpec
{
    [ForRequirement(typeof(RbacFeature), nameof(RbacFeature.UserCanAssignRole))]
    public async Task AssignRoleAsync(UserId user, RoleId role)
    {
        // implementation
    }

    [ForRequirement(typeof(RbacFeature), nameof(RbacFeature.UserCanRevokeRole))]
    public async Task RevokeRoleAsync(UserId user, RoleId role)
    {
        // implementation
    }

    [ForRequirement(typeof(RbacFeature), nameof(RbacFeature.UnauthorizedUserIsRejected))]
    public async Task<bool> CheckAccessAsync(UserId user, ResourceId resource)
    {
        // implementation
    }
}
```

The `[ForRequirement]` on the class establishes the link at the class level. The `[ForRequirement]` on each method traces individual ACs through the implementation layer. The REQ200 analyzer (when implemented) will verify that every specification interface has at least one implementing class.

---

## 8. Link Tests to Requirements

Test classes use `[TestsFor]` at the class level and `[Verifies]` on individual test methods.

```csharp
using FrenchExDev.Net.Requirements.Attributes;
using Xunit;

[TestsFor(typeof(RbacFeature))]
public class RbacFeatureTests
{
    [Fact]
    [Verifies(typeof(RbacFeature), nameof(RbacFeature.UserCanAssignRole))]
    public async Task AssignRole_adds_role_to_user()
    {
        // Arrange, Act, Assert
    }

    [Fact]
    [Verifies(typeof(RbacFeature), nameof(RbacFeature.UserCanRevokeRole))]
    public async Task RevokeRole_removes_role_from_user()
    {
        // Arrange, Act, Assert
    }

    [Fact]
    [Verifies(typeof(RbacFeature), nameof(RbacFeature.UnauthorizedUserIsRejected))]
    public async Task Unauthorized_user_gets_rejected()
    {
        // Arrange, Act, Assert
    }
}
```

- `[TestsFor(typeof(RbacFeature))]` marks the class. The REQ300 analyzer (when implemented) checks that every feature has at least one `[TestsFor]` class.
- `[Verifies(typeof(RbacFeature), nameof(RbacFeature.UserCanAssignRole))]` traces a test method to a specific AC. The REQ301 analyzer (when implemented) checks that every AC has at least one `[Verifies]` test.
- `AcceptanceCriterionName` is required on `[Verifies]` -- it always points at a specific criterion.
- Both attributes support `AllowMultiple = true`, so one test method can verify multiple ACs if needed.

---

## 9. Use AcceptanceCriterionResult

### Create results

```csharp
using FrenchExDev.Net.Requirements;

// Success
AcceptanceCriterionResult ok = AcceptanceCriterionResult.Satisfied();

// Failure with reason
AcceptanceCriterionResult fail = AcceptanceCriterionResult.Failed("User does not have admin role");
```

### Use implicit bool conversion

```csharp
AcceptanceCriterionResult result = AcceptanceCriterionResult.Satisfied();

if (result)
{
    Console.WriteLine("Criterion met");
}

// Works directly in xUnit assertions
Assert.True(result);
Assert.False(AcceptanceCriterionResult.Failed("reason"));
```

### Check failure reason

```csharp
var result = AcceptanceCriterionResult.Failed("timeout exceeded");
if (!result)
{
    Console.WriteLine($"Failed: {result.FailureReason}");
    // Output: Failed: timeout exceeded
}
```

### Value equality

```csharp
var a = AcceptanceCriterionResult.Satisfied();
var b = AcceptanceCriterionResult.Satisfied();
Assert.Equal(a, b);       // true
Assert.True(a == b);      // true

var c = AcceptanceCriterionResult.Failed("x");
var d = AcceptanceCriterionResult.Failed("x");
Assert.Equal(c, d);       // true

var e = AcceptanceCriterionResult.Failed("y");
Assert.NotEqual(c, e);    // true -- different reason
```

---

## 10. Use Domain Concepts in AC Signatures

Domain concepts are readonly structs defined in `FrenchExDev.Net.Requirements`. They exist to make acceptance criterion signatures type-safe and self-documenting.

### Available domain concepts

| Type | Fields | Purpose |
|---|---|---|
| `UserId` | `Guid Value`, `string FirstName`, `string LastName`, `Email Email` | Identifies a user |
| `RoleId` | `string Value` | Identifies a role |
| `Email` | `string Value` | Email address |
| `ResourceId` | `string Value` | Identifies a protected resource |
| `TokenId` | `Guid Value` | Identifies an authentication token |

### Usage in AC methods

```csharp
public abstract class AuthFeature : Feature<SecurityEpic>
{
    public override string Title => "Authentication";
    public override RequirementPriority Priority => RequirementPriority.Critical;
    public override string Owner => "security-team";

    // Domain concepts make the signature self-documenting
    public abstract AcceptanceCriterionResult UserCanLogin(UserId user, Email email);
    public abstract AcceptanceCriterionResult TokenIsIssued(UserId user, TokenId token);
    public abstract AcceptanceCriterionResult AccessGrantedForRole(UserId user, RoleId role, ResourceId resource);
}
```

### Constructing domain concepts in tests

```csharp
var user = new UserId(
    Guid.NewGuid(),
    "Alice",
    "Smith",
    new Email("alice@example.com"));

var role = new RoleId("admin");
var resource = new ResourceId("billing-dashboard");
var token = new TokenId(Guid.NewGuid());
```

---

## 11. Access the RequirementRegistry at Runtime

The source generator emits `RequirementRegistry` in namespace `FrenchExDev.Net.Requirements.Generated`. It is available at runtime in any project that references the source generator.

### Look up a specific requirement

```csharp
using FrenchExDev.Net.Requirements.Generated;

var info = RequirementRegistry.All[typeof(RbacFeature)];

Console.WriteLine(info.Kind);    // RequirementKind.Feature
Console.WriteLine(info.Title);   // "Role-based access control"
Console.WriteLine(info.Parent);  // typeof(PlatformScalabilityEpic)

foreach (var ac in info.AcceptanceCriteria)
{
    Console.WriteLine($"  AC: {ac}");
    // AC: UserCanAssignRole
    // AC: UserCanRevokeRole
    // AC: UnauthorizedUserIsRejected
}
```

### Iterate all requirements

```csharp
foreach (var (type, info) in RequirementRegistry.All)
{
    Console.WriteLine($"[{info.Kind}] {info.Title} ({type.Name})");
}
```

### Query by kind

```csharp
var epics = RequirementRegistry.All.Values
    .Where(r => r.Kind == RequirementKind.Epic)
    .ToList();

var uncoveredFeatures = RequirementRegistry.All.Values
    .Where(r => r.Kind == RequirementKind.Feature && r.AcceptanceCriteria.Length == 0)
    .ToList();
```

### Query parent-child relationships

```csharp
var children = RequirementRegistry.All.Values
    .Where(r => r.Parent == typeof(PlatformScalabilityEpic))
    .ToList();
```

### RequirementInfo properties

| Property | Type | Description |
|---|---|---|
| `Type` | `Type` | The requirement's CLR type |
| `Kind` | `RequirementKind` | `Epic`, `Feature`, `Story`, `Task`, or `Bug` |
| `Title` | `string` | From the `Title` property override (falls back to class name) |
| `Parent` | `Type?` | The generic type argument, or null for Epics and standalone Features |
| `AcceptanceCriteria` | `string[]` | Names of abstract methods returning `AcceptanceCriterionResult` |

---

## 12. Test the Hierarchy

Use reflection to verify the requirement hierarchy and acceptance criteria in unit tests.

### Test that an Epic is a RequirementMetadata

```csharp
using FrenchExDev.Net.Requirements;
using FrenchExDev.Net.Requirements.Testing;
using Xunit;

[Fact]
public void Epic_is_RequirementMetadata()
{
    Assert.True(typeof(RequirementMetadata).IsAssignableFrom(typeof(Epic)));
}
```

### Test that a Feature's parent constraint is satisfied

```csharp
[Fact]
public void Feature_with_epic_parent_compiles()
{
    // SampleFeature : Feature<SampleEpic> -- generic constraint enforced by compiler
    Assert.True(typeof(RequirementMetadata).IsAssignableFrom(typeof(SampleFeature)));
}
```

### Test acceptance criteria count

```csharp
[Fact]
public void SampleFeature_has_two_acceptance_criteria()
{
    var methods = typeof(SampleFeature).GetMethods(
        System.Reflection.BindingFlags.Public
        | System.Reflection.BindingFlags.Instance
        | System.Reflection.BindingFlags.DeclaredOnly);

    var acs = System.Array.FindAll(methods,
        m => m.IsAbstract && m.ReturnType == typeof(AcceptanceCriterionResult));

    Assert.Equal(2, acs.Length);
}
```

This pattern works for any requirement class. Change `typeof(SampleFeature)` and the expected count.

### Test that attributes carry MetaConcept

```csharp
[Fact]
public void ForRequirement_has_MetaConcept()
{
    var attr = Attribute.GetCustomAttribute(
        typeof(FrenchExDev.Net.Requirements.Attributes.ForRequirementAttribute),
        typeof(FrenchExDev.Net.Dsl.MetaConceptAttribute));
    Assert.NotNull(attr);
}

[Fact]
public void Verifies_has_MetaConcept()
{
    var attr = Attribute.GetCustomAttribute(
        typeof(FrenchExDev.Net.Requirements.Attributes.VerifiesAttribute),
        typeof(FrenchExDev.Net.Dsl.MetaConceptAttribute));
    Assert.NotNull(attr);
}

[Fact]
public void TestsFor_has_MetaConcept()
{
    var attr = Attribute.GetCustomAttribute(
        typeof(FrenchExDev.Net.Requirements.Attributes.TestsForAttribute),
        typeof(FrenchExDev.Net.Dsl.MetaConceptAttribute));
    Assert.NotNull(attr);
}
```

### Test AcceptanceCriterionResult behavior

```csharp
[Fact]
public void Satisfied_is_true()
{
    var result = AcceptanceCriterionResult.Satisfied();
    Assert.True(result.IsSatisfied);
    Assert.True(result); // implicit bool conversion
}

[Fact]
public void Failed_is_false_with_reason()
{
    var result = AcceptanceCriterionResult.Failed("bad input");
    Assert.False(result.IsSatisfied);
    Assert.False(result);
    Assert.Equal("bad input", result.FailureReason);
}

[Fact]
public void Equality_works()
{
    Assert.Equal(AcceptanceCriterionResult.Satisfied(), AcceptanceCriterionResult.Satisfied());
    Assert.NotEqual(AcceptanceCriterionResult.Satisfied(), AcceptanceCriterionResult.Failed("x"));
}
```

All of these tests are taken directly from `RequirementHierarchyTests` and `AcceptanceCriterionResultTests` in the test project.

---

## 13. Project Setup

### Consuming the Requirements DSL (defining requirements)

Add a project reference to `FrenchExDev.Net.Requirements`:

```xml
<ItemGroup>
  <ProjectReference Include="..\..\..\Requirements\src\FrenchExDev.Net.Requirements\FrenchExDev.Net.Requirements.csproj" />
</ItemGroup>
```

This gives you `RequirementMetadata`, `Epic`, `Feature<T>`, `Feature`, `Story<T>`, `RequirementTask<T>`, `Bug`, `AcceptanceCriterionResult`, the domain concepts (`UserId`, `RoleId`, etc.), and the enums (`RequirementPriority`, `BugSeverity`).

### Linking specifications and implementations (attributes)

Add a project reference to `FrenchExDev.Net.Requirements.Attributes`:

```xml
<ItemGroup>
  <ProjectReference Include="..\..\..\Requirements\src\FrenchExDev.Net.Requirements.Attributes\FrenchExDev.Net.Requirements.Attributes.csproj" />
</ItemGroup>
```

This gives you `ForRequirementAttribute`, `VerifiesAttribute`, and `TestsForAttribute`.

### Enabling the source generator and analyzers (test projects)

Wire the source generator and analyzers as analyzer references:

```xml
<ItemGroup>
  <ProjectReference Include="..\..\src\FrenchExDev.Net.Requirements\FrenchExDev.Net.Requirements.csproj" />
  <ProjectReference Include="..\..\src\FrenchExDev.Net.Requirements.Attributes\FrenchExDev.Net.Requirements.Attributes.csproj" />
  <ProjectReference Include="..\..\src\FrenchExDev.Net.Requirements.Testing\FrenchExDev.Net.Requirements.Testing.csproj" />
  <ProjectReference Include="..\..\src\FrenchExDev.Net.Requirements.SourceGenerator\FrenchExDev.Net.Requirements.SourceGenerator.csproj"
                    OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
  <ProjectReference Include="..\..\src\FrenchExDev.Net.Requirements.SourceGenerator.Lib\FrenchExDev.Net.Requirements.SourceGenerator.Lib.csproj"
                    OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
  <ProjectReference Include="..\..\src\FrenchExDev.Net.Requirements.Analyzers\FrenchExDev.Net.Requirements.Analyzers.csproj"
                    OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
</ItemGroup>
```

Key points:
- `OutputItemType="Analyzer"` tells MSBuild to load the assembly as a Roslyn analyzer/generator.
- `ReferenceOutputAssembly="false"` prevents the generator DLL from being referenced as a normal dependency.
- Both `SourceGenerator` and `SourceGenerator.Lib` must be listed as analyzers -- the generator depends on the Lib at runtime.
- The `Testing` project reference provides `SampleEpic`, `SampleFeature`, and `SampleStory` for use in tests.
- The `Analyzers` reference enables REQ1xx-REQ3xx diagnostics.

### Target frameworks

| Project | TFM |
|---|---|
| Requirements | `netstandard2.0;net10.0` |
| Requirements.Attributes | `netstandard2.0;net10.0` |
| Requirements.Testing | `netstandard2.0;net10.0` |
| Requirements.SourceGenerator | `netstandard2.0` |
| Requirements.SourceGenerator.Lib | `netstandard2.0` |
| Requirements.Analyzers | `netstandard2.0` |
| Requirements.Tests | `net10.0` |

The core libraries multi-target `netstandard2.0;net10.0` for broad compatibility. Source generators and analyzers must target `netstandard2.0` (Roslyn host requirement). Test projects target `net10.0`.
