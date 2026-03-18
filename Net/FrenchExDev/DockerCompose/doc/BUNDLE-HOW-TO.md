# DockerCompose.Bundle - How-To Guide

## Quick Start

### 1. Add a Project Reference

```xml
<ProjectReference Include="..\FrenchExDev.Net.DockerCompose.Bundle\FrenchExDev.Net.DockerCompose.Bundle.csproj" />
```

### 2. Build a Compose File Programmatically

```csharp
using FrenchExDev.Net.DockerCompose.Bundle;

var result = await new ComposeFileBuilder()
    .WithName("my-app")
    .WithServices(new Dictionary<string, ComposeService>
    {
        ["web"] = new ComposeService { Image = "nginx:latest" },
        ["db"] = new ComposeService { Image = "postgres:16" }
    })
    .WithNetworks(new Dictionary<string, ComposeNetwork?>
    {
        ["frontend"] = new ComposeNetwork { Driver = "bridge" }
    })
    .BuildAsync();

if (result.IsSuccess)
{
    var composeFile = result.ValueOrThrow().Resolved();
    // composeFile.Services["web"].Image == "nginx:latest"
}
```

### 3. Build Individual Resources

Each generated model has its own builder:

```csharp
// Build a service
var svcResult = await new ComposeServiceBuilder()
    .WithImage("postgres:16")
    .WithRestart("unless-stopped")
    .WithEnvironment(new Dictionary<string, string?> { ["POSTGRES_DB"] = "mydb" })
    .BuildAsync();

// Build a network
var netResult = await new ComposeNetworkBuilder()
    .WithName("frontend")
    .WithDriver("bridge")
    .BuildAsync();

// Build a volume
var volResult = await new ComposeVolumeBuilder()
    .WithName("pgdata")
    .WithDriver("local")
    .BuildAsync();
```

## Serialization

### Deserialize YAML to Model

```csharp
using FrenchExDev.Net.DockerCompose.Bundle.Serialization;

var yaml = File.ReadAllText("docker-compose.yml");
var result = ComposeSerializer.Deserialize(yaml);

if (result.IsSuccess)
{
    var file = result.Value!;
    foreach (var (name, service) in file.Services)
        Console.WriteLine($"Service: {name}, Image: {service.Image}");
}
else
{
    Console.Error.WriteLine($"Parse error: {result.Error}");
}
```

### Serialize Model to YAML

```csharp
using FrenchExDev.Net.DockerCompose.Bundle.Model;
using FrenchExDev.Net.DockerCompose.Bundle.Serialization;

var file = new ComposeFile();
file.Services["web"] = new Service { Image = "nginx:latest" };

var yaml = ComposeSerializer.Serialize(file);
File.WriteAllText("docker-compose.yml", yaml);
```

### Round-Trip

```csharp
var original = File.ReadAllText("docker-compose.yml");
var parsed = ComposeSerializer.Deserialize(original);
var serialized = ComposeSerializer.Serialize(parsed.Value!);
// serialized is semantically equivalent to original
```

## Union Types

The compose-spec uses union types extensively. The generated models handle these transparently:

### Ports (Short Syntax vs Long Syntax)

```yaml
# Short syntax
ports:
  - "8080:80"

# Long syntax
ports:
  - target: 80
    published: "8080"
    protocol: tcp
```

```csharp
// Access after deserialization:
var port = service.Ports![0];
port.Published;  // "8080:80" (short) or "8080" (long)
port.Target;     // null (short) or 80 (long)
port.Protocol;   // null (short) or "tcp" (long)
```

### Environment (List vs Map)

```yaml
# As map
environment:
  FOO: bar

# As list
environment:
  - FOO=bar
```

```csharp
var env = service.Environment!;
env.IsDictionary;      // true for map form
env.IsList;            // true for list form
env.ToDictionary();    // works for both forms: {"FOO": "bar"}
```

### depends_on (List vs Map with Conditions)

```yaml
# Simple list
depends_on:
  - db
  - redis

# Map with conditions
depends_on:
  db:
    condition: service_healthy
```

```csharp
var deps = service.DependsOn!;
deps.IsList;                     // true for list form
deps.List;                       // ["db", "redis"]
deps.Map["db"].Condition;        // "service_healthy" (map form)
```

### Build (String vs Object)

```yaml
# String shorthand
build: ./app

# Full object
build:
  context: .
  dockerfile: Dockerfile
```

```csharp
var build = service.Build!;
build.IsString;                  // true for string form
build.StringValue;               // "./app"
build.IsObject;                  // true for object form
build.ObjectValue.Context;       // "."
build.ObjectValue.Dockerfile;    // "Dockerfile"
```

## Version Awareness

### Query Available Versions

```csharp
// All 32 supported schema versions
IReadOnlyList<string> versions = ComposeSchemaVersions.Available;

string latest = ComposeSchemaVersions.Latest;   // "2.10.1"
string oldest = ComposeSchemaVersions.Oldest;   // "1.0.9"
```

### Parse and Compare Versions

```csharp
var v = ComposeSchemaVersion.Parse("v2.10.1");
// v.Major == 2, v.Minor == 10, v.Patch == 1

var v1 = new ComposeSchemaVersion(1, 0, 0);
var v2 = new ComposeSchemaVersion(2, 0, 0);
bool newer = v2 > v1;  // true
```

### Check Feature Availability

Properties carry `[SinceVersion]` and `[UntilVersion]` attributes:

