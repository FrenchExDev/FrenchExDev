# DockerCompose.Bundle - Design CLI & Schema Management

## Overview

The `FrenchExDev.Net.DockerCompose.Bundle.Design` project is a console application that downloads compose-spec JSON schemas from GitHub. These schemas feed the source generator at compile time.

**Source**: `DockerCompose/src/FrenchExDev.Net.DockerCompose.Bundle.Design/Program.cs`

**Schema repository**: [compose-spec/compose-go](https://github.com/compose-spec/compose-go) (GitHub)

**Schema location in source tree**: `DockerCompose/src/FrenchExDev.Net.DockerCompose.Bundle/schemas/`

## Version Selection Strategy

The Design CLI collects all GitHub releases from `compose-spec/compose-go` using `GitHubReleasesVersionCollector`, then applies filtering:

1. **Pre-release exclusion** - handled by the collector
2. **Latest patch per minor** - for each `major.minor` group, only the highest patch version is kept

This produces **32 versions** (as of March 2026), from v1.0.9 through v2.10.1.

## CLI Commands

Run from the Design project directory:

```
cd DockerCompose/src/FrenchExDev.Net.DockerCompose.Bundle.Design
```

### List All Versions

```bash
dotnet run -- --list
```

Output:
```
Found 32 versions (latest patch per minor):
  v1.0.9
  v1.1.0
  ...
  v2.10.1
```

### List Missing Versions Only

Shows only versions that do not yet have a schema file on disk:

```bash
dotnet run -- --list --missing
```

If all schemas are already downloaded:
```
Found 0 versions (missing) (latest patch per minor):
```

### Download All Schemas

Downloads all 32 schemas (overwrites existing files):

```bash
dotnet run
```

Output:
```
Downloading 32 schemas to .../schemas...
  Downloaded v1.0.9
  Downloaded v1.1.0
  ...
Done: 32 downloaded, 0 skipped, 0 failed.
```

### Download Missing Schemas Only

Only downloads schemas that don't already exist on disk:

```bash
dotnet run -- --missing
```

Output (when all exist):
```
Downloading 32 schemas to .../schemas...
Done: 0 downloaded, 32 skipped, 0 failed.
```

## GitHub Rate Limiting

The CLI makes requests to `raw.githubusercontent.com`. For unauthenticated requests, GitHub allows 60 requests/hour. To avoid rate limits:

```bash
# Set a GitHub personal access token (no special scopes needed)
export GITHUB_TOKEN=ghp_xxxxxxxxxxxxxxxxxxxx
dotnet run -- --missing
```

The CLI uses a concurrency limit of 6 parallel downloads via `SemaphoreSlim`.

## Schema File Naming

Files follow the pattern:

```
compose-spec-v{major}.{minor}.{patch}.json
```

Examples:
- `compose-spec-v1.0.9.json`
- `compose-spec-v2.10.1.json`

The source generator discovers these via the `.csproj` glob:

```xml
<AdditionalFiles Include="schemas\compose-spec-*.json" />
<EmbeddedResource Include="schemas\compose-spec-*.json" />
```

- `AdditionalFiles` - makes them visible to the incremental source generator
- `EmbeddedResource` - embeds them in the assembly for runtime schema access

## Schema Download Source

Each schema is fetched from:

```
https://raw.githubusercontent.com/compose-spec/compose-go/v{version}/schema/compose-spec.json
```

The JSON schema follows the [JSON Schema](https://json-schema.org/) draft-07 specification and defines:
- Root properties (`services`, `networks`, `volumes`, `secrets`, `configs`, `name`, `include`, `models`)
- Definitions (`service`, `network`, `volume`, `deployment`, `healthcheck`, etc.)
- Type unions via `oneOf` (string-or-object, string-or-list, etc.)
- `$ref` references between definitions

## Adding a New Schema Version

When a new compose-go release is published:

```bash
# 1. Check what's missing
dotnet run -- --list --missing

# 2. Download only the new ones
dotnet run -- --missing

# 3. Rebuild the Bundle project to regenerate source
cd ../FrenchExDev.Net.DockerCompose.Bundle
dotnet build
```

The source generator will automatically pick up new `compose-spec-*.json` files, merge them into the unified schema, and regenerate all model and builder classes.

## Output Directory Resolution

The CLI resolves the output directory relative to its build output:

```csharp
var outputDir = Path.GetFullPath(Path.Combine(
    AppContext.BaseDirectory, "..", "..", "..", "..",
    "FrenchExDev.Net.DockerCompose.Bundle", "schemas"));
```

This navigates from `bin/Debug/net10.0/` back to the source tree. The path assumes the default `dotnet run` output structure.

## Troubleshooting

### All versions show as "missing" even though files exist

The `--list --missing` combination filters the version list by checking whether `compose-spec-v{version}.json` exists in the output directory. If the output directory path doesn't resolve correctly (e.g., running from a different working directory or with a non-standard output path), all files will appear missing.

Verify the output path:
```bash
dotnet run  # Without --list; the first line prints the resolved path
# "Downloading 32 schemas to C:\...\schemas..."
```

### HTTP 404 errors during download

Some compose-go versions may not have a `schema/compose-spec.json` file. The CLI reports these as failures but continues downloading the rest. Check the compose-go repository to verify the schema exists for that tag.

### Rate limit errors (HTTP 403)

Set the `GITHUB_TOKEN` environment variable:
```bash
export GITHUB_TOKEN=ghp_xxxxxxxxxxxxxxxxxxxx
```

### Generated code doesn't reflect new schemas

After downloading new schemas:
1. Ensure the `.json` files appear in `schemas/`
2. Rebuild: `dotnet build` (the source generator runs during compilation)
3. Check `obj/Generated/` for updated `.g.cs` files
4. If using an IDE, restart it or reload the solution to pick up generator changes
