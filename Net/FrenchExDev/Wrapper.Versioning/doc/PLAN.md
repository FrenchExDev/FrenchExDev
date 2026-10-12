# Plan: Extract FrenchExDev.Net.JsonSchema.Design

## Context

`DockerCompose.Bundle.Design/Program.cs` is a 90-line standalone script that downloads versioned JSON schemas from GitHub. It depends on `BinaryWrapper.Design` solely for `GitHubReleasesVersionCollector`, and reimplements parallel downloading, CLI arg parsing, and progress reporting from scratch.

The goal is to create a reusable `FrenchExDev.Net.JsonSchema.Design` library (following the `BinaryWrapper.Design.Lib` middleware pipeline pattern) so that any project needing versioned schema scraping (OpenAPI, AsyncAPI, Kubernetes CRDs, etc.) gets parallel downloads, CLI commands, and progress tracking for free.

Additionally, `IVersionCollector` and its implementations are generic utilities misplaced in `BinaryWrapper.Design`. They move to a new `FrenchExDev.Net.Wrapper.Versioning` package — naming the actual concern (version discovery for wrappers).

## Dependency Graph (After)

```
Wrapper.Versioning  <── BinaryWrapper.Design ──> BinaryWrapper.Design.Lib
       ^                                                ^
       |                               Podman, Packer, Vagrant, GitLab.Cli
       |
       └── JsonSchema.Design  <── DockerCompose.Bundle.Design
```

---

## Phase 1: Create `FrenchExDev.Net.Wrapper.Versioning`

Extract generic version-collection types from `BinaryWrapper.Design/Code.cs` into a new shared package.

### New files

- `Net/FrenchExDev/Wrapper.Versioning/src/FrenchExDev.Net.Wrapper.Versioning/FrenchExDev.Net.Wrapper.Versioning.csproj`
  - `net10.0`, no NuGet deps (HttpClient is in-box)
  - Namespace: `FrenchExDev.Net.Wrapper.Versioning`

- `Net/FrenchExDev/Wrapper.Versioning/src/FrenchExDev.Net.Wrapper.Versioning/Code.cs`
  - Move from `BinaryWrapper/src/.../Code.cs` (lines ~1955-2297):
    - `IVersionCollector` interface
    - `StaticVersionCollector`
    - `GitHubReleasesVersionCollector` (+ `CompareVersionStrings`)
    - `GitLabReleasesVersionCollector`
    - `GitHubTagsVersionCollector`
  - Change namespace to `FrenchExDev.Net.Wrapper.Versioning`

### Modified files

- `BinaryWrapper/src/FrenchExDev.Net.BinaryWrapper.Design/Code.cs`
  - Remove the 5 extracted types
  - Add `using FrenchExDev.Net.Wrapper.Versioning;`

- `BinaryWrapper/src/FrenchExDev.Net.BinaryWrapper.Design/FrenchExDev.Net.BinaryWrapper.Design.csproj`
  - Add `<ProjectReference>` to `Wrapper.Versioning`

- `BinaryWrapper/src/FrenchExDev.Net.BinaryWrapper.Design.Lib/FrenchExDev.Net.BinaryWrapper.Design.Lib.csproj`
  - Likely no change (transitive via Design)

- All BinaryWrapper consumer `.Design/Program.cs` files that directly reference collectors:
  - Add `using FrenchExDev.Net.Wrapper.Versioning;` where needed

### Verification
- Build BinaryWrapper solution: all existing tests pass
- Build all consumer Design projects (Podman, Packer, Vagrant, GitLab.Cli)

---

## Phase 2: Create `FrenchExDev.Net.JsonSchema.Design`

### Project structure

```
Net/FrenchExDev/JsonSchema/
  FrenchExDev.Net.JsonSchema.slnx
  src/FrenchExDev.Net.JsonSchema.Design/
    FrenchExDev.Net.JsonSchema.Design.csproj    # net10.0, refs Wrapper.Versioning + Spectre.Console
    Code.cs                                      # All types below
  doc/
    PLAN.md                                      # This plan
```

