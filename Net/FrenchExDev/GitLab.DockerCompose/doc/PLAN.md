# Plan: GitLab.DockerCompose — Ruby Config Parser Source Generator

## Context

Model the entire GitLab Omnibus `gitlab.rb` configuration surface (3730 lines, 55 Ruby prefixes) as typed C# classes with `AbstractBuilder<>`. Instead of hand-writing hundreds of classes, **a Source Generator parses the `gitlab.rb.template` file** and auto-generates config models + builders.

This follows the same pattern as `ComposeBundleGenerator` (reads `compose-spec-*.json` as `AdditionalFiles`, generates models + builders).

The `gitlab.rb.template` uses a clear convention:
- `###` = section headers / documentation
- `##!` = doc comments
- `# prefix['key'] = value` = real config settings (commented-out examples)
- `external_url`, `registry_external_url`, etc. = standalone assignments

---

## End-to-End Pipeline

```
┌─────────────────────────────────────────────────────────────────────┐
│  DESIGN TIME  (dotnet run --project .Design)                        │
│                                                                     │
│  GitLab API  ───►  GitLabReleasesVersionCollector                   │
│  /api/v4/projects/gitlab-org%2Fomnibus-gitlab/releases              │
│       │                                                             │
│       │  tagToVersion: "18.10.1+ce.0" → "18.10.1"                   │
│       │  filter: skip rc, skip ee                                   │
│       │  VersionFilters.LatestPatchPerMinor                         │
│       ▼                                                             │
│  DesignPipeline<string>                                             │
│       │  UseHttpDownload(v → .../raw/{v}+ce.0/.../gitlab.rb..)      │
│       │  UseSave()                                                  │
│       ▼                                                             │
│  resources/                                                         │
│    gitlab-15.0.5.rb                                                 │
│    gitlab-16.0.8.rb                                                 │
│    gitlab-17.0.6.rb                                                 │
│    gitlab-18.0.1.rb                                                 │
│    gitlab-18.10.1.rb    ← one .rb per minor (latest patch)          │
└─────────────────────────────────────────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────────────────┐
│  BUILD TIME  (Source Generator — GitLabOmnibusGenerator)            │
│                                                                     │
│  AdditionalFiles: resources/gitlab-*.rb                             │
│       │                                                             │
│       ▼                                                             │
│  ┌───────────────────────────────────┐                              │
│  │  1. PARSE  (GitLabRbParser)       │                              │
│  │     for each gitlab-{v}.rb:       │                              │
│  │     → GitLabRbModel(version)      │                              │
│  │       tree of ObjectNodes per     │                              │
│  │       prefix (nginx, redis, ...)  │                              │
│  └───────────────┬───────────────────┘                              │
│                  ▼                                                  │
│  ┌───────────────────────────────────┐                              │
│  │  2. MERGE  (GitLabRbVersionMerger)│                              │
│  │     same as SchemaVersionMerger:  │                              │
│  │     compare trees across versions │                              │
│  │     → SinceVersion / UntilVersion │                              │
│  │       on every node               │                              │
│  └───────────────┬───────────────────┘                              │
│                  ▼                                                  │
│  ┌───────────────────────────────────────────────────────────┐      │
│  │  3. EMIT  (GitLabConfigEmitter + BuilderEmitter)          │      │
│  │     recursive walk of merged tree:                        │      │
│  │                                                           │      │
│  │     Leaf  → property on parent class                      │      │
│  │     Branch → sub-class + sub-builder (recurse)            │      │
│  │     [{…}]  → List<SubClass> + item builder                │      │
│  │                                                           │      │
│  │     For each class:                                       │      │
│  │       ctx.AddSource(XxxConfig.g.cs)        ← model        │      │
│  │       ctx.AddSource(XxxConfigBuilder.g.cs) ← builder      │      │
│  │         via BuilderEmitter.Emit(BuilderEmitModel)         │      │
│  │                                                           │      │
│  │     Also:                                                 │      │
│  │       GitLabOmnibusVersions.g.cs  ← version list          │      │
│  │       GitLabRbMetadata.g.cs       ← render metadata       │      │
│  └───────────────────────────────────────────────────────────┘      │
│                                                                     │
│  Generated (obj/Generated/):                                        │
│    GitLabOmnibusConfig.g.cs       ← root: URLs + 55 prefix props    │
│    GitLabOmnibusConfigBuilder.g.cs                                  │
│    NginxConfig.g.cs               ← [SinceVersion("15.0.5")]        │
│    NginxConfigBuilder.g.cs        ← WithListenPort(), ...           │
│    GitalyConfigConfiguration.g.cs ← sub-class from hash value       │
│    GitalyConfigConfigurationAuth.g.cs                               │
│    ...  (~200+ files)                                               │
└─────────────────────────────────────────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────────────────┐
│  RUNTIME  (hand-written code using generated types)                 │
│                                                                     │
│  var config = await new GitLabOmnibusConfigBuilder()                │
│      .WithExternalUrl("https://gitlab.example.com")                 │
│      .WithNginx(n => n                                              │
│          .WithListenPort(80)                                        │
│          .WithListenHttps(false))                                   │
│      .WithGitLabRails(r => r                                        │
│          .WithSmtpEnable(true)                                      │
│          .WithSmtpAddress("smtp.example.com"))                      │
│      .BuildAsync();                                                 │
│                                                                     │
│  var ruby = GitLabRbRenderer.Render(config);                        │
│  // → "external_url 'https://gitlab.example.com'\n                  │
│  //    nginx['listen_port'] = 80\n ..."                             │
│                                                                     │
│  GitLabComposeContributor  →  ComposeFile.Services["gitlab"]        │
│    env: GITLAB_OMNIBUS_CONFIG = ruby                                │
│    volumes, ports, healthcheck, shm_size                            │
│                                                                     │
│  + PostgresqlComposeContributor                                     │
│  + RedisComposeContributor                                          │
│  + GitLabRunnerComposeContributor                                   │
│  + MinioComposeContributor                                          │
└─────────────────────────────────────────────────────────────────────┘
```

