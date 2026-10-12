# BINARY-WRAPPER — Architecture

The reference implementation lives at `Net/FrenchExDev/BinaryWrapper/`. Consumers (Vagrant, Packer, Podman, Docker, Git, GitLab.Cli, PodmanCompose) sit at `Net/FrenchExDev/<Tool>/`.

## Layered Package Topology

```
+-----------------------------------------------------------+
|  Consumer (e.g. FrenchExDev.Net.Podman)                   |
|  - Descriptor.cs (a partial class with [BinaryWrapper])   |
|  - scrape/podman-*.json (AdditionalFiles)                 |
|  - generated: PodmanClient, PodmanXxxCommand, ...Builder  |
+----+--------------------------------------------------+---+
     | runtime ref          analyzer ref                | runtime ref
     v                      v                           v
+----+----------------+ +---+----------------------+ +--+--------+
| BinaryWrapper       | | BinaryWrapper.SG         | | Builder   |
| (net10.0)           | | (netstandard2.0, Roslyn) | | (runtime) |
| - CommandExecutor   | | - CommandTreeReader      | +-----------+
| - VersionGuard      | | - VersionDiffer          | | Result    |
| - IProcessRunner    | | - CommandClassEmitter    | +-----------+
| - IOutputParser     | | - BuilderClassEmitter    |
| - IResultCollector  | | - ClientClassEmitter     |
| - BinaryBinding     | | - NamingHelper           |
+---------------------+ +--------------------------+

Design-time only (Exe project per consumer):
+---------------------------------------------------+
| BinaryWrapper.Design + BinaryWrapper.Design.Lib   |
| - DesignPipeline / DesignPipelineRunner           |
| - UseImageBuild / UseContainer / UseInlineContainer
| - UseScraper / UseCachedHelp                      |
| - HelpScraper (recursive depth-first)             |
| - IHelpParser implementations                     |
+---------------------------------------------------+
```

The **attribute** package targets `netstandard2.0;net10.0` so it can be referenced from both runtime and the source generator. The **source generator** package targets `netstandard2.0` only (Roslyn requirement). The **runtime** package targets `net10.0`.

## Design-Time Pipeline

The Design exe composes middleware on a `DesignPipeline` and hands it to a `DesignPipelineRunner` with a version collector.

```
DesignPipelineRunner
  |- IVersionCollector  -> ["1.9.0", "1.10.0", ...]
  |- DesignPipeline (compiled middleware delegate)
  |- ReparsePipeline (alternate, uses UseCachedHelp)
  |- OutputDir + OutputFilePattern
  |- parallel Channel<Version> workers
```

Two canonical pipeline shapes:

**Pattern A — UseImageBuild + UseContainer (most binaries)**

```csharp
new DesignPipeline()
    .UseImageBuild(
        imageTagPrefix: "podman-scrape",
        baseImage: "alpine:3.19",
        installScript: v => $"apk add curl tar && curl -L {url(v)} | tar -xz -C /usr/local/bin")
    .UseContainer()
    .UseScraper("podman", parser)
    .Build();
```

`UseImageBuild` builds `podman-scrape:<v>`, `UseContainer` runs it and sets `ctx.RunHelp` to `podman exec ...`, `UseScraper` calls `ctx.RunHelp` recursively and writes JSON. Each middleware owns its own `finally` cleanup.

**Pattern B — UseInlineContainer (Packer)**

```csharp
new DesignPipeline()
    .UseInlineContainer(baseImage: "alpine:3.19", installScript: v => "...")
    .UseScraper("packer", parser, helpFlag: "-h")
    .Build();
```

Single container, install + scrape in one shot. No cached image. Good for fast installs.

**ReparsePipeline (every consumer)**

```csharp
new DesignPipeline()
    .UseCachedHelp()                       // reads help/{version}/*.help.txt
    .UseScraper("podman", parser)          // re-runs parser, no container
    .Build();
```

`UseCachedHelp` sets `ctx.RunHelp` to a file-reader. Activated by `--reparse`. Lets you iterate on parsers in seconds.

## Recursive Help Scraper

`HelpScraper` performs depth-first descent:

```
scrape(path = []):
  text = ctx.RunHelp(path)
  node = parser.Parse(text, joined(path))
  for sub in node.SubCommands:
    if sub.Name in SkippedCommands: continue
    sub.Children = scrape(path + [sub.Name])
  return node
```

`SkippedCommands` is per-parser and includes things like `help` (infinite recursion), `completion` (no useful output), `serve` (starts a server and hangs). Skipping is mandatory; without it Vagrant scraping never terminates.

