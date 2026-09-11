# Plan: Git BinaryWrapper Solution

## Context

Create `FrenchExDev.Net.Git` -- a typed C# wrapper for the `git` CLI, following the established BinaryWrapper pattern (Podman, Vagrant, GitLab.Cli). The skeleton directory already exists at `Net/FrenchExDev/Git/` with empty `README.md`, `doc/*.md`, `src/`, `test/`.

## Project Structure

```
Git/
+-- FrenchExDev.Net.Git.slnx
+-- quality-gate.yml
+-- coverage.runsettings
+-- README.md
+-- doc/
|   +-- ARCHITECTURE.md
|   +-- HOW-TO.md
|   +-- PHILOSOPHY.md
|   +-- PLAN.md
+-- src/
|   +-- FrenchExDev.Net.Git/
|   |   +-- FrenchExDev.Net.Git.csproj
|   |   +-- GitDescriptor.cs
|   |   +-- scrape/                        (populated by Design tool)
|   +-- FrenchExDev.Net.Git.Design/
|       +-- FrenchExDev.Net.Git.Design.csproj
|       +-- Program.cs
|       +-- GitHelpParser.cs
+-- test/
    +-- FrenchExDev.Net.Git.Tests/
        +-- FrenchExDev.Net.Git.Tests.csproj
        +-- GitHelpParserTests.cs
```

## Design Decisions

### Scope: All Commands (Porcelain + Plumbing)

Root discovery uses `git help -a` (not `git -h`) to scrape all ~150 commands including low-level plumbing.

### Custom GitHelpParser (3 Modes)

Git has a unique help format requiring a custom `IHelpParser`:

1. **Root discovery** (`git help -a`): Title Case section headers ("Main Porcelain Commands", "Low-level Commands / Internal Helpers") followed by indented command lines.
2. **Leaf commands** (`git commit -h`): `usage:` line + GNU-style options (`-n, --dry-run`). Outputs to stderr, exits 129.
3. **Subcommand groups** (`git remote -h`, `git stash -h`): Multiple `usage:`/`or:` lines with embedded subcommand names.

### stderr / Exit Code 129

Git outputs `-h` help to stderr and exits with code 129. The `ProcessRunnerContainerRuntime.RunProcessAsync` throws on non-zero exit codes. Solution: a `.Use()` middleware wraps `ctx.RunHelp` to catch `InvalidOperationException` and extract stderr content.

### Build from Source

Git has no pre-built Linux binaries on GitHub. Each version is compiled from source tarball on Alpine (make/gcc). Slow (~2-5 min/version) but reliable. Use `--parallel 2` for initial runs.

### Version Range

- Min: 2.30.0 (Jan 2021, modern flag set, stable `-h` format)
- Collector: `GitHubReleasesVersionCollector("git", "git")` -- tags use `v` prefix

### Skipped Commands

`help`, `credential`, `credential-cache`, `credential-store`

## Key Dependencies

| What | Path |
|------|------|
| IHelpParser interface | `BinaryWrapper/src/.../Design/Code.cs` |
| StandardHelpParser.ParseOptionLine | `BinaryWrapper/src/.../Design/Code.cs` |
| CommandNodeBuilder | `BinaryWrapper/src/.../Design/Code.cs` |
| DesignPipeline + extensions | `BinaryWrapper/src/.../Design.Lib/` |
| DesignPipelineRunner | `BinaryWrapper/src/.../Design.Lib/DesignPipelineRunner.cs` |
| GitHubReleasesVersionCollector | `Wrapper.Versioning/` |

## Verification

1. `dotnet build Git/src/FrenchExDev.Net.Git.Design/` -- Design project compiles
2. `dotnet build Git/src/FrenchExDev.Net.Git/` -- Main project compiles (no generated code yet)
3. `dotnet test Git/test/FrenchExDev.Net.Git.Tests/` -- Parser tests pass
4. `dotnet build Git/FrenchExDev.Net.Git.slnx` -- Full solution builds
5. `dotnet run --project Git/src/FrenchExDev.Net.Git.Design -- --min-version 2.45.0 --parallel 1` -- Scrape to validate pipeline