```csharp
// ComposeFile.Name has [SinceVersion("1.1.0")]
// ComposeFile.Models has [SinceVersion("2.7.1")]
// ComposeFile.Version has [Obsolete("Deprecated by compose-spec.")]

// Use reflection to check version bounds:
var prop = typeof(ComposeFile).GetProperty("Name");
var since = prop?.GetCustomAttribute<SinceVersionAttribute>();
// since.Version == "1.1.0"
```

### Load Raw JSON Schemas

```csharp
// Load embedded JSON schema for a specific version
var versions = ComposeSchema.GetAvailableVersions();
var schema = ComposeSchema.GetSchema(versions[^1]);   // latest
var root = schema.RootElement;

// Or load the latest directly
var latestSchema = ComposeSchema.GetLatestSchema();
```

### Detect Deprecations

```csharp
using FrenchExDev.Net.DockerCompose.Bundle.Versioning;

var file = new Model.ComposeFile { Version = "3.8" };
file.Services["web"] = new Model.Service { Image = "nginx" };

var warnings = SchemaVersionDetector.GetDeprecationWarnings(file);
foreach (var w in warnings)
    Console.WriteLine($"Deprecated: {w.Field}");
// Output: "Deprecated: version"
```

### Compare Schema Versions

```csharp
using FrenchExDev.Net.DockerCompose.Bundle.Versioning;

var versions = ComposeSchema.GetAvailableVersions();
var diff = SchemaDiffer.Compare(versions[0], versions[^1]);

Console.WriteLine($"From: {diff.From} -> To: {diff.To}");
foreach (var added in diff.AddedProperties)
    Console.WriteLine($"  + {added}");
```

## Custom Validation

Builders generate `virtual` validation methods you can override:

```csharp
public class StrictComposeFileBuilder : ComposeFileBuilder
{
    protected override IEnumerable<Exception>? ValidateServices(
        Dictionary<string, ComposeService>? value)
    {
        if (value is null || value.Count == 0)
            yield return new ArgumentException("At least one service is required.");
    }

    protected override IEnumerable<Exception>? ValidateName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            yield return new ArgumentException("Compose file must have a name.");
    }
}

// Usage:
var result = await new StrictComposeFileBuilder()
    .WithName("my-app")
    .WithServices(new Dictionary<string, ComposeService>
    {
        ["web"] = new ComposeService { Image = "nginx" }
    })
    .BuildAsync();
```

For collection items, override `Validate{Prop}Item`:

```csharp
public class SafeServiceBuilder : ComposeServiceBuilder
{
    protected override IEnumerable<Exception>? ValidateCapAddItem(string item, int index)
    {
        var forbidden = new[] { "CAP_SYS_ADMIN", "SYS_ADMIN" };
        if (forbidden.Contains(item))
            yield return new ArgumentException($"Capability {item} is not allowed.");
    }
}
```

## Extension Fields

Docker Compose supports `x-` prefixed extension fields at every level:

```yaml
x-common-env: &common-env
  LOG_LEVEL: info

services:
  web:
    image: nginx
    x-custom-metadata:
      team: platform
```

```csharp
// Access extensions on any model
var file = result.Value!;
file.Extensions?["x-common-env"];  // Dictionary<string, object?>

// Set extensions via builder
var result = await new ComposeFileBuilder()
    .WithExtensions(new Dictionary<string, object?>
    {
        ["x-common-env"] = new Dictionary<string, object?> { ["LOG_LEVEL"] = "info" }
    })
    .BuildAsync();
```

## Complete Example

```csharp
using FrenchExDev.Net.DockerCompose.Bundle;

var result = await new ComposeFileBuilder()
    .WithName("fullstack-app")
    .WithServices(new Dictionary<string, ComposeService>
    {
        ["web"] = new ComposeService
        {
            Image = "nginx:alpine",
            Ports = new List<ComposeServicePortsConfig>
            {
                new() { Target = 80, Published = "8080", Protocol = "tcp" }
            },
            DependsOn = /* list of service names */,
            Deploy = new ComposeDeployment
            {
                Replicas = 3,
                Resources = new ComposeDeploymentResources
                {
                    Limits = new ComposeDeploymentResourcesLimits
                    {
                        Cpus = "0.5",
                        Memory = "256M"
                    }
                }
            },
            Healthcheck = new ComposeHealthcheck
            {
                Test = new List<string> { "CMD", "curl", "-f", "http://localhost/" },
                Interval = "30s",
                Timeout = "10s",
                Retries = 3
            }
        },
        ["db"] = new ComposeService
        {
            Image = "postgres:16-alpine",
            Restart = "unless-stopped",
            Volumes = new List<ComposeServiceVolumesConfig>
            {
                new() { Source = "pgdata", Target = "/var/lib/postgresql/data", Type = "volume" }
            }
        }
    })
    .WithNetworks(new Dictionary<string, ComposeNetwork?>
    {
        ["frontend"] = new ComposeNetwork { Driver = "bridge" },
        ["backend"] = new ComposeNetwork { Driver = "bridge", Internal = true }
    })
    .WithVolumes(new Dictionary<string, ComposeVolume?>
    {
        ["pgdata"] = new ComposeVolume { Driver = "local" }
    })
    .BuildAsync();

if (result.IsSuccess)
{
    var file = result.ValueOrThrow().Resolved();
    var yaml = ComposeSerializer.Serialize(file);
    Console.WriteLine(yaml);
}
```
