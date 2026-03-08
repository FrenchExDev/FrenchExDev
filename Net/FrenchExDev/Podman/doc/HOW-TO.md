# How-To Guide

## Using the Podman wrapper

### Creating a client

The entry point is the static `Podman.Create()` factory, which takes a `BinaryBinding`:

```csharp
var binding = new BinaryBinding
{
    Identifier = new BinaryIdentifier("podman"),
    ExecutablePath = "/usr/bin/podman",
    DetectedVersion = SemanticVersion.Parse("5.8.0")
};

var client = Podman.Create(binding);
```

You can also resolve the binding dynamically via an `IBinaryResolver` and `IVersionDetector`.

### Building and executing commands

Every command uses a fluent builder pattern. Pass a configuration lambda to the client method:

```csharp
// Simple command -- run a container
var runCmd = client.Run(b => b
    .WithDetach(true)
    .WithName("my-app")
    .WithRm(true)
    .WithPublish(["8080:80"])
    .WithVolume(["/data:/app/data"])
    .WithEnv(["NODE_ENV=production"])
);

// Command with options -- list containers
var psCmd = client.Ps(b => b
    .WithAll(true)
    .WithFormat("json")
    .WithFilter(["status=running"])
);

// Nested command group -- list images
var imageListCmd = client.Image.List(b => b
    .WithAll(true)
    .WithFormat("{{.Repository}}:{{.Tag}}")
);

// Deep nesting -- system connection
var connListCmd = client.System.Connection.List(b => { });

// Other groups
var networkLsCmd = client.Network.Ls(b => b.WithQuiet(true));
var volumeLsCmd = client.Volume.Ls(b => b.WithFormat("json"));
var podLsCmd = client.Pod.Ps(b => b.WithFormat("json"));
var secretLsCmd = client.Secret.Ls(b => b.WithFormat("json"));
```

### Executing commands

Commands are data objects (`ICliCommand`). Execute them via `CommandExecutor`:

```csharp
var executor = new CommandExecutor(binaryResolver);

// Simple execution -- returns raw ProcessOutput
var output = await executor.ExecuteAsync(
    new BinaryIdentifier("podman"), runCmd);

// Streaming execution -- IAsyncEnumerable<TEvent>
await foreach (var line in executor.StreamAsync(
    new BinaryIdentifier("podman"), psCmd, new MyPodmanParser()))
{
    Console.WriteLine(line);
}
```

### Inspecting command serialization

Every command exposes `CommandPath` and `ToArguments()` for debugging:

```csharp
var cmd = client.Run(b => b
    .WithDetach(true)
    .WithName("test")
    .WithEnv(["FOO=bar", "BAZ=qux"])
);

Console.WriteLine(string.Join(" ", cmd.CommandPath));
// Output: run

Console.WriteLine(string.Join(" ", cmd.ToArguments()));
// Output: --detach --name test --env FOO=bar --env BAZ=qux
```

### Available command groups

The `PodmanClient` exposes 18 nested command groups:

| Group | Example commands |
|-------|-----------------|
| `Container` | `List`, `Inspect`, `Logs`, `Start`, `Stop`, `Rm`, ... |
| `Image` | `Build`, `List`, `Pull`, `Push`, `Rm`, `Inspect`, ... |
| `Network` | `Create`, `Ls`, `Inspect`, `Connect`, `Disconnect`, ... |
| `Volume` | `Create`, `Ls`, `Inspect`, `Prune`, `Rm` |
| `Pod` | `Create`, `Ps`, `Start`, `Stop`, `Rm`, `Inspect`, ... |
| `System` | `Df`, `Info`, `Prune`, `Connection/` (sub-group) |
| `Machine` | `Init`, `Start`, `Stop`, `Rm`, `List`, `Os/` (sub-group) |
| `Secret` | `Create`, `Ls`, `Inspect`, `Rm` |
| `Manifest` | `Create`, `Add`, `Push`, `Inspect`, `Rm` |
| `Healthcheck` | `Run` |
| `Generate` | `Kube`, `Spec`, `Systemd` |
| `Play` | `Kube` |
| `Kube` | `Apply`, `Down`, `Generate`, `Play` |
| `Farm` | `Build`, `Create`, `List`, `Remove`, `Update` |
| `Artifact` | `Add`, `Extract`, `Inspect`, `Ls`, `Pull`, `Push`, `Rm` |
| `Quadlet` | `Info` |

