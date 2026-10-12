# GitLab.Ci.Yaml -- Architecture

## 1. Overview

GitLab.Ci.Yaml is a schema-driven code generation library that produces typed C# models and fluent builders from the official GitLab CI JSON Schema. It follows the same four-project pattern as `DockerCompose.Bundle`.

---

## 2. Project Decomposition

```
GitLab.Ci.Yaml/
  src/
    FrenchExDev.Net.GitLab.Ci.Yaml/                 net10.0    Main library
    FrenchExDev.Net.GitLab.Ci.Yaml.Attributes/       ns2.0+net10   Marker attribute
    FrenchExDev.Net.GitLab.Ci.Yaml.Design/           net10.0    Schema downloader (Exe)
    FrenchExDev.Net.GitLab.Ci.Yaml.SourceGenerator/  ns2.0      Roslyn incremental SG
  test/
    FrenchExDev.Net.GitLab.Ci.Yaml.Tests/            net10.0    xUnit tests
```

| Project | Role |
|---------|------|
| **Attributes** | `[GitLabCiBundle]` attribute that triggers source generation |
| **Design** | CLI that downloads versioned CI schemas from GitLab releases API |
| **SourceGenerator** | Reads JSON schemas as `AdditionalFiles`, merges versions, emits C# models + builders |
| **Main library** | Contains generated code (via SG), hand-written YAML reader/writer, version type, contributor interface |
| **Tests** | 20 tests covering models, builders, writer, reader, version parsing |

---

## 3. Dependency Graph

```
Wrapper.Versioning
  ^
  |
Design (Exe) ----downloads----> schemas/*.json (embedded in main library)
                                      |
                                      v
Builder.SourceGenerator.Lib  <---  SourceGenerator (reads schemas, emits code)
                                      |
                                      v
Builder + Result  <-----------  Main Library (generated models + hand-written infra)
  ^                                   |
  |                                   v
YamlDotNet  <-----------------  Serialization/ (reader, writer, converters)
```

---

## 4. Source Generator Pipeline

The `GitLabCiBundleGenerator` (incremental source generator) runs at compile time:

```
AdditionalFiles (gitlab-ci-v*.json)
  |
  v
SchemaReader.Parse()          -- JSON Schema -> SchemaModel per version
  |                              Handles: definitions, $defs, allOf, oneOf, anyOf
  |                              Handles: markdownDescription, inline objects
  v
SchemaVersionMerger.Merge()   -- List<SchemaModel> -> UnifiedSchema
  |                              Tracks SinceVersion / UntilVersion per property
  v
ModelClassEmitter             -- UnifiedSchema -> model .g.cs files
  |                              GitLabCiFile, GitLabCiJob, GitLabCiArtifacts, ...
  |                              Inline classes for nested objects
  v
BuilderHelper                 -- UnifiedSchema -> BuilderEmitModel per class
  |                              Detects collections, dictionaries, nested builders
  v
BuilderEmitter.Emit()         -- BuilderEmitModel -> builder .g.cs files
                                 (from Builder.SourceGenerator.Lib, no Roslyn dep)
```

### Generated output (60 files)

- 30 model classes (`GitLabCiFile.g.cs`, `GitLabCiJob.g.cs`, ...)
- 30 builder classes (`GitLabCiFileBuilder.g.cs`, `GitLabCiJobBuilder.g.cs`, ...)
- 1 version metadata class (`GitLabCiSchemaVersions.g.cs`)

---

## 5. Schema Handling

### 5.1 Source

Schemas are downloaded from GitLab release tags:
```
https://gitlab.com/gitlab-org/gitlab/-/raw/v{version}-ee/app/assets/javascripts/editor/schema/ci.json
```

Tags use format `v18.10.0-ee`. The Design project strips the `v` prefix and `-ee` suffix to get semantic versions.

### 5.2 Schema Format

- JSON Schema draft-07 (`http://json-schema.org/draft-07/schema#`)
- Uses `definitions` (not `$defs`)
- Root `properties`: `spec`, `image`, `services`, `before_script`, `after_script`, `variables`, `cache`, `default`, `stages`, `include`, `pages`, `workflow`
- Root `additionalProperties`: `{ "$ref": "#/definitions/job" }` -- any non-reserved key is a job
- `job` definition uses `allOf: [{ "$ref": "#/definitions/job_template" }]`
- Many definitions are type aliases (e.g., `script` is `oneOf[string, array]`), not objects with properties
- Uses `markdownDescription` instead of `description` on most elements

### 5.3 Schema Adaptations

The SchemaReader handles several GitLab-specific patterns:

| Pattern | Handling |
|---------|----------|
| `allOf` with `$ref` | Resolves by copying properties from referenced definition |
| `anyOf` | Treated same as `oneOf` for code generation |
| `markdownDescription` | Used as fallback when `description` absent |
| `$schema`, `!reference` | Skipped (not useful as C# properties) |
| Special chars in names | `$`, `!`, `@` stripped by `ToPascalCase` |
| Type-alias definitions | Mapped to primitives via `MapRefToType` (54 definitions, ~40 are aliases) |
| Duplicate inline classes | Deduplicated (same class from `allOf`-resolved definitions) |

---

## 6. Hand-Written Infrastructure

### 6.1 GitLabCiBundleDescriptor

```csharp
[GitLabCiBundle]
public partial class GitLabCiBundleDescriptor;
```

Triggers the source generator. The generated code is emitted into the same project.

### 6.2 GitLabCiVersion

Semantic version record (`Major.Minor.Patch`) with `IComparable`, `Parse`, `TryParse`, comparison operators.

### 6.3 IGitLabCiContributor + Extensions

```csharp
public interface IGitLabCiContributor
{
    void Contribute(GitLabCiFile ciFile);
}
```

`GitLabCiFileExtensions.Apply()` provides fluent contributor application.

### 6.4 Serialization

| Class | Role |
|-------|------|
| `GitLabCiYamlWriter` | Serializes `GitLabCiFile` to YAML. Merges reserved keys + jobs at root level. Omits nulls. |
| `GitLabCiYamlReader` | Deserializes YAML to `GitLabCiFile`. Separates reserved keys from job entries. |
| `StringOrListConverter` | `IYamlTypeConverter` for `string -> List<string>` (handles `script: "echo hi"` vs `script: [...]`) |
| `GitLabCiNamingConvention` | PascalCase to snake_case conversion for YAML keys |

---

## 7. Root-Level Flat Structure

`.gitlab-ci.yml` has a flat root mapping where some keys are reserved (stages, variables, include, default, workflow) and all other keys are job names. This is reflected in:

- **Model**: `GitLabCiFile` has explicit properties for reserved keys + a `Dictionary<string, GitLabCiJob> Jobs` property
- **Writer**: Merges reserved keys and job entries into a single YAML document (jobs NOT nested under `jobs:`)
- **Reader**: Parses raw YAML, separates reserved keys, deserializes remaining entries as jobs

---

## 8. Version Merging

When multiple schema versions are present (18.0 -- 18.10), the `SchemaVersionMerger`:

1. Sorts schemas by semantic version
2. For each definition and property, tracks first/last appearance version
3. Uses the latest version's definition for property types
4. Annotates generated code with `[SinceVersion]` / `[UntilVersion]` attributes

This allows consumers to check API availability for specific GitLab versions.