### Types (all in `Code.cs`)

#### Delegate & Context

```csharp
namespace FrenchExDev.Net.JsonSchema.Design;

public delegate Task SchemaVersionDelegate(SchemaVersionContext ctx);

public sealed class SchemaVersionContext
{
    public required string Version { get; init; }
    public required string OutputDir { get; init; }
    public required ILogger Logger { get; init; }
    public required HttpClient HttpClient { get; init; }

    // Mutable state set by middleware
    public string? Content { get; set; }
    public string? OutputFilePath { get; set; }

    // Progress tracking
    public SchemaVersionProgressInfo? Progress { get; init; }
}

public sealed class SchemaVersionProgressInfo
{
    public string Version { get; }
    public string Stage { get; }           // Pending, Downloading, Transforming, Saving, Done, Failed
    public TimeSpan Elapsed { get; }
    public string? Error { get; }
    // Thread-safe setters (same pattern as BinaryWrapper's VersionProgressInfo)
}
```

#### Pipeline Builder

```csharp
public sealed class SchemaDesignPipeline
{
    public SchemaDesignPipeline Use(Func<SchemaVersionDelegate, SchemaVersionDelegate> middleware);
    public SchemaVersionDelegate Build();
}
```

Same LIFO middleware composition as `BinaryWrapper.Design.Lib.DesignPipeline`.

#### Built-in Middleware Extensions

```csharp
public static class SchemaDesignPipelineExtensions
{
    /// Downloads schema content via HTTP GET. Sets ctx.Content.
    public static SchemaDesignPipeline UseHttpDownload(
        this SchemaDesignPipeline pipeline,
        Func<string, string> urlBuilder);       // version -> URL

    /// Transforms downloaded content. Reads/writes ctx.Content.
    public static SchemaDesignPipeline UseContentTransform(
        this SchemaDesignPipeline pipeline,
        Func<string, string, string> transform);  // (version, content) -> transformed

    /// Writes ctx.Content to disk. Sets ctx.OutputFilePath.
    /// Pattern supports {version} placeholder.
    public static SchemaDesignPipeline UseSave(
        this SchemaDesignPipeline pipeline,
        string? outputFilePattern = null);       // null = use runner's pattern
}
```

#### Runner

```csharp
public sealed class SchemaDesignPipelineRunner
{
    public required IVersionCollector VersionCollector { get; init; }
    public required SchemaVersionDelegate Pipeline { get; init; }
    public required string OutputDir { get; init; }

    // Optional configuration
    public string? OutputFilePattern { get; init; }
    public int DefaultParallelism { get; init; } = 6;
    public string UserAgent { get; init; } = "FrenchExDev-SchemaScrape/1.0";
    public string? AuthTokenEnvVar { get; init; } = "GITHUB_TOKEN";
    public Func<IReadOnlyList<string>, IReadOnlyList<string>>? VersionFilter { get; init; }
    public LogLevel MinLogLevel { get; init; } = LogLevel.Information;

    /// Parses CLI args, collects versions, runs pipeline in parallel.
    /// Supported args: --list, --missing, --parallel N
    public Task<int> RunAsync(string[] args);
}
```

**RunAsync internals** (following `DesignPipelineRunner` pattern):
1. Parse CLI args (`--list`, `--missing`, `--parallel N`)
2. Collect versions via `VersionCollector.CollectVersionsAsync()`
3. Apply `VersionFilter` if set
4. If `--list`: print versions, return 0
5. If `--missing`: filter to versions without existing output file
6. Create `HttpClient` with `UserAgent` + bearer token from `AuthTokenEnvVar` env var
7. Parallel execution via `SemaphoreSlim(parallelism)` + `Task.WhenAll`
8. For each version: create `SchemaVersionContext`, execute pipeline delegate
9. Print summary (downloaded / skipped / failed), return exit code

#### Version Filters

```csharp
public static class VersionFilters
{
    /// Keeps only the highest patch for each major.minor group.
    /// E.g., [1.0.1, 1.0.3, 1.1.0, 1.1.2] -> [1.0.3, 1.1.2]
    public static IReadOnlyList<string> LatestPatchPerMinor(IReadOnlyList<string> versions);
}
```