Top-level commands (shortcuts) are also available directly on `PodmanClient`: `Run`, `Ps`, `Build`, `Exec`, `Pull`, `Push`, `Images`, `Rm`, `Rmi`, `Start`, `Stop`, `Kill`, `Logs`, `Inspect`, `Info`, etc.

---

## Scraping new Podman versions

### Prerequisites

- **podman** (or docker) installed and running
- Network access to `github.com` (GitHub releases API + binary downloads)
- The Design project built: `dotnet build src/FrenchExDev.Net.Podman.Design`

### Listing available versions

```bash
dotnet run --project src/FrenchExDev.Net.Podman.Design -- --list
```

Filter to recent versions:

```bash
dotnet run --project src/FrenchExDev.Net.Podman.Design -- --list --min-version 5.0.0
```

### Full scrape pipeline

The scraper runs in two phases:

**Phase 1: Build images** -- For each version, creates a temporary Alpine 3.19 container, downloads the `podman-remote-static` binary from GitHub releases, installs it to `/usr/local/bin/podman`, and commits the container as a reusable image (`podman-scrape:{version}`).

**Phase 2: Scrape** -- Starts containers from pre-built images, runs `podman <cmd> --help` recursively for every command, parses the output via `PodmanHelpParser`, and writes JSON to `scrape/`.

Run both phases:

```bash
dotnet run --project src/FrenchExDev.Net.Podman.Design -- \
    --min-version 4.1.0 \
    --parallel 4
```

Build images only (useful for pre-caching):

```bash
dotnet run --project src/FrenchExDev.Net.Podman.Design -- \
    --build-images \
    --min-version 4.1.0
```

### CLI options

| Flag | Default | Description |
|------|---------|-------------|
| `--parallel N` | `4` | Number of concurrent scrape workers |
| `--output DIR` | `../FrenchExDev.Net.Podman/scrape` | Output directory for JSON files |
| `--min-version VER` | `4.1.0` | Only process versions >= VER |
| `--runtime BIN` | `podman` | Container runtime binary |
| `--list` | off | List versions and exit |
| `--build-images` | off | Build images and exit (skip scraping) |

### Using docker instead of podman

```bash
dotnet run --project src/FrenchExDev.Net.Podman.Design -- \
    --runtime docker \
    --min-version 4.1.0
```

### JSON output format

Each version produces a file like `podman-5.8.0.json`:

```json
{
  "binaryName": "podman",
  "root": {
    "name": "podman",
    "description": "Manage pods, containers and images",
    "options": [
      {
        "longName": "config",
        "shortName": null,
        "description": "Location of config file",
        "valueKind": "single",
        "clrType": "string",
        "isRequired": false
      },
      {
        "longName": "connection",
        "shortName": "c",
        "description": "Connection to use for remote Podman service",
        "valueKind": "single",
        "clrType": "string",
        "isRequired": false
      }
    ],
    "subCommands": [
      {
        "name": "run",
        "description": "Run a command in a new container",
        "options": [
          {
            "longName": "detach",
            "shortName": "d",
            "valueKind": "flag",
            "clrType": "bool"
          },
          {
            "longName": "name",
            "valueKind": "single",
            "clrType": "string"
          },
          {
            "longName": "volume",
            "shortName": "v",
            "valueKind": "multiple",
            "clrType": "string"
          }
        ],
        "subCommands": []
      },
      {
        "name": "container",
        "subCommands": [
          { "name": "list", "options": [...] },
          { "name": "inspect", "options": [...] }
        ]
      }
    ]
  }
}
```

After scraping, rebuild the main library to regenerate the C# code:

```bash
dotnet build src/FrenchExDev.Net.Podman
```

### Cleanup

The scraper automatically cleans up after itself in a `finally` block:
1. Removes all containers started during Phase 2
2. Removes all `podman-scrape:*` images built during Phase 1

If the scraper is interrupted, you may need to clean up manually:

```bash
podman rm -f $(podman ps -a --filter "ancestor=podman-scrape" -q) 2>/dev/null
podman rmi -f $(podman images --filter "reference=podman-scrape" -q) 2>/dev/null
```

---

## Running tests

### Full test suite

```bash
dotnet test test/FrenchExDev.Net.Podman.Tests
```

### With code coverage

