# GitLab.Ci.Yaml -- How-To Guide

## 1. Build a Pipeline from Scratch

### Minimal pipeline

```csharp
var file = new GitLabCiFile
{
    Stages = new List<object> { "build", "test" },
    Jobs = new Dictionary<string, GitLabCiJob>
    {
        ["build"] = new GitLabCiJob
        {
            Script = new List<string> { "dotnet build" }
        },
        ["test"] = new GitLabCiJob
        {
            Script = new List<string> { "dotnet test" }
        }
    }
};

var yaml = GitLabCiYamlWriter.Serialize(file);
```

Output:
```yaml
stages:
- build
- test
build:
  script:
  - dotnet build
test:
  script:
  - dotnet test
```

### Using the fluent builder

```csharp
var result = await new GitLabCiFileBuilder()
    .WithStages(new List<object> { "build", "test", "deploy" })
    .WithVariables(new Dictionary<string, object?>
    {
        ["DOTNET_VERSION"] = "9.0",
        ["CONFIGURATION"] = "Release"
    })
    .WithJob("build", job => job
        .WithImage("mcr.microsoft.com/dotnet/sdk:9.0")
        .WithScript(new List<string>
        {
            "dotnet restore",
            "dotnet build -c $CONFIGURATION"
        })
        .WithArtifacts(new GitLabCiArtifacts
        {
            Paths = new List<string> { "bin/", "obj/" }
        }))
    .WithJob("test", job => job
        .WithImage("mcr.microsoft.com/dotnet/sdk:9.0")
        .WithScript(new List<string> { "dotnet test --logger trx" })
        .WithArtifacts(new GitLabCiArtifacts
        {
            Reports = new GitLabCiArtifactsReports
            {
                Junit = new List<string> { "junit.xml" },
                CoverageReport = new List<string> { "coverage/cobertura.xml" }
            }
        }))
    .BuildAsync();

var ciFile = result.ValueOrThrow().Resolved();
```

---

## 2. Read an Existing Pipeline

```csharp
using FrenchExDev.Net.GitLab.Ci.Yaml.Serialization;

// From file
var yaml = File.ReadAllText(".gitlab-ci.yml");
var ciFile = GitLabCiYamlReader.Deserialize(yaml);

// From TextReader
using var reader = new StreamReader(".gitlab-ci.yml");
var ciFile2 = GitLabCiYamlReader.Deserialize(reader);
```

### Inspect the parsed pipeline

```csharp
Console.WriteLine($"Stages: {string.Join(", ", ciFile.Stages ?? new())}");
Console.WriteLine($"Jobs: {ciFile.Jobs?.Count ?? 0}");

foreach (var (name, job) in ciFile.Jobs ?? new())
{
    Console.WriteLine($"  {name}:");
    Console.WriteLine($"    image: {job.Image}");
    Console.WriteLine($"    script: {string.Join("; ", job.Script ?? new())}");
}
```

### Known limitations

- Properties with union types (`string | object`) may lose type fidelity during deserialization
- The `StringOrListConverter` handles `script`-like fields (`string` -> `List<string>` coercion)
- Deeply nested or custom YAML structures fall back to `object?` types

---

## 3. Write a Pipeline to YAML

```csharp
using FrenchExDev.Net.GitLab.Ci.Yaml.Serialization;

// To string
var yaml = GitLabCiYamlWriter.Serialize(ciFile);

// To TextWriter (file, stream, etc.)
using var writer = new StreamWriter("output.gitlab-ci.yml");
GitLabCiYamlWriter.Serialize(ciFile, writer);
```

### Writer behavior

- **Root-level jobs**: Jobs are emitted as top-level YAML keys, not nested under `jobs:`
- **Null omission**: Properties with `null` values are omitted
- **Empty collection omission**: Empty lists and dictionaries are omitted
- **Reserved keys first**: `stages`, `variables`, `include`, `default`, `workflow` are emitted before job entries
- **Snake_case keys**: Property names are converted from PascalCase to snake_case

---

## 4. Use Contributors

Contributors implement `IGitLabCiContributor` to modularly build pipelines:

