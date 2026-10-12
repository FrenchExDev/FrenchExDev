# How-To Guide

## Using the GitLab CLI wrapper

### Creating a client

The entry point is the static `Glab.Create()` factory, which takes a `BinaryBinding`:

```csharp
var binding = new BinaryBinding
{
    Identifier = new BinaryIdentifier("glab"),
    ExecutablePath = "/usr/bin/glab",
    DetectedVersion = SemanticVersion.Parse("1.53.0")
};

var client = Glab.Create(binding);
```

You can also resolve the binding dynamically via an `IBinaryResolver` and `IVersionDetector`.

### Building and executing commands

Every command uses a fluent builder pattern. Pass a configuration lambda to the client method:

```csharp
// Simple command -- authenticate with GitLab
var authCmd = client.Auth.Login(b => b
    .WithHostname("gitlab.com")
    .WithToken(token)
);

// Command with options -- list pull requests
var prListCmd = client.Pr.List(b => b
    .WithAll(true)
    .WithFormat("json")
    .WithFilter(["status=opened"])
);

// Nested command group -- view a specific issue
var issueViewCmd = client.Issue.View(b => b
    .WithIid(123)
);

// Simple info command
var versionCmd = client.Version(b => { });
```

### Executing commands

Commands are data objects (`ICliCommand`). Execute them via `CommandExecutor`:

```csharp
var executor = new CommandExecutor(binaryResolver);

// Simple execution -- returns raw ProcessOutput
var output = await executor.ExecuteAsync(
    new BinaryIdentifier("glab"), authCmd);

// Streaming execution -- IAsyncEnumerable<TEvent>
await foreach (var line in executor.StreamAsync(
    new BinaryIdentifier("glab"), prListCmd, new MyGlabParser()))
{
    Console.WriteLine(line);
}
```

### Inspecting command serialization

Every command exposes `CommandPath` and `ToArguments()` for debugging:

```csharp
var cmd = client.Pr.List(b => b
    .WithAll(true)
    .WithFormat("json")
);

Console.WriteLine(string.Join(" ", cmd.CommandPath));
// Output: pr list

Console.WriteLine(string.Join(" ", cmd.ToArguments()));
// Output: --all --format json
```

### Available command groups

The `GlabClient` exposes nested command groups (exact groups vary by version):

| Group | Example commands |
|-------|-----------------|
| `Auth` | `Login`, `Logout`, `RefreshToken`, `Status` |
| `Pr` | `Create`, `List`, `View`, `Close`, `Merge`, `Approve`, ... |
| `Issue` | `Create`, `List`, `View`, `Close`, `Reopen`, `Comment`, ... |
| `Repo` | `Create`, `Clone`, `Fork`, `View`, `DeployKey`, `Delete`, ... |
| `Snippet` | `Create`, `List`, `View`, `Delete`, `Comment`, ... |
| `Release` | `Create`, `List`, `View`, `Delete`, `Update`, `Download`, ... |
| `Variable` | `List`, `Set`, `Delete`, `Get` |
| `Label` | `Create`, `List`, `View`, `Delete` |
| `User` | `Get`, `List` |

Top-level commands (shortcuts) are also available directly on `GlabClient` depending on the version.

---

## Scraping glab versions

### Prerequisites

- **podman** or **docker** installed and running
- Network access to `gitlab.com` (GitLab API + binary downloads)
- Optional: `GITLAB_TOKEN` env var set for higher rate limits
- The Design project built: `dotnet build src/FrenchExDev.Net.GitLab.Cli.Design`

### Listing available versions

```bash
dotnet run --project src/FrenchExDev.Net.GitLab.Cli.Design -- --list
```

Filter to recent versions:

```bash
dotnet run --project src/FrenchExDev.Net.GitLab.Cli.Design -- --list --min-version 1.50.0
```

### Full scrape pipeline

The scraper downloads glab binaries from GitLab releases, runs each command's `--help`, parses the output via `GlabHelpParser`, and writes JSON to `scrape/`:

```bash
dotnet run --project src/FrenchExDev.Net.GitLab.Cli.Design -- \
    --min-version 1.47.0 \
    --parallel 4
```

### CLI options

