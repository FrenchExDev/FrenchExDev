# GitLab.Ci.Yaml -- Philosophy

## 1. Why Schema-Driven Code Generation?

`.gitlab-ci.yml` is a complex format with 50+ definition types, union types (`string | list`, `string | object`), version-dependent properties, and a flat root structure where reserved keys coexist with arbitrary job names. Writing and maintaining these files by hand is error-prone:

- Typos in keywords are silently ignored
- Removed/renamed properties go unnoticed until pipeline failure
- No IntelliSense or type checking for YAML strings
- Version differences between GitLab releases cause subtle breakage

By generating C# models directly from GitLab's own JSON Schema, we get:

- **Compile-time safety** -- invalid property names or types won't compile
- **IntelliSense** -- full discoverability with XML docs from schema descriptions
- **Version awareness** -- `[SinceVersion]` / `[UntilVersion]` attributes document when properties were added/removed
- **Automatic evolution** -- run Design to download new schemas, rebuild, and the models update

---

## 2. Why Follow the DockerCompose.Bundle Pattern?

The four-project architecture (Attributes, Design, SourceGenerator, Bundle) is a proven pattern in this codebase:

| Concern | Project | Benefit |
|---------|---------|---------|
| Triggering | Attributes | Minimal dependency, `netstandard2.0` compatible |
| Schema acquisition | Design | Separate executable, runs on demand, doesn't slow build |
| Code generation | SourceGenerator | Runs at compile time, incremental, no runtime cost |
| Consumer API | Bundle | Clean public surface, hand-written + generated code |

This separation means:
- **No runtime schema parsing** -- all code is generated at compile time
- **No build-time downloads** -- schemas are pre-downloaded and committed
- **Testable in isolation** -- each project has a clear boundary

---

## 3. Why Merge Multiple Schema Versions?

GitLab evolves rapidly (monthly releases). A property available in 18.5 may not exist in 18.0. Rather than targeting a single version:

- **Unified API**: One set of models covers all supported versions
- **Version metadata**: Each property carries `[SinceVersion]` / `[UntilVersion]` so consumers can check compatibility
- **Latest types**: Property types use the latest schema's definition (most complete)
- **No breaking changes**: Adding a new schema version only adds properties, never removes

This mirrors the DockerCompose.Bundle approach and enables consumers to build pipelines that are aware of their target GitLab version.

---

## 4. Why a Flat Root Model?

`.gitlab-ci.yml` has an unusual structure: the root YAML mapping contains both reserved keywords (`stages`, `variables`, `include`, `default`, `workflow`) and arbitrary job names as siblings:

```yaml
stages: [build, test]       # reserved
variables:                   # reserved
  CI: "true"
build:                       # job (arbitrary name)
  script: echo building
test:                        # job (arbitrary name)
  script: echo testing
```

The C# model reflects this honestly:

```csharp
public partial class GitLabCiFile
{
    public List<object>? Stages { get; set; }
    public Dictionary<string, object?>? Variables { get; set; }
    // ... other reserved properties
    public Dictionary<string, GitLabCiJob>? Jobs { get; set; }  // arbitrary job names
}
```

The **writer** flattens this back: reserved keys are emitted first, then job entries are merged at root level (not nested under `jobs:`). The **reader** does the inverse: parses raw YAML, separates reserved keys, and puts remaining entries into the `Jobs` dictionary.

---

## 5. Why Hand-Written Reader/Writer?

Unlike DockerCompose.Bundle (which only generates models and builders), GitLab.Ci.Yaml also provides serialization. This is hand-written because:

- **Flat root merging** requires custom logic (jobs at root level, not nested)
- **Union type handling** (`string | List<string>` for `script`) needs `IYamlTypeConverter`
- **YamlDotNet configuration** (naming convention, null handling) is runtime behavior, not generated code
- **Forward compatibility** -- the reader gracefully handles unknown properties via `IgnoreUnmatchedProperties`

The reader and writer are intentionally simple and tolerant. The writer always produces valid YAML. The reader does best-effort parsing, falling back to empty jobs when typed deserialization fails for specific properties.

---

## 6. Why `IGitLabCiContributor`?

The contributor pattern enables modular pipeline composition:

- Each concern (build, test, Docker, deploy) is encapsulated in its own contributor
- Contributors can be combined, reordered, or conditionally applied
- Each contributor is independently testable
- The pattern mirrors `IComposeFileContributor` from DockerCompose.Bundle

This is especially useful for HomeLab or multi-project setups where different projects share pipeline fragments.

---

## 7. Design Trade-offs

### Type aliases mapped to primitives

Many GitLab CI schema definitions are type aliases (e.g., `script` = `oneOf[string, array]`, `when` = `string enum`). Rather than generating classes for these, they're mapped to C# primitives (`List<string>?`, `string?`, `bool?`). This keeps the API surface clean at the cost of losing some schema-level validation.

### `List<object>` for stages

The `stages` property is `type: array` without `items` type constraints in the schema, so it maps to `List<object>?`. In practice, stages are always strings, but the schema doesn't enforce this.

### Catch-all job deserialization

The reader wraps job deserialization in try/catch because generated models have strict types (e.g., `List<string>` for scripts) while YAML allows flexible structures. When typed deserialization fails, a minimal `GitLabCiJob` is created. This is pragmatic: partial parsing is more useful than no parsing.

### No YAML anchor/alias support

YAML anchors (`&anchor`) and aliases (`*alias`) are not modeled in the C# types. The reader processes the resolved YAML (anchors already expanded by YamlDotNet), and the writer always emits explicit values. This is intentional: anchors are a YAML syntax feature, not a GitLab CI concept.