```csharp
public class DockerBuildContributor : IGitLabCiContributor
{
    private readonly string _imageName;

    public DockerBuildContributor(string imageName) => _imageName = imageName;

    public void Contribute(GitLabCiFile ciFile)
    {
        ciFile.Stages ??= new List<object>();
        if (!ciFile.Stages.Contains("docker"))
            ciFile.Stages.Add("docker");

        ciFile.Jobs ??= new Dictionary<string, GitLabCiJob>();
        ciFile.Jobs["docker-build"] = new GitLabCiJob
        {
            Image = "docker:24",
            Script = new List<string>
            {
                $"docker build -t {_imageName} .",
                $"docker push {_imageName}"
            }
        };
    }
}
```

Apply contributors:

```csharp
var ciFile = new GitLabCiFile()
    .Apply(new DotNetContributor())
    .Apply(new DockerBuildContributor("registry.example.com/app:latest"));

var yaml = GitLabCiYamlWriter.Serialize(ciFile);
```

---

## 5. Download New Schema Versions

The Design project downloads schemas from GitLab releases:

```bash
# List available versions
dotnet run --project src/FrenchExDev.Net.GitLab.Ci.Yaml.Design -- --list

# Download all schemas (min version 18.0)
dotnet run --project src/FrenchExDev.Net.GitLab.Ci.Yaml.Design
```

Schemas are saved to `src/FrenchExDev.Net.GitLab.Ci.Yaml/schemas/gitlab-ci-v{version}.json` and automatically picked up by the source generator on next build.

To change the minimum version, edit `DefaultMinVersion` in `src/FrenchExDev.Net.GitLab.Ci.Yaml.Design/Program.cs`.

---

## 6. Check Version Availability

```csharp
// List all supported schema versions
foreach (var v in GitLabCiSchemaVersions.Available)
    Console.WriteLine(v);

Console.WriteLine($"Latest: {GitLabCiSchemaVersions.Latest}");
Console.WriteLine($"Oldest: {GitLabCiSchemaVersions.Oldest}");
```

Check if a property or class is available for a specific GitLab version using reflection:

```csharp
var prop = typeof(GitLabCiJob).GetProperty("Run");
var since = prop?.GetCustomAttribute<SinceVersionAttribute>();
if (since is not null)
    Console.WriteLine($"'run' available since GitLab {since.Version}");
```

---

## 7. Working with Job Properties

### Image

```csharp
// Image is a string (the SG maps the $ref to string type)
new GitLabCiJob { Image = "node:20-alpine" }
```

### Script (string or list)

```csharp
// Always use List<string> in the model
new GitLabCiJob
{
    Script = new List<string> { "npm ci", "npm run build" },
    BeforeScript = new List<string> { "echo 'Setting up...'" },
    AfterScript = new List<string> { "echo 'Cleaning up...'" }
}
```

The reader handles both forms:
```yaml
# Both are valid and deserialized correctly
script: echo hello          # single string -> List<string> { "echo hello" }
script:
  - npm ci                  # list -> List<string> { "npm ci", "npm run build" }
  - npm run build
```

### Artifacts

```csharp
new GitLabCiJob
{
    Artifacts = new GitLabCiArtifacts
    {
        Paths = new List<string> { "dist/", "coverage/" },
        ExpireIn = "1 week",
        When = "always",
        Reports = new GitLabCiArtifactsReports
        {
            Junit = new List<string> { "junit.xml" },
            CoverageReport = new List<string> { "coverage/cobertura.xml" }
        }
    }
}
```

### Cache

```csharp
new GitLabCiFile
{
    Cache = new List<object>
    {
        new GitLabCiCacheItem
        {
            Key = "$CI_COMMIT_REF_SLUG",
            Paths = new List<string> { "node_modules/", ".npm/" },
            Policy = "pull-push"
        }
    }
}
```

### Variables

```csharp
new GitLabCiFile
{
    Variables = new Dictionary<string, object?>
    {
        ["NODE_ENV"] = "production",
        ["DOCKER_TLS_CERTDIR"] = ""
    }
}
```

---

## 8. Run Tests

```bash
dotnet test GitLab.Ci.Yaml/FrenchExDev.Net.GitLab.Ci.Yaml.slnx
```

20 tests covering:
- Version parsing and comparison
- Model instantiation and contributor pattern
- YAML serialization (stages, jobs, null omission, TextWriter)
- YAML deserialization (fixture file, job extraction, string-or-list handling)