### Version Merger Detail

Same algorithm as `SchemaVersionMerger.Merge()` in DockerCompose.Bundle.SG:

```
Input:  gitlab-15.0.5.rb → { nginx.listen_port, nginx.listen_https, ... }
        gitlab-16.0.8.rb → { nginx.listen_port, nginx.http2_enabled, ... }
        gitlab-17.0.6.rb → { nginx.listen_port, nginx.http2_enabled, gitlab_kas.enable, ... }
        gitlab-18.10.1.rb→ { nginx.listen_port, nginx.http2_enabled, gitlab_kas.enable, oak.enable, ... }

Merge:  nginx.listen_port   → SinceVersion: null (all)     UntilVersion: null (all)
        nginx.http2_enabled  → SinceVersion: "16.0.8"      UntilVersion: null
        gitlab_kas.enable    → SinceVersion: "17.0.6"      UntilVersion: null
        oak.enable           → SinceVersion: "18.10.1"     UntilVersion: null
        (removed_setting)    → SinceVersion: "15.0.5"      UntilVersion: "16.0.8"

Output: [SinceVersion("16.0.8")]
        public bool? Http2Enabled { get; set; }
```

---

## Phase 0: Prerequisite — `IComposeFileContributor` in DockerCompose.Bundle

Does not exist yet. Create in `DockerCompose/src/FrenchExDev.Net.DockerCompose.Bundle/`:

- `IComposeFileContributor.cs` — `void Contribute(ComposeFile composeFile);`
- `ComposeFileExtensions.cs` — `Apply()` fluent chaining (mirrors `PackerBundle.Apply()`)

---

## Phase 1: Source Generator — Parse `gitlab.rb.template`

### 1.1 Project Structure

```
GitLab.DockerCompose/
  FrenchExDev.Net.GitLab.DockerCompose.slnx
  resources/
    gitlab.rb                              ← already exists, the source of truth
  src/
    FrenchExDev.Net.GitLab.DockerCompose/
      FrenchExDev.Net.GitLab.DockerCompose.csproj
      Rendering/GitLabRbRenderer.cs        ← renders built config → Ruby syntax
      Contributors/                        ← IComposeFileContributor impls
      GitLabDockerImages.cs

    FrenchExDev.Net.GitLab.DockerCompose.SourceGenerator/
      FrenchExDev.Net.GitLab.DockerCompose.SourceGenerator.csproj  (netstandard2.0)
      GitLabRbParser.cs                    ← parses gitlab.rb → GitLabRbModel
      GitLabRbModel.cs                     ← parsed section/setting models
      GitLabConfigEmitter.cs               ← emits C# config classes
      GitLabOmnibusGenerator.cs            ← IIncrementalGenerator entry point
      NamingHelper.cs                      ← Ruby snake_case → C# PascalCase

    FrenchExDev.Net.GitLab.DockerCompose.SourceGenerator.Lib/  (optional, if shared)
      ← only if rendering logic needs to be shared with Design project

    FrenchExDev.Net.GitLab.DockerCompose.Design/               ← console Exe
      FrenchExDev.Net.GitLab.DockerCompose.Design.csproj
      Program.cs                           ← downloads gitlab.rb.template from GitLab repo

  test/
    FrenchExDev.Net.GitLab.DockerCompose.Tests/
    FrenchExDev.Net.GitLab.DockerCompose.SourceGenerator.Tests/  ← parser + emitter unit tests
```