```bash
dotnet test test/FrenchExDev.Net.Podman.Tests \
    --settings test/FrenchExDev.Net.Podman.Tests/coverage.runsettings \
    --collect:"XPlat Code Coverage" \
    --results-directory test/coverage
```

The coverage configuration excludes source-generated code (`obj/Generated/**`) and measures only the hand-written Podman library code.

### Test categories

The test suite includes:

**Command serialization tests** -- Verify that each command type serializes its options correctly:
- Boolean flags serialize as `--flag` (presence = true)
- String options serialize as `--flag value` (two args)
- List options serialize each item as a separate `--flag value` pair
- Null/unset options are excluded from output

**Generated API shape tests** -- Verify the full client API surface:
- Static entry point `Podman.Create()` returns a `PodmanClient`
- Top-level commands (`Run`, `Ps`, `Build`, `Exec`, etc.) via client
- Sub-group navigation (`Container`, `Image`, `Network`, `Volume`, etc.)
- Builder fluent chains return the correct builder type
- Empty builders produce zero arguments

**Descriptor tests** -- Verify the `PodmanDescriptor` class exists and is instantiable

---

## Extending the wrapper

### Adding output parsing

Podman does not yet have a custom output parser or event hierarchy (unlike Packer and Vagrant). To add one:

1. Create `PodmanEvents.cs` with a record hierarchy:

```csharp
public abstract record PodmanEvent;
public sealed record PodmanContainerOutput(string ContainerId, string Message) : PodmanEvent;
public sealed record PodmanContainerError(string ContainerId, string Message) : PodmanEvent;
public sealed record PodmanOutputLine(string Text, OutputSource Source) : PodmanEvent;
```

2. Implement `IOutputParser<PodmanEvent>`:

```csharp
public sealed class PodmanOutputParser : IOutputParser<PodmanEvent>
{
    public IEnumerable<PodmanEvent> ParseLine(OutputLine line)
    {
        yield return new PodmanOutputLine(line.Text, line.Source);
    }

    public IEnumerable<PodmanEvent> Complete(int exitCode)
    {
        if (exitCode != 0)
            yield return new PodmanContainerError("podman", $"Exit code: {exitCode}");
    }
}
```

3. Implement `IResultCollector<PodmanEvent, TResult>` for result aggregation.

### Supporting a new Podman version

1. Run the scraper with `--min-version` set to the new version
2. The new JSON file appears in `scrape/`
3. Rebuild -- the source generator automatically picks up the new file and adjusts `[SinceVersion]` / `[UntilVersion]` attributes
4. Run tests to verify nothing broke

---

## Troubleshooting

### Broken versions (4.1.0, 4.3.0)

These versions publish `podman-remote-static*.tar.gz` on GitHub, but the binaries are not truly static and fail on Alpine. They are excluded from scraping. The `VersionDiffer` fills in the gaps from adjacent versions (4.1.1, 4.3.1).

### Asset naming varies by version

Pre-4.4.0 releases use `podman-remote-static.tar.gz`, while 4.4.0+ uses `podman-remote-static-linux_amd64.tar.gz`. The scraper handles this automatically. If adding support for ARM or other architectures, update the `AssetName()` method in `Program.cs`.

### "Command not supported" at runtime

A `CommandNotSupportedException` means the `BinaryBinding.DetectedVersion` is outside the scraped version range. Either:
- Update the binding's `DetectedVersion` to match the actual binary
- Scrape the target version to extend coverage

### Generated code not updating

Ensure scrape JSON files are registered as `AdditionalFiles` in the `.csproj`:

```xml
<AdditionalFiles Include="scrape\podman-*.json" />
```

Clean and rebuild:

```bash
dotnet clean src/FrenchExDev.Net.Podman
dotnet build src/FrenchExDev.Net.Podman
```

Generated files are emitted to `obj/Generated/` when `EmitCompilerGeneratedFiles` is enabled.

### Tar ownership errors during scraping

If you see `Cannot change ownership to uid ...` during scraping, this is a rootless podman issue. The scraper already uses `--no-same-owner` in the tar command. If the error persists, ensure podman is running in rootless mode or use `--runtime docker` as an alternative.

### GitHub API rate limiting

The version collector uses the GitHub releases API, which has a 60 requests/hour limit for unauthenticated requests. Set the `GITHUB_TOKEN` environment variable to increase this limit:

```bash
export GITHUB_TOKEN=ghp_your_token_here
dotnet run --project src/FrenchExDev.Net.Podman.Design -- --list
```
