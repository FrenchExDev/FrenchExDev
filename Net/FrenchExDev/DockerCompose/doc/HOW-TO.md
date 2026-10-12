# How-To Guide

## Using the Docker Compose wrapper

### Creating a client

The entry point is the static `DockerCompose.Create()` factory, which takes a `BinaryBinding`:

```csharp
var binding = new BinaryBinding
{
    Identifier = new BinaryIdentifier("docker-compose"),
    ExecutablePath = "/usr/local/bin/docker-compose",
    DetectedVersion = SemanticVersion.Parse("5.1.0")
};

var client = DockerCompose.Create(binding);
```

You can also resolve the binding dynamically via an `IBinaryResolver` and `IVersionDetector`.

### Building and executing commands

Every command uses a fluent builder pattern. Pass a configuration lambda to the client method:

```csharp
// Start services in detached mode with build
var upCmd = client.Up(b => b
    .WithDetach(true)
    .WithBuild(true)
    .WithScale(["web=3", "worker=2"])
    .WithTimeout(30)
);

// Stop and remove containers, networks, volumes
var downCmd = client.Down(b => b
    .WithRemoveOrphans(true)
    .WithVolumes(true)
    .WithTimeout(30)
);

// Build services
var buildCmd = client.Build(b => b
    .WithNoCache(true)
    .WithParallel(true)
    .WithPull(true)
);

// List running containers
var psCmd = client.Ps(b => b
    .WithFormat("json")
    .WithAll(true)
    .WithFilter(["status=running"])
);

// Follow service logs
var logsCmd = client.Logs(b => b
    .WithFollow(true)
    .WithTail("100")
    .WithTimestamps(true)
);

// Execute a command in a running service
var execCmd = client.Exec(b => b
    .WithDetach(false)
    .WithIndex(1)
);

// Run a one-off command
var runCmd = client.Run(b => b
    .WithRm(true)
    .WithDetach(false)
    .WithNoDeps(true)
);

// Watch for file changes and sync/rebuild
var watchCmd = client.Watch(b => b
    .WithNoUp(true)
);

// Validate and view the Compose file
var configCmd = client.Config(b => b
    .WithFormat("json")
    .WithResolveImageDigests(true)
);

// Pull service images
var pullCmd = client.Pull(b => b
    .WithQuiet(true)
    .WithIgnorePullFailures(true)
);

// Nested command group -- bridge transformations
var transformCmd = client.Bridge.Transformations.List(b => { });
var convertCmd = client.Bridge.Convert(b => { });
```

### Executing commands

Commands are data objects (`ICliCommand`). Execute them via `CommandExecutor`:

```csharp
var executor = new CommandExecutor(binaryResolver);

// Simple execution -- returns raw ProcessOutput
var output = await executor.ExecuteAsync(
    new BinaryIdentifier("docker-compose"), upCmd);

// Streaming execution -- IAsyncEnumerable<TEvent>
await foreach (var line in executor.StreamAsync(
    new BinaryIdentifier("docker-compose"), logsCmd, new MyComposeParser()))
{
    Console.WriteLine(line);
}
```

### Global options

Docker Compose supports global options that apply to all commands. These are set on the root command path:

| Option | Description |
|--------|-------------|
| `--file` / `-f` | Compose configuration files |
| `--project-name` / `-p` | Project name |
| `--project-directory` | Alternate working directory |
| `--profile` | Specify profiles to enable |
| `--parallel` | Control max parallelism |
| `--progress` | Set progress output type (auto, tty, plain, json, quiet) |
| `--ansi` | Control ANSI output (never, always, auto) |
| `--no-ansi` | Do not print ANSI control sequences |
| `--verbose` / `-v` | Show more output |
| `--dry-run` | Execute command in dry run mode |

### Inspecting command serialization

Every command exposes `CommandPath` and `ToArguments()` for debugging:

```csharp
var cmd = client.Up(b => b
    .WithDetach(true)
    .WithBuild(true)
    .WithScale(["web=3"])
);

Console.WriteLine(string.Join(" ", cmd.CommandPath));
// Output: up

Console.WriteLine(string.Join(" ", cmd.ToArguments()));
// Output: --detach --build --scale web=3
```

### Available commands

The `DockerComposeClient` exposes 35 top-level commands and 1 nested command group:

| Command | Since | Description |
|---------|-------|-------------|
| `Attach` | 2.24.0 | Attach local I/O streams to a service |
| `Build` | 2.20.0 | Build or rebuild services |
| `Commit` | 2.31.0 | Create a new image from a service container's changes |
| `Config` | 2.20.0 | Parse, resolve and render compose file |
| `Cp` | 2.20.0 | Copy files between service containers and host |
| `Create` | 2.20.0 | Create containers for a service |
| `Down` | 2.20.0 | Stop and remove containers, networks |
| `Events` | 2.20.0 | Receive real time events from containers |
| `Exec` | 2.20.0 | Execute a command in a running container |
| `Export` | 2.30.1 | Export a service container's filesystem as a tar archive |
| `Images` | 2.20.0 | List images used by the created containers |
| `Kill` | 2.20.0 | Force stop service containers |
| `Logs` | 2.20.0 | View output from containers |
| `Ls` | 2.20.0 | List running compose projects |
| `Pause` | 2.20.0 | Pause services |
| `Port` | 2.20.0 | Print the public port for a port binding |
| `Ps` | 2.20.0 | List containers |
| `Publish` | 2.34.0 | Publish compose application |
| `Pull` | 2.20.0 | Pull service images |
| `Push` | 2.20.0 | Push service images |
| `Restart` | 2.20.0 | Restart service containers |
| `Rm` | 2.20.0 | Remove stopped service containers |
| `Run` | 2.20.0 | Run a one-off command on a service |
| `Scale` | 2.22.0 | Scale services |
| `Start` | 2.20.0 | Start services |
| `Stats` | 2.24.0 | Display live resource usage statistics |
| `Stop` | 2.20.0 | Stop services |
| `Top` | 2.20.0 | Display the running processes |
| `Unpause` | 2.20.0 | Unpause services |
| `Up` | 2.20.0 | Create and start containers |
| `Version` | 2.20.0 | Show Docker Compose version |
| `Volumes` | 2.38.0 | Manage volumes |
| `Wait` | 2.20.0 | Block until containers stop |
| `Watch` | 2.22.0 | Watch build context and rebuild/refresh on changes |

Nested command group:

| Group | Commands | Since |
|-------|----------|-------|
| `Bridge.Convert` | Convert bridge transformations | 5.0.0 |
| `Bridge.Transformations.Create` | Create a transformation | 5.0.0 |
| `Bridge.Transformations.List` | List transformations | 5.0.0 |

---

## Scraping new Docker Compose versions

### Prerequisites

- **podman** (or docker) installed and running
- Network access to `github.com` (GitHub releases API + binary downloads)
- The Design project built: `dotnet build src/FrenchExDev.Net.DockerCompose.Design`

### Listing available versions

```bash
dotnet run --project src/FrenchExDev.Net.DockerCompose.Design -- --list
```

Filter to recent versions:

```bash
dotnet run --project src/FrenchExDev.Net.DockerCompose.Design -- --list --min-version 5.0.0
```

### Full scrape pipeline

The scraper runs in two pipelined phases using a `Channel<string>`:

**Phase 1 (producers)**: For each version, creates a temporary Alpine 3.19 container, downloads the `docker-compose-linux-x86_64` standalone binary from GitHub releases, installs it to `/usr/local/bin/docker-compose`, and commits the container as a reusable image (`docker-compose-scrape:{version}`). Ready versions are published to the channel immediately.

**Phase 2 (consumers)**: Reads from the channel as versions become available, starts containers from the built images, runs `docker-compose <cmd> --help` recursively for every command via `CobraHelpParser`, writes JSON, and immediately deletes the image after scrape completes (eager cleanup).

Run both phases:

```bash
dotnet run --project src/FrenchExDev.Net.DockerCompose.Design -- \
    --min-version 2.20.0 \
    --parallel 4
```

Build images only (useful for pre-caching):

```bash
dotnet run --project src/FrenchExDev.Net.DockerCompose.Design -- \
    --build-images \
    --min-version 2.20.0
```

### CLI options

| Flag | Default | Description |
|------|---------|-------------|
| `--parallel N` | `4` | Number of concurrent build/scrape workers |
| `--output DIR` | `../FrenchExDev.Net.DockerCompose/scrape` | Output directory for JSON files |
| `--min-version VER` | `2.20.0` | Only process versions >= VER |
| `--runtime BIN` | `podman` | Container runtime binary (`podman` or `docker`) |
| `--list` | off | List versions and exit |
| `--build-images` | off | Build images and exit (skip scraping) |

### Using docker instead of podman

```bash
dotnet run --project src/FrenchExDev.Net.DockerCompose.Design -- \
    --runtime docker \
    --min-version 2.20.0
```

### JSON output format

Each version produces a file like `docker-compose-5.1.0.json`:

```json
{
  "binaryName": "docker-compose",
  "root": {
    "name": "docker-compose",
    "description": "Define and run multi-container applications with Docker",
    "options": [
      {
        "longName": "file",
        "shortName": "f",
        "description": "Compose configuration files",
        "valueKind": "multiple",
        "clrType": "string",
        "isRequired": false
      },
      {
        "longName": "project-name",
        "shortName": "p",
        "description": "Project name",
        "valueKind": "single",
        "clrType": "string",
        "isRequired": false
      }
    ],
    "subCommands": [
      {
        "name": "up",
        "description": "Create and start containers",
        "options": [
          {
            "longName": "detach",
            "shortName": "d",
            "valueKind": "flag",
            "clrType": "bool"
          },
          {
            "longName": "build",
            "valueKind": "flag",
            "clrType": "bool"
          },
          {
            "longName": "scale",
            "valueKind": "multiple",
            "clrType": "string"
          }
        ],
        "subCommands": []
      }
    ]
  }
}
```

After scraping, rebuild the main library to regenerate the C# code:

```bash
dotnet build src/FrenchExDev.Net.DockerCompose
```

### Cleanup

The scraper performs eager cleanup -- each version's image is deleted immediately after its scrape completes. This keeps disk usage proportional to `--parallel` count (typically 4 images at once), not total version count (57+ images).

If the scraper is interrupted, a `finally` block cleans up straggler containers and images. For manual cleanup:

```bash
podman rm -f $(podman ps -a --filter "ancestor=docker-compose-scrape" -q) 2>/dev/null
podman rmi -f $(podman images --filter "reference=docker-compose-scrape" -q) 2>/dev/null
```

---

## Running tests

### Full test suite

```bash
dotnet test test/FrenchExDev.Net.DockerCompose.Tests
```

### With code coverage

```bash
dotnet test test/FrenchExDev.Net.DockerCompose.Tests \
    --settings test/FrenchExDev.Net.DockerCompose.Tests/coverage.runsettings \
    --collect:"XPlat Code Coverage" \
    --results-directory test/coverage
```

The coverage configuration excludes source-generated code (`obj/Generated/**`) and measures only the hand-written Docker Compose library code.

---

## Extending the wrapper

### Adding output parsing

Docker Compose does not yet have a custom output parser or event hierarchy. To add one:

1. Create `DockerComposeEvents.cs` with a record hierarchy:

```csharp
public abstract record DockerComposeEvent;
public sealed record DockerComposeServiceOutput(string Service, string Message) : DockerComposeEvent;
public sealed record DockerComposeServiceError(string Service, string Message) : DockerComposeEvent;
public sealed record DockerComposeOutputLine(string Text, OutputSource Source) : DockerComposeEvent;
```

2. Implement `IOutputParser<DockerComposeEvent>`:

```csharp
public sealed class DockerComposeOutputParser : IOutputParser<DockerComposeEvent>
{
    public IEnumerable<DockerComposeEvent> ParseLine(OutputLine line)
    {
        yield return new DockerComposeOutputLine(line.Text, line.Source);
    }

    public IEnumerable<DockerComposeEvent> Complete(int exitCode)
    {
        if (exitCode != 0)
            yield return new DockerComposeServiceError("docker-compose", $"Exit code: {exitCode}");
    }
}
```

3. Implement `IResultCollector<DockerComposeEvent, TResult>` for result aggregation.

### Supporting a new Docker Compose version

1. Run the scraper with `--min-version` set to the new version
2. The new JSON file appears in `scrape/`
3. Rebuild -- the source generator automatically picks up the new file and adjusts `[SinceVersion]` / `[UntilVersion]` attributes
4. Run tests to verify nothing broke

---

## Troubleshooting

### Binary download failures

Docker Compose V2 publishes standalone binaries (not tarballs). If a download fails with a 404 or 502, verify the release exists:

```bash
curl -fsSL -o /dev/null -w "%{http_code}" \
    https://github.com/docker/compose/releases/download/v{version}/docker-compose-linux-x86_64
```

Transient GitHub 502 errors can be retried by re-running the scraper -- it skips versions whose images already exist.

### "Command not supported" at runtime

A `CommandNotSupportedException` means the `BinaryBinding.DetectedVersion` is outside the scraped version range. Either:
- Update the binding's `DetectedVersion` to match the actual binary
- Scrape the target version to extend coverage

### Generated code not updating

Ensure scrape JSON files are registered as `AdditionalFiles` in the `.csproj`:

```xml
<AdditionalFiles Include="scrape\docker-compose-*.json" />
```

Clean and rebuild:

```bash
dotnet clean src/FrenchExDev.Net.DockerCompose
dotnet build src/FrenchExDev.Net.DockerCompose
```

Generated files are emitted to `obj/Generated/` when `EmitCompilerGeneratedFiles` is enabled.

### GitHub API rate limiting

The version collector uses the GitHub releases API, which has a 60 requests/hour limit for unauthenticated requests. Set the `GITHUB_TOKEN` environment variable to increase this limit:

```bash
export GITHUB_TOKEN=ghp_your_token_here
dotnet run --project src/FrenchExDev.Net.DockerCompose.Design -- --list
```