### 1.2 csproj Wiring

The SG reads all `gitlab-*.rb` files as `AdditionalFiles`, parses each, merges across versions (tracking `SinceVersion`/`UntilVersion` like `ComposeSchemaVersions`), and emits **both** model classes **and** builder classes directly — same pattern as `ComposeBundleGenerator`.

**No two-stage SG chaining** — a SG cannot consume another SG's output. Instead, our SG uses `BuilderEmitModel` + `BuilderEmitter.Emit()` from `Builder.SourceGenerator.Lib` to emit builders alongside models. This is exactly how `ComposeBundleGenerator` works:
1. Emit `NginxConfig.g.cs` (model class)
2. Construct `BuilderEmitModel` with `BuilderPropertyModel` list for each property
3. Call `BuilderEmitter.Emit(builderModel)` → `NginxConfigBuilder.g.cs`

**SG project** (`FrenchExDev.Net.GitLab.DockerCompose.SourceGenerator.csproj`):
```xml
<TargetFramework>netstandard2.0</TargetFramework>
<LangVersion>latest</LangVersion>
<Nullable>enable</Nullable>
<IsRoslynComponent>true</IsRoslynComponent>
<EnforceExtendedAnalyzerRules>true</EnforceExtendedAnalyzerRules>

<PackageReference Include="Microsoft.CodeAnalysis.CSharp" PrivateAssets="all" />
<PackageReference Include="Microsoft.CodeAnalysis.Analyzers" PrivateAssets="all" />

<!-- Shared builder emission (BuilderEmitter, BuilderEmitModel, BuilderPropertyModel) -->
<ProjectReference Include="Builder.SourceGenerator.Lib" />
```

**Main project** (`FrenchExDev.Net.GitLab.DockerCompose.csproj`):
```xml
<AdditionalFiles Include="..\..\resources\gitlab-*.rb" />

<!-- SG as analyzer (emits model + builder classes) -->
<ProjectReference Include="GitLab.DockerCompose.SourceGenerator"
                  OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
<!-- Builder.SourceGenerator.Lib also as analyzer (required by the SG at emit time) -->
<ProjectReference Include="Builder.SourceGenerator.Lib"
                  OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
<!-- Runtime dependency on Builder base classes (AbstractBuilder<T>) -->
<ProjectReference Include="Builder" />
<ProjectReference Include="Result" />
<ProjectReference Include="DockerCompose.Bundle" />
```

**No `[Builder]` attribute needed** — the SG emits builders directly. No `Builder.Attributes` reference needed in main project.

---

## Phase 1b: Design Project (`FrenchExDev.Net.GitLab.DockerCompose.Design`)

Console `Exe` that discovers omnibus-gitlab versions via GitLab API, downloads `gitlab.rb.template` for each version, and saves to `resources/`. Follows the `DockerCompose.Bundle.Design` pattern (generic `DesignPipeline<string>` + `GitLabReleasesVersionCollector`).

### Version Discovery

GitLab omnibus tags live at `gitlab-org/omnibus-gitlab` on gitlab.com:
- Tags: `18.10.1+ce.0`, `18.10.0+ee.0`, `18.10.0+rc43.ce.0`, etc.
- We want **CE stable releases only** (filter: `+ce.0`, exclude `rc`)
- Raw file URL: `https://gitlab.com/gitlab-org/omnibus-gitlab/-/raw/{tag}/files/gitlab-config-template/gitlab.rb.template`

Use `GitLabReleasesVersionCollector("gitlab-org%2Fomnibus-gitlab")` with a custom `tagToVersion` that:
1. Filters to `+ce.0` tags only (or `+ee.0` — user preference, CE is default for the project)
2. Strips the `+ce.0` suffix → clean version string `18.10.1`
3. Skips pre-release (`rc`) tags

### csproj

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="Wrapper.Versioning" />
  </ItemGroup>
</Project>
```

### Program.cs

```csharp
using FrenchExDev.Net.Wrapper.Versioning;

var outputDir = Path.GetFullPath(Path.Combine(
    AppContext.BaseDirectory, "..", "..", "..", "..",
    "FrenchExDev.Net.GitLab.DockerCompose", "resources"));

var pipeline = new DesignPipeline<string>()
    .UseHttpDownload(version =>
        $"https://gitlab.com/gitlab-org/omnibus-gitlab/-/raw/{version}%2Bce.0/files/gitlab-config-template/gitlab.rb.template")
    .UseSave()
    .Build();