| Flag | Default | Description |
|------|---------|-------------|
| `--parallel N` | `2` | Number of concurrent scrape workers |
| `--output DIR` | `../FrenchExDev.Net.GitLab.Cli/scrape` | Output directory for JSON files |
| `--min-version VER` | `1.47.0` | Only process versions >= VER |
| `--runtime BIN` | `podman` | Container runtime binary |
| `--list` | off | List versions and exit |

### Using docker instead of podman

```bash
dotnet run --project src/FrenchExDev.Net.GitLab.Cli.Design -- \
    --runtime docker \
    --min-version 1.50.0
```

### Setting a GitLab token

Set the `GITLAB_TOKEN` environment variable to increase API rate limits:

```bash
export GITLAB_TOKEN=glpat_your_token_here
dotnet run --project src/FrenchExDev.Net.GitLab.Cli.Design -- \
    --min-version 1.47.0
```

This allows fetching version information if you're working with a private GitLab instance or want faster API responses. The token is passed as the `PRIVATE-TOKEN` header.

### JSON output format

Each version produces a file like `glab-1.53.0.json`:

```json
{
  "binaryName": "glab",
  "root": {
    "name": "glab",
    "description": "GitLab's official CLI tool",
    "options": [
      {
        "longName": "help",
        "shortName": "h",
        "description": "Show help for command",
        "valueKind": "flag",
        "clrType": "bool"
      }
    ],
    "subCommands": [
      {
        "name": "auth",
        "description": "Handle authentication and configuration",
        "subCommands": [
          {
            "name": "login",
            "description": "Authenticate with a GitLab instance",
            "options": [
              {
                "longName": "hostname",
                "shortName": null,
                "description": "The URL of the GitLab instance",
                "valueKind": "single",
                "clrType": "string"
              },
              {
                "longName": "token",
                "shortName": "t",
                "description": "Provide a personal access token",
                "valueKind": "single",
                "clrType": "string"
              }
            ]
          }
        ]
      },
      {
        "name": "pr",
        "description": "Work with pull requests",
        "subCommands": [
          {
            "name": "list",
            "description": "List pull requests",
            "options": [...]
          }
        ]
      }
    ]
  }
}
```

After scraping, rebuild the main library to regenerate the C# code:

```bash
dotnet build src/FrenchExDev.Net.GitLab.Cli
```

### Cleanup

The scraper automatically cleans up after itself in a `finally` block:
1. Removes all containers started during scraping
2. Removes all scraping images

If the scraper is interrupted, you may need to clean up manually:

```bash
podman rm -f $(podman ps -a -q --filter "ancestor=glab-scrape") 2>/dev/null
podman rmi -f $(podman images -q --filter "reference=glab-scrape") 2>/dev/null
```

Or with docker:

```bash
docker rm -f $(docker ps -a -q --filter "ancestor=glab-scrape") 2>/dev/null
docker rmi -f $(docker images -q --filter "reference=glab-scrape") 2>/dev/null
```

---

## Running tests

### Full test suite

If tests exist for the GitLab.Cli project:

```bash
dotnet test src/FrenchExDev.Net.GitLab.Cli.Tests
```

### With code coverage

```bash
dotnet test src/FrenchExDev.Net.GitLab.Cli.Tests \
    --settings src/FrenchExDev.Net.GitLab.Cli.Tests/coverage.runsettings \
    --collect:"XPlat Code Coverage" \
    --results-directory coverage
```

### Test categories

The test suite typically includes:

**Command serialization tests** -- Verify that each command type serializes its options correctly:
- Boolean flags serialize as `--flag` (presence = true)
- String options serialize as `--flag value` (two args)
- List options serialize each item as a separate `--flag value` pair
- Null/unset options are excluded from output

**Generated API shape tests** -- Verify the full client API surface:
- Static entry point `Glab.Create()` returns a `GlabClient`
- Top-level commands and command groups via client
- Builder fluent chains return the correct builder type
- Empty builders produce zero arguments

**Descriptor tests** -- Verify the `GlabDescriptor` class exists and is instantiable

---

## Extending the wrapper

### Adding output parsing

GitLab CLI does not yet have a custom output parser or event hierarchy. To add one:

1. Create `GlabEvents.cs` with a record hierarchy:

```csharp
public abstract record GlabEvent;
public sealed record GlabAuthSuccess(string Message) : GlabEvent;
public sealed record GlabError(string Message) : GlabEvent;
public sealed record GlabOutputLine(string Text, OutputSource Source) : GlabEvent;
```

2. Implement `IOutputParser<GlabEvent>`:

```csharp
public sealed class GlabOutputParser : IOutputParser<GlabEvent>
{
    public IEnumerable<GlabEvent> ParseLine(OutputLine line)
    {
        if (line.Text.Contains("Authentication successful", StringComparison.OrdinalIgnoreCase))
            yield return new GlabAuthSuccess(line.Text);
        else
            yield return new GlabOutputLine(line.Text, line.Source);
    }

    public IEnumerable<GlabEvent> Complete(int exitCode)
    {
        if (exitCode != 0)
            yield return new GlabError($"Exit code: {exitCode}");
    }
}
```

3. Implement `IResultCollector<GlabEvent, TResult>` for result aggregation.

### Supporting a new glab version

1. Run the scraper with `--min-version` set to the new version
2. The new JSON file appears in `scrape/`
3. Rebuild -- the source generator automatically picks up the new file and adjusts `[SinceVersion]` / `[UntilVersion]` attributes
4. Run tests to verify nothing broke

### Customizing the help parser

The `GlabHelpParser` can be customized or replaced by implementing `IHelpParser`:

```csharp
public interface IHelpParser
{
    CommandNode? Parse(string helpText, string commandName);
}
```

To use a custom parser, modify `Program.cs` in the Design project:

```csharp
Func<string, ILogger, IHelpParser> parser = (_, _) => new MyCustomGlabParser();
```

---

## Troubleshooting

### "No versions found" from GitLab API

Check your internet connection and GitLab API access:

```bash
curl -s https://gitlab.com/api/v4/projects/gitlab-org%2Fcli/releases | head -20
```

If you get a 401, set `GITLAB_TOKEN`:

```bash
export GITLAB_TOKEN=glpat_your_token_here
```

### Asset download fails

Verify the URL pattern for your glab version. Glab releases binaries at:

```
https://gitlab.com/gitlab-org/cli/-/releases/v{version}/downloads/glab_{version}_linux_amd64.tar.gz
```

If a version doesn't have this asset, you may need to adjust the scraper or skip that version.

### "Command not supported" at runtime

A `CommandNotSupportedException` means the `BinaryBinding.DetectedVersion` is outside the scraped version range. Either:
- Update the binding's `DetectedVersion` to match the actual binary
- Scrape the target version to extend coverage

### Generated code not updating

Ensure scrape JSON files are registered as `AdditionalFiles` in the `.csproj`:

```xml
<AdditionalFiles Include="scrape\glab-*.json" />
```

Clean and rebuild:

```bash
dotnet clean src/FrenchExDev.Net.GitLab.Cli
dotnet build src/FrenchExDev.Net.GitLab.Cli
```

Generated files are emitted to `obj/Generated/` when `EmitCompilerGeneratedFiles` is enabled.

### Help parser unable to parse new version output

If glab's help output changes significantly, `GlabHelpParser` may fail to parse it correctly. This typically manifests as missing commands or options.

Debug by checking the scraped JSON:

```bash
cat scrape/glab-{version}.json | python -m json.tool | head -50
```

If whole sections are missing, update the parser to handle the new format, or file an issue with your findings.

### GitLab API rate limiting

The version collector uses the GitLab API, which has rate limits:
- **Unauthenticated**: 300 requests/hour
- **Authenticated**: Up to 600 requests/hour with `GITLAB_TOKEN`

Set the `GITLAB_TOKEN` environment variable to authenticate:

```bash
export GITLAB_TOKEN=glpat_your_token_here
dotnet run --project src/FrenchExDev.Net.GitLab.Cli.Design -- --list
```

### Container cleanup failed

If containers or images remain after a scrape failure, clean them manually:

```bash
# With podman
podman ps -a --filter "ancestor=glab-scrape" -q | xargs podman rm -f
podman images --filter "reference=glab-scrape" -q | xargs podman rmi -f

# With docker
docker ps -a --filter "ancestor=glab-scrape" -q | xargs docker rm -f
docker images --filter "reference=glab-scrape" -q | xargs docker rmi -f
```
