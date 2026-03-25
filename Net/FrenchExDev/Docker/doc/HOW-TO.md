# How-To Guide

Practical instructions for using, extending, and maintaining FrenchExDev.Net.Docker.

## Using the Library

### Create a client

```csharp
using FrenchExDev.Net.BinaryWrapper;
using FrenchExDev.Net.Docker;

var binding = new BinaryBinding
{
    Identifier = new BinaryIdentifier("docker"),
    ExecutablePath = "/usr/local/bin/docker",
    DetectedVersion = SemanticVersion.Parse("27.0.0")
};

var client = Docker.Create(binding);
```

The `DetectedVersion` enables runtime version checks. If omitted (null), all commands and options are available without version gating.

### Run a container

```csharp
var cmd = await client.Container.RunAsync(b => b
    .WithDetach(true)
    .WithName("my-app")
    .WithRm(true)
    .WithPublishAll(true)
    .WithHostname("myhost")
    .WithWorkdir("/app")
    .WithUser("nobody")
    .WithTty(true)
    .WithInteractive(true));

// Inspect the generated arguments
Console.WriteLine(string.Join(" ", cmd.ToArguments()));
// --detach --name my-app --rm --publish-all --hostname myhost --workdir /app --user nobody --tty --interactive
```

### List containers

```csharp
var cmd = await client.Container.LsAsync(b => b
    .WithAll(true)
    .WithFormat("json")
    .WithQuiet(true)
    .WithNoTrunc(true)
    .WithSize(true));
```

### Build an image

```csharp
var cmd = await client.Builder.BuildAsync(b => b
    .WithFile("Dockerfile.prod")
    .WithTag(["myapp:latest", "myapp:1.0"])
    .WithCacheFrom(["registry/image:latest"]));
```

### Navigate command groups

```csharp
// Image operations
var imgCmd = await client.Image.LsAsync(b => b.WithAll(true));

// Network operations
var netCmd = await client.Network.CreateAsync(b => b.WithDriver("bridge"));

// Volume operations
var volCmd = await client.Volume.LsAsync(b => b.WithFormat("json"));

// Swarm operations
var swarmCmd = await client.Swarm.InitAsync(b => b.WithAdvertiseAddr("eth0"));

// Nested groups
var trustCmd = await client.Trust.Key.LoadAsync(b => { });
```

### Execute a command

```csharp
var executor = new CommandExecutor(new SystemProcessRunner());
var result = await executor.ExecuteAsync(binding, cmd, cancellationToken);

// result.ExitCode, result.StandardOutput, result.StandardError
```

### Use commands directly (without client)

```csharp
// Create a command directly
var cmd = new DockerContainerRunCommand
{
    Detach = true,
    Name = "my-container",
    Tty = true
};

// Or use the builder
var builder = new DockerContainerRunCommandBuilder()
    .WithDetach(true)
    .WithName("my-container")
    .WithTty(true);
var result = await builder.BuildAsync();
var cmd2 = result.ValueOrThrow().Resolved();
```

## Scraping New Versions

### Prerequisites

- A container runtime (podman or docker) must be available
- Internet access to download Docker static binaries
- Optional: `GITHUB_TOKEN` for higher GitHub API rate limits

### Full scrape

```bash
cd Docker/
dotnet run --project src/FrenchExDev.Net.Docker.Design
```

This runs the two-phase pipeline:
1. Queries GitHub for Docker CLI tags
2. Builds Alpine images with each version's static binary
3. Scrapes `docker --help` recursively for each version
4. Writes JSON files to `src/FrenchExDev.Net.Docker/scrape/`

### Scrape specific versions

```bash
# Only versions >= 28.0.0
dotnet run --project src/FrenchExDev.Net.Docker.Design -- --min-version 28.0.0

# Increase parallelism
dotnet run --project src/FrenchExDev.Net.Docker.Design -- --parallel 8

# Use docker as the container runtime (instead of podman)
dotnet run --project src/FrenchExDev.Net.Docker.Design -- --runtime docker
```

### Re-parse cached help text

If you've fixed a parser bug and want to regenerate JSON without re-scraping:

```bash
dotnet run --project src/FrenchExDev.Net.Docker.Design -- --reparse
```

This reads the cached help output from `scrape/help/` and re-parses with the current cobra parser.

### List available versions

```bash
dotnet run --project src/FrenchExDev.Net.Docker.Design -- --list
```

### Pre-cache images

```bash
dotnet run --project src/FrenchExDev.Net.Docker.Design -- --build-images --min-version 27.0.0
```

## Running Tests

### All tests

```bash
dotnet test test/FrenchExDev.Net.Docker.Tests
```

### With coverage

```bash
dotnet test test/FrenchExDev.Net.Docker.Tests \
    --settings coverage.runsettings \
    --collect:"XPlat Code Coverage"
```

Coverage excludes generated code (`[ExcludeFromCodeCoverage]`) and `obj/` directories.

### Quality gate

```bash
dotnet run --project ../QualityGate/src/FrenchExDev.Net.QualityGate.Cli -- test \
    --config quality-gate.yml
```

## Rebuilding Generated Code

After adding new scrape JSON files:

```bash
dotnet clean src/FrenchExDev.Net.Docker
dotnet build src/FrenchExDev.Net.Docker
```

The source generator runs automatically during build. Generated files appear in `obj/Generated/`.

If generated code is stale, verify the `.csproj` includes:
```xml
<AdditionalFiles Include="scrape\docker-*.json" />
```

## Troubleshooting

### CommandNotSupportedException

The bound version is outside the scraped range. Either:
- Scrape the target version: `dotnet run --project src/FrenchExDev.Net.Docker.Design -- --min-version X.Y.Z`
- Set `DetectedVersion = null` to disable version checking

### Generated code not updating after adding JSON files

1. Verify `<AdditionalFiles Include="scrape\docker-*.json" />` in `.csproj`
2. Run `dotnet clean && dotnet build`
3. Check that the JSON file is valid (no truncated content)

### GitHub API rate limiting during scraping

Set the `GITHUB_TOKEN` environment variable:
```bash
export GITHUB_TOKEN=ghp_...
dotnet run --project src/FrenchExDev.Net.Docker.Design
```

### Scrape container fails to start

- Ensure the container runtime is running (`podman info` or `docker info`)
- Try `--runtime docker` if podman has issues
- Check disk space -- each image is ~30MB

### Build errors in generated code

Usually caused by a parser bug producing invalid option names. Check the JSON file for the problematic version and either:
- Fix the parser and `--reparse`
- Remove the problematic JSON file
