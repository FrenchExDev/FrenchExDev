# FrenchExDev.Net.Git

Typed C# wrapper for the `git` CLI, built with the BinaryWrapper source generator. 134 versions scraped (2.30.0 through 2.53.0), 422 generated source files covering ~150 commands with full option and argument typing.

## Architecture

```
Phase 1: Scraping (Design project)

  GitHubTagsVersionCollector("git", "git")
    -> UseImageBuild(alpine:3.19, make + gcc, source tarball)
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
| `FrenchExDev.Net.Git.Tests` | xUnit tests for the parser (15 tests). |

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

### Common Operations

```bash
cd Net/FrenchExDev/Git/src/FrenchExDev.Net.Git.Design

# List available versions
dotnet run -- --list
dotnet run -- --list --missing

# Scrape all missing versions
dotnet run -- --missing --parallel 2

# Scrape a specific version
dotnet run -- --min-version 2.45.0 --max-version 2.45.0 --parallel 1

# Reparse cached help text (after parser changes, no container needed)
dotnet run -- --reparse --parallel 8
```

Build-from-source is slow (~2-5 min per version). Use `--parallel 2` for initial runs, `--parallel 4` once stable.

### Version Coverage

- **134 versions** from 2.30.0 (Jan 2021) through 2.53.0
- Version discovery: `GitHubTagsVersionCollector` (git/git has tags only, no GitHub Releases)
- Build: Alpine 3.19 + make/gcc + source tarball from `github.com/git/git/archive/refs/tags/v{version}.tar.gz`

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