return await new DesignPipelineRunner<string>
{
    ItemCollector = new GitLabReleasesVersionCollector(
        "gitlab-org%2Fomnibus-gitlab",
        tagToVersion: tag =>
        {
            // Tags: "18.10.1+ce.0", "18.10.0+ee.0", "18.10.0+rc43.ce.0"
            // Keep only stable CE: ends with "+ce.0", no "rc"
            if (!tag.EndsWith("+ce.0") || tag.Contains("rc"))
                return null;  // skip
            return tag.Replace("+ce.0", "");  // "18.10.1"
        }),
    Pipeline = pipeline,
    KeySelector = v => v,
    ItemFilter = VersionFilters.LatestPatchPerMinor,  // one per minor version
    OutputDir = outputDir,
    OutputFilePattern = "gitlab-{key}.rb",
    AuthTokenEnvVar = "GITLAB_TOKEN",
}.RunAsync(args);
```

### Output

```
GitLab.DockerCompose/resources/
  gitlab-18.10.1.rb     ← latest from 18.10.x
  gitlab-18.9.3.rb      ← latest from 18.9.x
  gitlab-18.8.5.rb      ← latest from 18.8.x
  ...
  gitlab.rb             ← currently present (master, unversioned)
```

### Usage

```bash
# Download all stable CE versions (latest patch per minor)
dotnet run --project GitLab.DockerCompose/src/FrenchExDev.Net.GitLab.DockerCompose.Design

# Only download missing versions
dotnet run --project GitLab.DockerCompose/src/FrenchExDev.Net.GitLab.DockerCompose.Design -- --missing

# List available versions without downloading
dotnet run --project GitLab.DockerCompose/src/FrenchExDev.Net.GitLab.DockerCompose.Design -- --list

# Restrict to recent versions
dotnet run --project GitLab.DockerCompose/src/FrenchExDev.Net.GitLab.DockerCompose.Design -- --min-version 17.0.0
```

### SG Integration

The Source Generator reads **all** `gitlab-*.rb` files as `AdditionalFiles`:
```xml
<AdditionalFiles Include="..\..\resources\gitlab-*.rb" />
```

This enables multi-version awareness (like `ComposeSchemaVersions` in DockerCompose.Bundle) — settings can track `SinceVersion`/`UntilVersion` across omnibus releases.

---

## Phase 2: Ruby Config Parser (`GitLabRbParser`)

### 2.1 Parsing Rules

Input: raw text of `gitlab.rb.template`

**Line classification:**
| Pattern | Meaning | Action |
|---------|---------|--------|
| `## Title` or `###...###` | Major section header | New section group |
| `### Title` | Sub-section header | New sub-section |
| `##! text` or `###! text` | Documentation comment | Attach to next setting |
| `# prefix['key'] = value` | Config setting | Parse prefix, key, infer type from value |
| `# prefix['k1']['k2'] = value` | Nested config | Parse as nested path |
| `external_url '...'` or `# external_url '...'` | Standalone assignment | Special top-level setting |
| `# prefix['key'] = { ... }` | Hash value (may span multiple lines) | Parse as Dictionary or nested object |
| Empty / `##` banner lines | Separator | Skip |

### 2.2 Parsed Model (`GitLabRbModel`)

The parser builds a **full object hierarchy** — nested bracket paths and Ruby hashes become sub-objects with their own classes and builders.

```csharp
// Output of parsing
internal sealed class GitLabRbModel
{
    public List<GitLabRbSection> Sections { get; }
    public List<GitLabRbStandaloneUrl> StandaloneUrls { get; }  // external_url, etc.
}

internal sealed class GitLabRbSection
{
    public string Name { get; }          // "GitLab NGINX", "GitLab Redis", etc.
    public string? DocComment { get; }
    public List<GitLabRbPrefixGroup> PrefixGroups { get; }  // grouped by prefix
}

internal sealed class GitLabRbPrefixGroup
{
    public string Prefix { get; }        // "nginx", "gitlab_rails", "redis", etc.
    public GitLabRbObjectNode Root { get; }  // tree of properties
}

/// <summary>
/// A node in the property tree. Leaf nodes have a ValueType;
/// branch nodes have Children (representing nested brackets or hash keys).
/// </summary>
internal sealed class GitLabRbObjectNode
{
    public string Name { get; }                    // property/key name
    public string? DocComment { get; }
    public Dictionary<string, GitLabRbObjectNode> Children { get; }  // sub-properties
    public GitLabRbValueType? LeafType { get; }    // null if branch node
    public string? ExampleValue { get; }
    public bool IsArrayOfObjects { get; }          // [{...}, {...}] pattern
}

internal sealed class GitLabRbStandaloneUrl
{
    public string RubyKey { get; }       // "external_url", "registry_external_url"
    public string? ExampleValue { get; }
    public string? DocComment { get; }
}

internal enum GitLabRbValueType
{
    String,       // "value" or 'value'
    Integer,      // 80, 5432
    Long,         // very large numbers (e.g., 17179869184)
    Boolean,      // true, false
    Float,        // 0.9, 2.0
    StringList,   // ['a', 'b']
    IntList,      // [0.001, 0.005, ...]
    StringDict,   // { "key" => "value" }  — flat string-to-string
    Nil,          // nil → default to string?
}
```