## Build-Time Source Generation

```
[BinaryWrapper("podman")] partial class PodmanDescriptor;
                  +
            scrape/podman-*.json (AdditionalFiles)
                  |
                  v
        CommandTreeReader (per-file)
                  |
                  v
        VersionDiffer.Merge -> UnifiedCommandTree
                  |
        +---------+----------+
        v         v          v
   CommandClass  Builder   Client
   Emitter      Emitter   Emitter
        |         |          |
        v         v          v
  Generated *.g.cs files
```

`VersionDiffer` walks every command across every JSON file, intersects/unions options, and stamps each artifact with its earliest and latest known version. The output is a single `UnifiedCommandTree` that the three emitters consume.

## Naming Collision Resolution

Collisions are resolved at the emitter, not propagated to user code:

| Symptom in raw help | Resolution |
|---|---|
| `--no-tty` and `--[no-]tty` | `NamingHelper.DeduplicateOptions` keeps one |
| Option name `tag[=name]` | `NamingHelper.ToPascalCase` strips `[]` and truncates at `=` |
| Reserved word like `class` | Escape with `@` or suffix `_` |
| Leaf command `version` clashes with sub-group `version` | `ClientClassEmitter.PruneClashingLeaves` removes the leaf |
| Two flags PascalCase to same name | Disambiguate with original-name suffix |

Each rule has a unit test against real JSON in `BinaryWrapper.SourceGenerator.Tests`.

## Runtime Execution Pipeline

```
Client.Build(b => b.WithForce(true))
  -> VersionGuard.EnsureCommandSupported(detectedVersion, "build")
  -> Builder.WithForce(true)
        -> VersionGuard.EnsureOptionSupported(version, "build", "force")
  -> Builder.BuildAsync() -> ICliCommand
                |
                v
CommandExecutor.ExecuteAsync(binaryId, command, parser, collector)
  -> IBinaryResolver.ResolveAsync -> BinaryBinding (path, version, overrides)
  -> CommandExecutor.BuildProcessSpec(binding, command.ToArguments())
        -> apply CommandOverrides (option name remapping, drop unsupported)
  -> IProcessRunner.StreamAsync(spec) -> IAsyncEnumerable<OutputLine>
  -> for each line: parser.ParseLine(line) -> 0..n TEvent
        -> for each event: collector.OnEvent(event)
  -> parser.Complete(exitCode) -> final events
  -> collector.Complete() -> Result<TResult, CommandError>
```

Three consumption modes share the same pipeline:

- **Raw** — `executor.ExecuteAsync(id, cmd)` returns `Result<ProcessOutput, CommandError>`
- **Streaming** — `await foreach (var evt in execution)`
- **Collected** — `execution.ExecuteAsync(collector)` returns `Result<TResult, CommandError>`

## VersionGuard

```csharp
public static class VersionGuard
{
    public static void EnsureCommandSupported(
        SemanticVersion? detected, string commandPath,
        string? since, string? until);

    public static void EnsureOptionSupported(
        SemanticVersion? detected, string commandPath, string optionName,
        string? since, string? until);
}
```

Generated `Builder.With*()` methods call this *before* setting the property. Generated `Client.<Command>()` methods call it before constructing the builder. If `detected` is null (version unknown), the guard is permissive — never block when you don't know.

## Real-World Anchors

| File | Purpose |
|---|---|
| `Net/FrenchExDev/BinaryWrapper/src/.../SourceGenerator/CommandClassEmitter.cs` | Per-command class generation |
| `Net/FrenchExDev/BinaryWrapper/src/.../SourceGenerator/ClientClassEmitter.cs` | Top-level client + nested groups |
| `Net/FrenchExDev/BinaryWrapper/src/.../SourceGenerator/VersionDiffer.cs` | Multi-version merge |
| `Net/FrenchExDev/BinaryWrapper/src/.../SourceGenerator/NamingHelper.cs` | Collision resolution |
| `Net/FrenchExDev/BinaryWrapper/src/.../Design.Lib/DesignPipelineRunner.cs` | Parallel orchestration |
| `Net/FrenchExDev/BinaryWrapper/src/.../Design/Pipeline/UseImageBuildMiddleware.cs` | Container image stage |
| `Net/FrenchExDev/Vagrant/src/.../Vagrant.Design/VagrantHelpParser.cs` | Custom parser anchor |
| `Net/FrenchExDev/Podman/src/.../Podman.Design/Program.cs` | Pattern A pipeline anchor |
| `Net/FrenchExDev/Packer/src/.../Packer.Design/Program.cs` | Pattern B pipeline anchor |
