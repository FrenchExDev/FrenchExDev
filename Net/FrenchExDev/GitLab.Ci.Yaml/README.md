# GitLab.Ci.Yaml

Schema-driven, source-generated library for building typed `.gitlab-ci.yml` files in C#. Produces fully typed models and fluent builders from the official GitLab CI JSON Schema, with YAML reader and writer support.

## What This Project Does

`GitLab.Ci.Yaml` generates C# classes from the [GitLab CI JSON Schema](https://gitlab.com/gitlab-org/gitlab/-/blob/master/app/assets/javascripts/editor/schema/ci.json) using a Roslyn incremental source generator. The generated code provides:

- **Typed models** for every CI concept (jobs, artifacts, cache, rules, triggers, environments, etc.)
- **Fluent builders** with `With*()` methods, validation, and async build support
- **Version tracking** across GitLab releases (18.0 -- 18.10) with `[SinceVersion]` / `[UntilVersion]` attributes
- **YAML writer** that serializes models to valid `.gitlab-ci.yml` (jobs at root level, null omission)
- **YAML reader** that deserializes existing `.gitlab-ci.yml` files back into typed models
- **Contributor pattern** (`IGitLabCiContributor`) for composable pipeline construction

## Quick Start

### Build a pipeline programmatically

```csharp
using FrenchExDev.Net.GitLab.Ci.Yaml;
using FrenchExDev.Net.GitLab.Ci.Yaml.Serialization;

var result = await new GitLabCiFileBuilder()
    .WithStages(new List<object> { "build", "test", "deploy" })
    .WithJob("build", job => job
        .WithImage("node:20")
        .WithScript(new List<string> { "npm ci", "npm run build" }))
    .WithJob("test", job => job
        .WithImage("node:20")
        .WithScript(new List<string> { "npm test" }))
    .WithJob("deploy", job => job
        .WithScript(new List<string> { "echo deploying..." }))
    .BuildAsync();

var ciFile = result.ValueOrThrow().Resolved();
var yaml = GitLabCiYamlWriter.Serialize(ciFile);
// yaml is a valid .gitlab-ci.yml string
```

### Read an existing pipeline

```csharp
var yaml = File.ReadAllText(".gitlab-ci.yml");
var ciFile = GitLabCiYamlReader.Deserialize(yaml);

foreach (var (name, job) in ciFile.Jobs!)
    Console.WriteLine($"Job: {name}, scripts: {job.Script?.Count ?? 0}");
```

### Use contributors for composable pipelines

```csharp
public class DotNetContributor : IGitLabCiContributor
{
    public void Contribute(GitLabCiFile ciFile)
    {
        ciFile.Stages ??= new List<object>();
        ciFile.Stages.Add("build");
        ciFile.Stages.Add("test");

        ciFile.Jobs ??= new Dictionary<string, GitLabCiJob>();
        ciFile.Jobs["dotnet-build"] = new GitLabCiJob
        {
            Image = "mcr.microsoft.com/dotnet/sdk:9.0",
            Script = new List<string> { "dotnet build", "dotnet test" }
        };
    }
}

var ciFile = new GitLabCiFile()
    .Apply(new DotNetContributor());
```

## Schema Versions

Schemas are downloaded from GitLab releases (`v18.0.0-ee` through `v18.10.0-ee`) using the Design project. The source generator merges all versions into a unified API where each property and class carries version metadata:

| Version Range | Schemas |
|--------------|---------|
| 18.0 -- 18.10 | 11 versioned schemas |

Properties introduced after 18.0 are annotated with `[SinceVersion("18.x.0")]`. Properties removed before 18.10 are annotated with `[UntilVersion("18.x.0")]`.

## Generated Types (30 model classes, 30 builders)

| Model | Description |
|-------|-------------|
| `GitLabCiFile` | Root pipeline (stages, variables, include, default, workflow, jobs) |
| `GitLabCiJob` / `GitLabCiJobTemplate` | Job definition (script, image, artifacts, rules, needs, etc.) |
| `GitLabCiArtifacts` | Build artifacts (paths, reports, expiry) |
| `GitLabCiCacheItem` | Cache configuration (key, paths, policy) |
| `GitLabCiDefault` | Default job settings (image, services, before/after_script) |
| `GitLabCiWorkflow` | Workflow rules and auto-cancel |
| `GitLabCiSpec` | Pipeline specification (inputs) |
| `GitLabCiHooks` | Job hooks (pre_get_sources_script) |
| + 22 inline/nested models | Environment, release, trigger, inherit, needs, etc. |

## Project Structure

```
GitLab.Ci.Yaml/
  src/
    FrenchExDev.Net.GitLab.Ci.Yaml/                Main library (models + builders + reader/writer)
    FrenchExDev.Net.GitLab.Ci.Yaml.Attributes/     [GitLabCiBundle] marker attribute
    FrenchExDev.Net.GitLab.Ci.Yaml.Design/         Schema downloader CLI
    FrenchExDev.Net.GitLab.Ci.Yaml.SourceGenerator/ Incremental source generator
  test/
    FrenchExDev.Net.GitLab.Ci.Yaml.Tests/          20 tests (models, builders, writer, reader)
```

## Documentation

- [ARCHITECTURE.md](doc/ARCHITECTURE.md) -- project structure, source generator pipeline, dependency graph
- [HOW-TO.md](doc/HOW-TO.md) -- usage guide with examples (build, read, write, extend)
- [PHILOSOPHY.md](doc/PHILOSOPHY.md) -- design decisions, why schema-driven, why generated
