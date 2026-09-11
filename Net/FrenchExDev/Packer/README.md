# FrenchExDev.Net.Packer

Typed C# wrapper for [HashiCorp Packer](https://www.packer.io/) built on the BinaryWrapper framework.

## Quick Start

```csharp
var client = Packer.Create(new BinaryBinding
{
    Identifier = new BinaryIdentifier("packer"),
    ExecutablePath = "/usr/local/bin/packer"
});

// Build with typed fluent API
var buildCmd = client.Build(b => b
    .WithTemplate("aws.pkr.hcl")
    .WithForce(true)
    .WithVar(["region=us-east-1"]));

// Validate
var validateCmd = client.Validate(b => b
    .WithTemplate("aws.pkr.hcl")
    .WithSyntaxOnly(true));

// Nested plugin commands
var installCmd = client.Plugins.Install(b => b
    .WithPlugin("github.com/hashicorp/docker")
    .WithForce(true));
```

## Commands

| Command | Method | Options |
|---|---|---|
| `build` | `client.Build(...)` | force, color, debug, except, only, on-error, parallel-builds, timestamp-ui, var, var-file, machine-readable |
| `validate` | `client.Validate(...)` | syntax-only, except, only, var, var-file, no-warn-undeclared-var |
| `init` | `client.Init(...)` | upgrade, force |
| `fmt` | `client.Fmt(...)` | check, diff, recursive, write |
| `inspect` | `client.Inspect(...)` | (none) |
| `hcl2-upgrade` | `client.Hcl2Upgrade(...)` | output-file, with-annotations |
| `console` | `client.Console(...)` | var, var-file |
| `plugins install` | `client.Plugins.Install(...)` | force |
| `plugins remove` | `client.Plugins.Remove(...)` | (none) |
| `plugins installed` | `client.Plugins.Installed(...)` | (none) |
| `plugins required` | `client.Plugins.Required(...)` | (none) |

## Output Parsing

```csharp
// Parse standard build output
var parser = new PackerBuildParser();

// Parse machine-readable format (-machine-readable flag)
var mrParser = new PackerMachineReadableParser();

// Collect build results
var collector = new PackerBuildCollector();
```

## Architecture

See [doc/ARCHITECTURE.md](doc/ARCHITECTURE.md).

## Shared dependency and version images

The Design runner prepares the shared system dependencies once, then installs each
software version in an image derived from that base. `UseVersionImage().UseContainer()`
builds or reuses the image before collecting help. After collection, the container
and version image are removed; `--keep-images` retains the version image. The
shared base remains cached.

From the wrapper directory, with Podman running (or add `--runtime docker`):

```powershell
$design = './src/FrenchExDev.Net.Packer.Design/FrenchExDev.Net.Packer.Design.csproj'
dotnet run --project $design --framework net10.0 -- --help
dotnet run --project $design --framework net10.0 -- --build-base
dotnet run --project $design --framework net10.0 -- --list --missing
# Review the selection; optionally narrow it with --min-version.
dotnet run --project $design --framework net10.0 -- --build-images --missing --parallel 2
dotnet run --project $design --framework net10.0 -- --missing --parallel 2
dotnet run --project $design --framework net10.0 -- --reparse
dotnet run --project $design --framework net10.0 -- --clean-images
```

`--build-base` prepares only dependencies. `--build-images` installs selected versions
without scraping and keeps their images. `--clean-images` removes this wrapper's
version images before its base, without forcing removal. Base preparation and cleanup
do not query the version collector; `--list` and `--reparse` do not build images.
With `--missing`, selection is based on missing JSON files.

The three image operations are mutually exclusive and cannot be combined with
`--reparse` or known-missing management. `--build-base` and `--clean-images` also
reject `--list` and `--missing`.

The PowerShell launcher exposes `-BuildBase`, `-BuildImages`, `-CleanImages`,
`-KeepImages`, `-Reparse`, `-Missing`, `-List`, `-MinVersion`, `-Parallel`,
`-ScrapeParallel`, `-Runtime`, `-Output` and `-Framework`. It resolves its project
relative to the script, so it also works from another directory:

```powershell
./scripts/Find-Missing.ps1 -BuildBase -Framework net10.0
./scripts/Find-Missing.ps1 -BuildImages -Missing -Parallel 2 -Framework net10.0
```

See the [BinaryWrapper image pipeline guide](../BinaryWrapper/doc/UPGRADE-IMAGE-PIPELINES.md)
for each client's dependencies, cache identities, build logs, reuse and cleanup.