Encapsulates the logic currently inline in Bundle.Design `Program.cs` lines 13-26.

---

## Phase 3: Refactor `DockerCompose.Bundle.Design`

### Modified files

- `DockerCompose/src/FrenchExDev.Net.DockerCompose.Bundle.Design/FrenchExDev.Net.DockerCompose.Bundle.Design.csproj`
  - Remove: `<ProjectReference>` to `BinaryWrapper.Design`
  - Add: `<ProjectReference>` to `JsonSchema.Design`

- `DockerCompose/src/FrenchExDev.Net.DockerCompose.Bundle.Design/Program.cs`
  - Replace 90-line script with ~15-line consumer:

```csharp
using FrenchExDev.Net.Wrapper.Versioning;
using FrenchExDev.Net.JsonSchema.Design;

var outputDir = Path.GetFullPath(Path.Combine(
    AppContext.BaseDirectory, "..", "..", "..", "..",
    "FrenchExDev.Net.DockerCompose.Bundle", "schemas"));

var pipeline = new SchemaDesignPipeline()
    .UseHttpDownload(v =>
        $"https://raw.githubusercontent.com/compose-spec/compose-go/v{v}/schema/compose-spec.json")
    .UseSave()
    .Build();

return await new SchemaDesignPipelineRunner
{
    VersionCollector = new GitHubReleasesVersionCollector("compose-spec", "compose-go"),
    Pipeline = pipeline,
    VersionFilter = VersionFilters.LatestPatchPerMinor,
    OutputDir = outputDir,
    OutputFilePattern = "compose-spec-v{version}.json",
}.RunAsync(args);
```

### Verification
- `dotnet run --project DockerCompose/src/FrenchExDev.Net.DockerCompose.Bundle.Design -- --list` should print 32 versions
- `dotnet run --project DockerCompose/src/FrenchExDev.Net.DockerCompose.Bundle.Design -- --missing` should show 0 missing (schemas already on disk)
- Delete one schema, re-run with `--missing` — should re-download just that one

---

## Phase 4: Namespace fixups in BinaryWrapper consumers

Update `using` statements in consumer Design projects that directly reference `IVersionCollector` or collectors:
- `Podman/src/FrenchExDev.Net.Podman.Design/Program.cs`
- `Packer/src/FrenchExDev.Net.Packer.Design/Program.cs`
- `Vagrant/src/FrenchExDev.Net.Vagrant.Design/Program.cs`
- `GitLab.Cli/src/FrenchExDev.Net.GitLab.Cli.Design/Program.cs`

Add `using FrenchExDev.Net.Wrapper.Versioning;` where the old `FrenchExDev.Net.BinaryWrapper.Design` namespace no longer provides the collector types.

### Verification
- Build each consumer Design project
- Run BinaryWrapper test suite

---

## Critical files

| File | Role |
|------|------|
| `BinaryWrapper/src/.../Design/Code.cs` | Source of IVersionCollector + 4 collectors to extract |
| `BinaryWrapper/src/.../Design.Lib/DesignPipeline.cs` | Pattern for middleware chain |
| `BinaryWrapper/src/.../Design.Lib/DesignPipelineRunner.cs` | Pattern for parallel runner + CLI args |
| `BinaryWrapper/src/.../Design.Lib/VersionProgressInfo.cs` | Pattern for progress tracking |
| `DockerCompose/src/.../Bundle.Design/Program.cs` | Current 90-line script to refactor |
| `Net/FrenchExDev/Directory.Packages.props` | CPM — add Spectre.Console if not already present |

## Reusable code to leverage

- `DesignPipeline` middleware pattern (copy structure, not code — different delegate type)
- `DesignPipelineRunner.RunAsync` CLI arg parsing and parallel execution logic
- `VersionProgressInfo` thread-safe progress tracking pattern
- `GitHubReleasesVersionCollector.CompareVersionStrings` — reused in `VersionFilters`
