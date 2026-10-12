# SG (Source Generators) — Architecture

## Multi-Project Decomposition

Every source generator follows a mandatory 4-project layout:

| Project | Target | Roslyn Dependency | Purpose |
|---------|--------|-------------------|---------|
| `FrenchExDev.Net.{Name}` | `netstandard2.0;net10.0` | No | Runtime library (base classes, interfaces) |
| `FrenchExDev.Net.{Name}.Attributes` | `netstandard2.0;net10.0` | No | Marker attributes that trigger generation |
| `FrenchExDev.Net.{Name}.SourceGenerator` | `netstandard2.0` | **Yes** | `IIncrementalGenerator` — Roslyn extraction |
| `FrenchExDev.Net.{Name}.SourceGenerator.Lib` | `netstandard2.0` | **No** | Stateless emitter — pure string building |

## Generator Catalog

| Generator | Attribute Trigger | Emitter | Output |
|-----------|------------------|---------|--------|
| `BuilderGenerator` | `[Builder]` | `BuilderEmitter` | `{Class}Builder.g.cs` |
| `BinaryWrapperGenerator` | `[BinaryWrapper("tool")]` + JSON AdditionalFiles | `CommandClassEmitter`, `ClientClassEmitter` | Commands, builders, client |
| `ComposeBundleGenerator` | `[ComposeBundle]` + JSON schemas | `ModelClassEmitter` + `BuilderEmitter` | Models + builders |
| `InvariantGenerator` | `[AggregateRoot]`/`[Entity]` + `[Invariant]` | `InvariantEmitter` | `EnsureInvariants()` |
| `MetamodelRegistryGenerator` | `[MetaConcept]` | `MetamodelRegistryEmitter` | Concept registry |
| `RequirementRegistryGenerator` | Requirement hierarchy classes | `RequirementRegistryEmitter` | `RequirementRegistry.g.cs` |
| `FiniteStateMachineGenerator` | `[StateMachine]` | `FsmEmitter` | State machine builders |

Plus Diem domain generators: Admin.Forms, Admin.Lists, Admin.Actions, Content.Blocks, Content.Parts, Content.StreamFields, Pages.Widgets, Workflow.Gates, Workflow.StateMachine.

## BuilderEmitModel (Universal Model)

The central data model that drives `BuilderEmitter.Emit()`:

```
BuilderEmitModel
  |-- Namespace: string
  |-- TargetClassName: string
  |-- BuilderClassName: string
  |-- Instantiation: string ("init" | "ctor" | "factory:X" | "custom")
  |-- Preamble: string?              <-- extension point
  |-- Properties: BuilderPropertyModel[]
        |-- Name: string
        |-- TypeName: string
        |-- IsCollection: bool
        |-- IsRequired: bool
        |-- WithMethodAttributes: string[]?      <-- extension point
        |-- WithMethodBodyPrefix: string?        <-- extension point
        |-- InstantiationExpression: string?     <-- extension point
```

See: `Builder/src/FrenchExDev.Net.Builder.SourceGenerator.Lib/BuilderEmitModel.cs`

## Data Flow Per Generator Type

### Builder (attribute-driven)
```
[Builder] attribute on class
  --> BuilderGenerator.GetModel() extracts IPropertySymbol -> BuilderEmitModel
  --> BuilderEmitter.Emit(model) -> C# source string
  --> {Class}Builder.g.cs
```

### BinaryWrapper (JSON + attribute-driven)
```
[BinaryWrapper("tool")] descriptor + scrape/{tool}-{version}.json (AdditionalFiles)
  --> BinaryWrapperGenerator reads JSON -> CommandTree model
  --> CommandClassEmitter, BuilderClassEmitter, ClientClassEmitter
  --> Commands/, Builders/, {Tool}Client.g.cs
```

### DockerCompose (schema + attribute-driven)
```
[ComposeBundle] + schemas/compose-spec-v{version}.json (AdditionalFiles)
  --> ComposeBundleGenerator reads JSON schema -> SchemaModel
  --> ModelClassEmitter -> model classes
  --> BuilderHelper -> BuilderEmitModel -> BuilderEmitter.Emit() -> builder classes
```

### DDD (attribute-driven)
```
[AggregateRoot]/[Entity] + [Invariant] methods
  --> InvariantGenerator extracts invariant methods -> InvariantModel
  --> InvariantEmitter.Emit(model) -> EnsureInvariants() method
```

## NamingHelper

Shared naming normalization across BinaryWrapper and DockerCompose:
- `ToPascalCase` — strips `[]` brackets and spaces from option names
- `DeduplicateOptions` — deduplicates options with same PascalCase name

See: `BinaryWrapper/src/FrenchExDev.Net.BinaryWrapper.SourceGenerator.Lib/NamingHelper.cs`

## Key Files

- `Builder/src/.../SourceGenerator/BuilderGenerator.cs` — canonical `[Generator]` entry point
- `Builder/src/.../SourceGenerator.Lib/BuilderEmitter.cs` — shared emission logic
- `Builder/src/.../SourceGenerator.Lib/BuilderEmitModel.cs` — universal model with extension points
- `BinaryWrapper/src/.../SourceGenerator/BinaryWrapperGenerator.cs` — JSON-driven generation
- `DockerCompose/src/.../SourceGenerator/ComposeBundleGenerator.cs` — schema-driven generation
- `Ddd/src/.../SourceGenerator/InvariantGenerator.cs` — DDD invariant generation
