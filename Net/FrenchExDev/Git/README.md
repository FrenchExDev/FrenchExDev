# FrenchExDev.Net.Git

Typed C# wrapper for the `git` CLI, built with the BinaryWrapper source generator. 136 versions scraped (2.30.0 through 2.55.0), with full option and argument typing. The 2.55.0 snapshot contains 150 top-level commands and 218 command nodes.

## Architecture

```
Phase 1: Scraping (Design project)

  GitHubTagsVersionCollector("git", "git")
    -> GitImagePlanResolver(version < 2.55: C dependencies; >= 2.55: add cargo/rustc)
    -> DesignImagePlan(debian:bookworm, dependencies shared within each recipe)
    -> UseVersionImage(source tarball, compile and install selected Git)
    -> UseContainer()
    -> RunHelp middleware (stderr/exit-129 + git help -a override)
    -> UseScraper("git", GitHelpParser, "-h")
    -> scrape/git-{version}.json

Phase 2: Source Generation (main project)

  BinaryWrapperGenerator reads git-*.json
    -> GitAddCommand.g.cs          (immutable command with ToArguments())
    -> GitAddCommandBuilder.g.cs   (fluent With*() methods)
    -> GitClient.g.cs              (typed client with async methods + nested groups)
```

## Projects

| Project | Role |
|---------|------|
| `FrenchExDev.Net.Git` | Runtime library. `GitDescriptor`, generated commands/builders/client. |
| `FrenchExDev.Net.Git.Design` | Design-time scraper. Custom `GitHelpParser`, build-from-source pipeline. |
| `FrenchExDev.Net.Git.Tests` | xUnit tests for the parser and version-dependent image recipes. |

## Quick Start

```csharp
using FrenchExDev.Net.Git;

// Create a client from a resolved binary binding
var client = Git.Create(binding);

// Build a typed command
var cmd = await client.CommitAsync(b => b
    .WithMessage("fix: resolve null reference")
    .WithAll(true));

// Inspect the serialized arguments
cmd.ToArguments(); // ["--message", "fix: resolve null reference", "--all"]

// Nested sub-groups
var addCmd = await client.Remote.AddAsync(b => b
    .WithName("origin")
    .WithUrl("https://github.com/user/repo.git"));

// Stash sub-commands
var pushCmd = await client.Stash.PushAsync(b => b.WithMessage("wip"));
```

## Scraping

### Prerequisites

- Podman (or Docker) running
- Internet access (downloads source tarballs from `github.com/git/git`)

Git 2.55 enables Rust by default. `GitImagePlanResolver` adds Bookworm's
`cargo rustc` packages from that version onward; older versions retain the
existing C dependencies. Both recipes use the same source installation commands,
and both `make` streams remain visible in the build log. `--build-base` prepares
both dependency recipes without discovering versions.

### Common Operations

```bash
cd Net/FrenchExDev/Git/src/FrenchExDev.Net.Git.Design

# List available versions
dotnet run --framework net10.0 -- --list
dotnet run --framework net10.0 -- --list --missing

# Scrape all missing versions
dotnet run --framework net10.0 -- --missing --parallel 2

# Scrape versions starting at 2.45.0
dotnet run --framework net10.0 -- --min-version 2.45.0 --parallel 1

# Reparse cached help text (after parser changes, no container needed)
dotnet run --framework net10.0 -- --reparse --parallel 8
```

Build-from-source is slow (~2-5 min per version). Use `--parallel 2` for initial runs, `--parallel 4` once stable.

### Version Coverage

- **134 versions** from 2.30.0 (Jan 2021) through 2.53.0
- Version discovery: `GitHubTagsVersionCollector` (git/git has tags only, no GitHub Releases)
- Build: Debian bookworm + shared make/gcc dependencies + source tarball from `github.com/git/git/archive/refs/tags/v{version}.tar.gz`

## GitHelpParser

Custom parser handling three distinct help formats:

| Mode | Input | Detection |
|------|-------|-----------|
| Root discovery | `git help -a` | Title Case section headers ("Main Porcelain Commands") |
| Leaf command | `git commit -h` | Single `usage:` line + GNU-style options |
| Subcommand group | `git remote -h` | Multiple `usage:`/`or:` lines |

### Design Decisions

- **`-h` not `--help`**: `--help` opens man pages (hangs); `-h` prints short help to stderr with exit 129
- **`git help -a`**: Root override intercepts `git -h` (shows ~30 commands) and runs `git help -a` (all ~150)
- **Argument extraction**: Positional arguments parsed from `usage:` lines; args after `[--]` marked optional
- **Skipped commands**: `help`, `credential`, `credential-cache`, `credential-store`
- **Option parsing**: Delegated to `StandardHelpParser.ParseOptionLine()` for GNU-style flags

## Property Name Resolution

The source generator resolves naming clashes between options, arguments, and reserved names:

| Clash | Resolution | Example |
|-------|-----------|---------|
| Option vs argument same name | Both prefixed | `--file` -> `OptFile`, `<file>` -> `ArgFile` |
| Option vs reserved member | Option suffixed | `--reference` -> `ReferenceOpt` |
| Argument vs reserved member | Argument suffixed | `<reference>` -> `ReferenceArg` |

## Dependencies

```
FrenchExDev.Net.Git
  +-- BinaryWrapper              (ICliCommand, BinaryBinding, SemanticVersion)
  +-- BinaryWrapper.Attributes   ([BinaryWrapper] marker attribute)
  +-- BinaryWrapper.SourceGenerator  (code generation, runs as Analyzer)
  +-- Builder.SourceGenerator.Lib    (builder generation, runs as Analyzer)
  +-- Builder                    (AbstractBuilder<T>)
  +-- Result                     (Result<T, TError>)

FrenchExDev.Net.Git.Design
  +-- BinaryWrapper.Design       (IHelpParser, StandardHelpParser)
  +-- BinaryWrapper.Design.Lib   (DesignPipeline, DesignPipelineRunner)
```

## Tests

```bash
cd Net/FrenchExDev/Git
dotnet test   # 15 tests: parser root discovery, leaf commands, subcommand groups
```

## Documentation

- [ARCHITECTURE.md](doc/ARCHITECTURE.md) -- project structure and 2-phase pipeline
- [HOW-TO.md](doc/HOW-TO.md) -- scraping commands and API usage examples
- [PHILOSOPHY.md](doc/PHILOSOPHY.md) -- why build-from-source, `-h` not `--help`, all commands
- [PLAN.md](doc/PLAN.md) -- implementation plan

## Shared dependency and version images

The Design runner prepares the shared system dependencies once, then installs each
software version in an image derived from that base. `UseVersionImage().UseContainer()`
builds or reuses the image before collecting help. After collection, the container
and version image are removed; `--keep-images` retains the version image. The
shared base remains cached.

From the wrapper directory, with Podman running (or add `--runtime docker`):

```powershell
$design = './src/FrenchExDev.Net.Git.Design/FrenchExDev.Net.Git.Design.csproj'
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