### 2.3 Hierarchy Building

The parser constructs a **tree** per prefix. Examples:

**Bracket nesting** → tree branches:
```ruby
# gitlab_rails['object_store']['enabled'] = false
# gitlab_rails['object_store']['connection'] = {}
# gitlab_rails['object_store']['objects']['artifacts']['bucket'] = nil
```
Produces tree:
```
GitLabRails (root)
  └── ObjectStore (branch)
        ├── Enabled (leaf: Boolean)
        ├── Connection (leaf: StringDict)
        └── Objects (branch)
              └── Artifacts (branch)
                    └── Bucket (leaf: String)
```

**Ruby hash values** → sub-tree:
```ruby
# gitaly['configuration'] = {
#   listen_addr: 'localhost:8075',
#   auth: {
#     token: '<secret>',
#     transitioning: false,
#   },
#   storage: [
#     { name: 'default', path: '/var/opt/gitlab/git-data/repositories' },
#   ],
# }
```
Produces tree:
```
Gitaly (root)
  └── Configuration (branch, from hash value)
        ├── ListenAddr (leaf: String)
        ├── Auth (branch)
        │     ├── Token (leaf: String)
        │     └── Transitioning (leaf: Boolean)
        └── Storage (leaf: array-of-objects)
              ├── Name (leaf: String)
              └── Path (leaf: String)
```

### 2.4 Type Inference

