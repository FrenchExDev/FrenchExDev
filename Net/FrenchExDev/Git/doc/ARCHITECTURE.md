# Architecture

## Overview

`FrenchExDev.Net.Git` is a BinaryWrapper project that generates a typed C# API for the `git` CLI. It follows the same 2-phase architecture as the Podman, Vagrant, and GitLab.Cli wrappers.

## Projects

| Project | Role |
|---------|------|
| `FrenchExDev.Net.Git` | Runtime library. Contains `GitDescriptor` and generated commands/builders/client. |
| `FrenchExDev.Net.Git.Design` | Design-time console app. Scrapes git help output across versions into JSON. |
| `FrenchExDev.Net.Git.Tests` | xUnit tests for the parser and generated types. |

## Phase 1: Scraping (Design Project)

```
DesignPipelineRunner
  -> GitHubTagsVersionCollector("git", "git")        // discover versions from tags
  -> UseImageBuild("alpine:3.19", make + gcc)       // compile git from source
  -> UseContainer()                                  // start container
  -> RunHelp wrapper middleware                      // handle stderr/exit-129 + git help -a
  -> UseScraper("git", GitHelpParser, "-h")          // recursive help scraping
  -> git-{version}.json                              // output to scrape/
```

### GitHelpParser (3 Modes)

The parser detects the help format and switches mode:

1. **Root discovery** -- parses `git help -a` output. Title Case section headers ("Main Porcelain Commands") with indented command lines. Discovers all ~150 commands.

2. **Leaf commands** -- parses `git <cmd> -h` output. `usage:` line followed by GNU-style options. Delegates option parsing to `StandardHelpParser.ParseOptionLine()`.

3. **Subcommand groups** -- parses multi-usage output (`git remote -h`, `git stash -h`). Extracts subcommand names from `usage:`/`or:` lines.

### stderr / Exit 129 Workaround

Git outputs `-h` help to stderr and exits with code 129. A middleware wraps `ctx.RunHelp` to catch `InvalidOperationException` and extract the stderr content from the error message.

### Root Command Override

The scraper normally runs `git -h` for root discovery, which only shows ~30 common commands. The middleware intercepts root-level calls and runs `git help -a` instead, getting all commands including plumbing.

## Phase 2: Source Generation (Main Project)

The `BinaryWrapperGenerator` reads `scrape/git-*.json` files and generates:

- **Command classes** (`GitAddCommand`, `GitCommitCommand`, ...) -- immutable records with `ToArguments()` serialization
- **Builder classes** (`GitAddCommandBuilder`, ...) -- fluent `With*()` methods via `AbstractBuilder<T>`
- **Client class** (`GitClient`) -- typed entry point with `async` methods and nested sub-groups

Entry point: `Git.Create(binding)` returns a `GitClient`.

## Dependencies

```
FrenchExDev.Net.Git
  -> BinaryWrapper (runtime interfaces)
  -> BinaryWrapper.Attributes ([BinaryWrapper] attribute)
  -> BinaryWrapper.SourceGenerator (code generation, Analyzer)
  -> Builder.SourceGenerator.Lib (builder generation, Analyzer)
  -> Builder (AbstractBuilder<T>)
  -> Result (Result<T, TError>)

FrenchExDev.Net.Git.Design
  -> BinaryWrapper.Design (IHelpParser, StandardHelpParser, parsers)
  -> BinaryWrapper.Design.Lib (DesignPipeline, DesignPipelineRunner)
```