From the example value:
- `true` / `false` → `bool?`
- bare integer (`80`, `5432`, `604800`) → `int?`
- very large integer (`17179869184`) → `long?`
- bare float (`0.9`, `2.0`) → `double?`
- `"string"` or `'string'` → `string?`
- `nil` → `string?` (unknown, default to string)
- `[]` or `['a', 'b']` → `List<string>?`
- `[0.001, 0.005, ...]` → `List<double>?`
- `{ "key" => "value" }` → `Dictionary<string, string?>?`
- Nested hash `{ key: { ... } }` → **sub-class** (new C# class with own builder)
- Array of hashes `[{ name: '...', path: '...' }]` → `List<SubClass>?` (sub-class generated)

### 2.5 Grouping Strategy

Settings are grouped by **Ruby prefix** into C# classes:

| Ruby Prefix | C# Class Name | Ruby Rendering Prefix |
|-------------|---------------|----------------------|
| `gitlab_rails` | `GitLabRailsConfig` | `gitlab_rails` |
| `nginx` | `NginxConfig` | `nginx` |
| `postgresql` | `PostgresqlConfig` | `postgresql` |
| `redis` | `RedisConfig` | `redis` |
| `puma` | `PumaConfig` | `puma` |
| `sidekiq` | `SidekiqConfig` | `sidekiq` |
| `gitaly` | `GitalyConfig` | `gitaly` |
| `registry` | `RegistryConfig` | `registry` |
| `gitlab_pages` | `GitLabPagesConfig` | `gitlab_pages` |
| `prometheus` | `PrometheusConfig` | `prometheus` |
| `letsencrypt` | `LetsEncryptConfig` | `letsencrypt` |
| `logging` | `LoggingConfig` | `logging` |
| `mattermost` | `MattermostConfig` | `mattermost` |
| `sentinel` | `SentinelConfig` | `sentinel` |
| `praefect` | `PraefectConfig` | `praefect` |
| `gitlab_workhorse` | `GitLabWorkhorseConfig` | `gitlab_workhorse` |
| `gitlab_shell` | `GitLabShellConfig` | `gitlab_shell` |
| `gitlab_sshd` | `GitLabSshdConfig` | `gitlab_sshd` |
| `gitlab_kas` | `GitLabKasConfig` | `gitlab_kas` |
| `alertmanager` | `AlertmanagerConfig` | `alertmanager` |
| `node_exporter` | `NodeExporterConfig` | `node_exporter` |
| `redis_exporter` | `RedisExporterConfig` | `redis_exporter` |
| `postgres_exporter` | `PostgresExporterConfig` | `postgres_exporter` |
| `gitlab_exporter` | `GitLabExporterConfig` | `gitlab_exporter` |
| `pgbouncer` | `PgbouncerConfig` | `pgbouncer` |
| `patroni` | `PatroniConfig` | `patroni` |
| `consul` | `ConsulConfig` | `consul` |
| `logrotate` | `LogrotateConfig` | `logrotate` |
| `spamcheck` | `SpamcheckConfig` | `spamcheck` |
| `user` | `UserConfig` | `user` |
| `web_server` | `WebServerConfig` | `web_server` |
| `geo_secondary` | `GeoSecondaryConfig` | `geo_secondary` |
| `geo_postgresql` | `GeoPostgresqlConfig` | `geo_postgresql` |
| `pages_nginx` | `PagesNginxConfig` | `pages_nginx` |
| `registry_nginx` | `RegistryNginxConfig` | `registry_nginx` |
| `mattermost_nginx` | `MattermostNginxConfig` | `mattermost_nginx` |
| `mailroom` | `MailroomConfig` | `mailroom` |
| ... | ... | ... |

Plus a **root class** `GitLabOmnibusConfig` that holds all prefix classes as nested properties + standalone URLs.

---

## Phase 3: Code Emitter (`GitLabConfigEmitter`)

### 3.1 Generated Config Classes (Model)

For each prefix group, emit a plain `partial class` (no `[Builder]` attribute — builders are emitted separately by our SG):

```csharp
// Generated: NginxConfig.g.cs
[ExcludeFromCodeCoverage]
public partial class NginxConfig
{
    /// <summary>nginx['listen_port'] — Override only if you use a reverse proxy</summary>
    public int? ListenPort { get; set; }

    /// <summary>nginx['listen_https']</summary>
    public bool? ListenHttps { get; set; }

    /// <summary>nginx['proxy_set_headers']</summary>
    public Dictionary<string, string?>? ProxySetHeaders { get; set; }

    // ... all nginx['*'] settings
}
```

Property names: `NamingHelper.ToPascalCase(rubyKey)` — e.g., `listen_port` → `ListenPort`, `smtp_enable_starttls_auto` → `SmtpEnableStarttlsAuto`.

Doc comments: from `##!` / `###!` lines preceding each setting.

### 3.2 Generated Root Class (Model)

```csharp
// Generated: GitLabOmnibusConfig.g.cs
[ExcludeFromCodeCoverage]
public partial class GitLabOmnibusConfig
{
    /// <summary>external_url 'https://...'</summary>
    public string? ExternalUrl { get; set; }

    /// <summary>registry_external_url 'https://...'</summary>
    public string? RegistryExternalUrl { get; set; }

    /// <summary>pages_external_url 'http://...'</summary>
    public string? PagesExternalUrl { get; set; }

    /// <summary>mattermost_external_url 'http://...'</summary>
    public string? MattermostExternalUrl { get; set; }

    /// <summary>gitlab_kas_external_url 'ws://...'</summary>
    public string? GitLabKasExternalUrl { get; set; }

    public NginxConfig? Nginx { get; set; }
    public GitLabRailsConfig? GitLabRails { get; set; }
    public PostgresqlConfig? Postgresql { get; set; }
    public RedisConfig? Redis { get; set; }
    // ... one property per prefix class
}
```

### 3.3 Generated Builders (via `BuilderEmitter`)

For each model class (root, prefix, and sub-classes), the SG constructs a `BuilderEmitModel` and calls `BuilderEmitter.Emit()` — same pattern as `ComposeBundleGenerator`.

**Recursive emission** — the emitter walks the `GitLabRbObjectNode` tree:
- **Leaf node** → `BuilderPropertyModel` (simple property on parent class)
- **Branch node** → new sub-class + sub-builder, and a `BuilderPropertyModel` on parent referencing it

```csharp
// Inside GitLabOmnibusGenerator — recursive class emission:
void EmitClassAndBuilder(SourceProductionContext ctx, string ns, string className, GitLabRbObjectNode node)
{
    var builderProps = new List<BuilderPropertyModel>();

    foreach (var (key, child) in node.Children)
    {
        var propName = NamingHelper.ToPascalCase(key);

        if (child.LeafType != null)
        {
            // Leaf → simple property
            var csharpType = NamingHelper.MapCSharpType(child.LeafType.Value);
            builderProps.Add(new BuilderPropertyModel(propName, csharpType, csharpType));
        }
        else if (child.IsArrayOfObjects)
        {
            // Array of objects → List<SubClass> + sub-class builder
            var subClassName = className + propName + "Item";
            EmitClassAndBuilder(ctx, ns, subClassName, child);  // recurse
            var listType = $"List<{subClassName}>?";
            builderProps.Add(new BuilderPropertyModel(propName, listType, listType,
                isCollection: true, itemTypeFull: subClassName,
                itemBuilderClassName: subClassName + "Builder"));
        }
        else
        {
            // Branch → sub-class with own builder
            var subClassName = className + propName;
            EmitClassAndBuilder(ctx, ns, subClassName, child);  // recurse
            builderProps.Add(new BuilderPropertyModel(propName,
                subClassName + "?", subClassName + "?",
                itemBuilderClassName: subClassName + "Builder"));
        }
    }

    // Emit model class
    ctx.AddSource($"{className}.g.cs",
        SourceText.From(GitLabConfigEmitter.EmitModelClass(ns, className, node, builderProps), Encoding.UTF8));

    // Emit builder class
    var builderModel = new BuilderEmitModel(ns, className, className + "Builder", builderProps);
    ctx.AddSource($"{className}Builder.g.cs",
        SourceText.From(BuilderEmitter.Emit(builderModel), Encoding.UTF8));
}
```

**Example output** for `gitaly['configuration'] = { auth: { token: '...' }, storage: [...] }`:

```
GitalyConfig.g.cs                          — { Configuration }
GitalyConfigBuilder.g.cs                   — WithConfiguration(Action<...> configure)
GitalyConfigConfiguration.g.cs             — { ListenAddr, Auth, Storage }
GitalyConfigConfigurationBuilder.g.cs      — WithListenAddr(), WithAuth(), WithStorage()
GitalyConfigConfigurationAuth.g.cs         — { Token, Transitioning }
GitalyConfigConfigurationAuthBuilder.g.cs  — WithToken(), WithTransitioning()
GitalyConfigConfigurationStorageItem.g.cs  — { Name, Path }
GitalyConfigConfigurationStorageItemBuilder.g.cs
```

This produces builders with:
- `With*()` fluent methods for every property
- `WithSubObject(Action<SubObjectBuilder> configure)` for nested objects
- `WithItem(Action<ItemBuilder> configure)` for list-of-objects
- `BuildAsync()` → `Result<Reference<T>>`
- Virtual `Validate*()` hooks

For the **root `GitLabOmnibusConfig`**, each prefix class is a property with its builder, so: `WithNginx(Action<NginxConfigBuilder> configure)`, `WithRedis(Action<RedisConfigBuilder> configure)`, etc.

### 3.4 Metadata for Rendering

Each generated class needs metadata to know its Ruby prefix for rendering. Emit a static metadata dictionary:

```csharp
// Generated: GitLabRbMetadata.g.cs
public static class GitLabRbMetadata
{
    public static readonly IReadOnlyDictionary<string, GitLabRbPropertyMeta> Properties = new Dictionary<string, GitLabRbPropertyMeta>
    {
        ["NginxConfig.ListenPort"] = new("nginx", "listen_port", GitLabRbValueKind.Integer),
        ["NginxConfig.ListenHttps"] = new("nginx", "listen_https", GitLabRbValueKind.Boolean),
        ["NginxConfig.ProxySetHeaders"] = new("nginx", "proxy_set_headers", GitLabRbValueKind.StringDict),
        // ...
    };

    public static readonly IReadOnlyList<string> StandaloneUrls = new[] { "external_url", "registry_external_url", "pages_external_url", "mattermost_external_url", "gitlab_kas_external_url" };
}
```

This metadata drives the `GitLabRbRenderer` — it doesn't need to know the Ruby prefixes at compile time; they come from the parsed template.

---

## Phase 4: Ruby Renderer (`GitLabRbRenderer`)

Hand-written (not generated). Uses the generated metadata to render:

```csharp
public static class GitLabRbRenderer
{
    public static string Render(GitLabOmnibusConfig config)
    {
        var sb = new StringBuilder();
        // 1. Standalone URLs
        if (config.ExternalUrl != null)
            sb.AppendLine($"external_url '{config.ExternalUrl}'");
        // ...

        // 2. Per-prefix sections — uses reflection + GitLabRbMetadata
        RenderSection(sb, "nginx", config.Nginx);
        RenderSection(sb, "gitlab_rails", config.GitLabRails);
        // ...
        return sb.ToString();
    }
}
```

Each property rendered as `prefix['key'] = rubyValue` using the metadata for prefix/key mapping and value type for proper Ruby formatting.

---

## Phase 5: Compose Contributors

Same as before (hand-written, not generated):

- `GitLabComposeContributor : IComposeFileContributor` — renders omnibus config → env var, emits ComposeService
- `PostgresqlComposeContributor` — external PostgreSQL service
- `RedisComposeContributor` — external Redis service
- `GitLabRunnerComposeContributor` — GitLab Runner service
- `MinioComposeContributor` — MinIO S3 service

---

## Phase 6: Tests

### Parser Tests (`GitLabRbParser`)
- Parse section headers correctly
- Parse `# prefix['key'] = value` settings
- Parse standalone URLs (`external_url`)
- Parse nested keys `['k1']['k2']`
- Infer types correctly (bool, int, string, list, dict)
- Handle multi-line hash values
- Handle `##!` doc comments

### Emitter Tests
- Generated class has correct namespace
- Property names are PascalCase
- Property types match inferred Ruby types
- Doc comments from `##!` lines are preserved
- Root class has all prefix classes as properties

### Rendering Tests
- Render minimal config (only `external_url`)
- Render behind-Traefik config (nginx proxy headers)
- Render SMTP settings
- Only non-null properties rendered
- Bool → `true`/`false`, int → bare number, string → `"..."`
- Dict → `{ "K" => "V" }` syntax
- Standalone URL → `external_url 'https://...'`

### Contributor Tests
- GitLab service added with correct image, env, volumes, healthcheck
- GITLAB_OMNIBUS_CONFIG contains valid rendered Ruby
- Companion services added correctly

---

## Implementation Order

1. **Phase 0**: `IComposeFileContributor` + `ComposeFileExtensions` in DockerCompose.Bundle
2. **Phase 1**: Project scaffolding — csproj, slnx, directories for all 4 projects (main, SG, Design, Tests)
3. **Phase 1b**: Design project — `GitLabReleasesVersionCollector("gitlab-org%2Fomnibus-gitlab")` + download pipeline, run to populate `resources/`
4. **Phase 2**: `GitLabRbParser` + `GitLabRbModel` — parse gitlab.rb template lines
5. **Phase 2 tests**: Parser unit tests (section detection, setting extraction, type inference, multi-line hash)
6. **Phase 3a**: `GitLabConfigEmitter` — emit config classes per prefix
7. **Phase 3b**: `GitLabOmnibusGenerator : IIncrementalGenerator` — orchestrator (parse → merge versions → emit)
8. **Phase 3c**: `GitLabRbMetadata.g.cs` — emit rendering metadata (prefix, key, value kind per property)
9. **Build**: Verify SG emits both model + builder classes (via `BuilderEmitter.Emit()`)
10. **Phase 4**: `GitLabRbRenderer` — hand-written renderer using generated metadata
11. **Phase 4 tests**: Rendering tests (Ruby syntax correctness)
12. **Phase 5**: Compose contributors (`GitLabComposeContributor`, `PostgresqlComposeContributor`, `RedisComposeContributor`, `GitLabRunnerComposeContributor`, `MinioComposeContributor`)
13. **Phase 5 tests**: Contributor tests + full pipeline test

---

## Special Cases to Handle

1. **`gitlab_rails` is massive** (~400 settings) — single class is fine, BuilderEmitter handles large classes
2. **Nested paths** (`gitlab_rails['object_store']['connection']`) — become sub-classes in the object tree
3. **YAML-embedded LDAP** (`YAML.load <<-'EOS' ... EOS`) — parse as a special hash block
4. **`gitaly['configuration']`** — large Ruby hash with symbol keys, different syntax than bracket-string keys
5. **`praefect['configuration']`** — same pattern as gitaly
6. **`consul['configuration']`** — same pattern
7. **Multi-line hash values** — parser needs to track brace depth `{ ... }`
8. **EE-only sections** — include them (GitLab EE is superset of CE, settings are simply ignored on CE)
9. **`roles`** — `List<string>` standalone assignment

---

## Verification

```bash
# Build (verifies SG pipeline)
dotnet build GitLab.DockerCompose/FrenchExDev.Net.GitLab.DockerCompose.slnx

# Inspect generated files
ls GitLab.DockerCompose/src/FrenchExDev.Net.GitLab.DockerCompose/obj/Generated/

# Run tests
dotnet test GitLab.DockerCompose/FrenchExDev.Net.GitLab.DockerCompose.slnx
```

---

## Key Files to Reference

- `DockerCompose/src/.../ComposeBundleGenerator.cs` — SG pattern reading AdditionalFiles, emitting models + builders
- `DockerCompose/src/.../BuilderHelper.cs` — constructing `BuilderEmitModel` from schema properties
- `Builder/src/.../BuilderEmitter.cs` — `BuilderEmitter.Emit(BuilderEmitModel)` → builder source code
- `Builder/src/.../BuilderEmitModel.cs` — `BuilderEmitModel`, `BuilderPropertyModel` API
- `Packer/src/.../IPackerBundleContributor.cs` — contributor interface pattern
- `Packer.Alpine/src/.../AlpineBaseContributor.cs` — reference contributor impl
- `Wrapper.Versioning/src/...` — `DesignPipeline<T>`, `GitLabReleasesVersionCollector`, `VersionFilters`
- `GitLab.DockerCompose/resources/gitlab.rb` — the 3730-line source of truth
