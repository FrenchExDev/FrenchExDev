# BINARY-WRAPPER — Requirements

Hard rules for any wrapper built on this pattern. A wrapper that violates these is broken.

## Project Structure

- **MUST** ship at least three projects per consumer:
  - `<Tool>` — runtime library, contains the descriptor and generated code
  - `<Tool>.Design` — design-time exe, contains `Program.cs` with the pipeline
  - `<Tool>.Tests` — xUnit tests for the parser and any custom output parsers
- **MUST** put scraped JSON files at `<Tool>/src/FrenchExDev.Net.<Tool>/scrape/<binary>-{version}.json`.
- **MUST** include the JSON files via `<AdditionalFiles Include="scrape\<binary>-*.json" />`.
- **MUST NOT** check in generated `*.g.cs` files. They are emitted from JSON on every build.

## Descriptor

- **MUST** be a `partial class` with the `[BinaryWrapper("<binary-name>")]` attribute.
- **MUST NOT** contain hand-written members. The descriptor is a marker only.
- **MUST** use `FlagPrefix`, `FlagValueSeparator`, and `UseBoolEqualsFormat` if the CLI is not GNU-style. Default = GNU (`--flag value`).

## Source Generator Targeting

- **MUST** target `netstandard2.0` for any project that compiles into the source generator.
- **MUST** target `netstandard2.0;net10.0` for the Attributes project (so it can be referenced from both runtime and the SG).
- **MUST** reference `Microsoft.CodeAnalysis.CSharp` 5.3.0 or compatible (per CPM).
- **MUST NOT** use `net10.0`-only APIs in any SG code path.

## Help Scraping

- **MUST** be performed inside an isolated container (Podman or Docker), not against the host's installed binary.
- **MUST** be invoked manually by the developer, never on `dotnet build`.
- **MUST** include `help` and `completion` in `SkippedCommands` for any custom parser. Optionally `serve`, `daemon`, or any sub-command that opens a port or hangs on `--help`.
- **MUST** persist a JSON file per scraped version. JSON is the only contract between Design and Build phases.
- **SHOULD** dump raw `.help.txt` to a `help/{version}/` directory so `--reparse` works.

## Version Collection

- **MUST** implement `IVersionCollector` (or reuse one from `Wrapper.Versioning`).
- **MUST** strip the leading `v` from tags by default; override with `tagToVersion` lambda otherwise.
- **MUST** filter pre-releases by default. The wrapper targets stable releases.
- **MUST** support `GITHUB_TOKEN` (or `GITLAB_TOKEN`) via env var. Without a token the API rate-limits to 60/hour.

## Pipeline Composition

- **MUST** use `DesignPipeline` middleware composition. No hand-rolled scraping loops.
- **MUST** define a `ReparsePipeline` using `UseCachedHelp` so parser changes can be re-applied without re-running containers.
- **MUST** clean up containers and images in `finally` blocks. Each middleware owns its lifecycle.
- **MUST** call `RunAsync(args)` from `Main` so `--parallel`, `--missing`, `--list`, `--reparse` work uniformly.

## Multi-Version Support

- **MUST** scrape at least two versions when shipping. Single-version wrappers are forbidden — they hide the versioning bug.
- **MUST** rely on `VersionDiffer.Merge` to produce `[SinceVersion]` / `[UntilVersion]` annotations. Do not hand-annotate.
- **MUST** allow `BinaryBinding.DetectedVersion` to be null. When null, `VersionGuard` is permissive.

## Naming

- **MUST** delegate option / command name PascalCasing to `NamingHelper`.
- **MUST** route deduplication through `NamingHelper.DeduplicateOptions` — do not invent ad-hoc suffixes.
- **MUST NOT** leak `[]`, spaces, `=`, or other punctuation into generated identifiers.
- **MUST NOT** generate a leaf command and a sub-group with the same name. `ClientClassEmitter.PruneClashingLeaves` is responsible.

## Runtime API Surface

- **MUST** generate one sealed `<Pascal>Command` class per scraped command, implementing `ICliCommand`.
- **MUST** generate one `<Pascal>CommandBuilder : AbstractBuilder<<Pascal>Command>` per command.
- **MUST** generate exactly one client (`<Tool>Client`) with nested groups mirroring the binary's command tree.
- **MUST** call `VersionGuard.EnsureCommandSupported` from generated client methods *before* constructing the builder.
- **MUST** call `VersionGuard.EnsureOptionSupported` from generated `With*()` methods *before* setting the property.
- **MUST NOT** use reflection at runtime. Everything is direct method calls.

## Output Parsing (optional layer)

- **MAY** implement `IOutputParser<TEvent>` for binaries with structured output.
- **MAY** implement `IResultCollector<TEvent, TResult>` to aggregate events.
- **MUST** support all three consumption modes (raw, streaming, collected) — that comes for free if you use the framework's `CommandExecutor`.
- **MUST NOT** parse stdout in user code. Parsing belongs in `IOutputParser<TEvent>`.

## Testing

- **MUST** test custom `IHelpParser` implementations against real captured `.help.txt` files.
- **MUST** add a regression test for any naming-collision edge case discovered in scraped JSON.
- **SHOULD** keep parser tests in `<Tool>.Tests` — not in `BinaryWrapper.Tests`.
- **SHOULD** use hand-written `Fakes` for `IProcessRunner` / `IBinaryResolver` in execution tests. No mocking frameworks.

## Documentation

- **MUST** ship a `README.md` at the consumer root with a Quick Start.
- **MUST** ship a `CLAUDE.md` at the consumer root pointing to this skill and the package docs.
- **SHOULD** ship `doc/ARCHITECTURE.md` and `doc/HOW-TO.md` for non-trivial parsers.
- **SHOULD** document version-specific bugs (e.g. broken upstream releases) in CLAUDE.md notes so future contributors do not "fix" them.

## Forbidden

- **NEVER** add `Version=` to `<PackageReference>`. Central Package Management governs versions.
- **NEVER** check in generated `*.g.cs` files.
- **NEVER** hand-write a command, builder, or client class for a binary that has a wrapper. Re-scrape and regenerate.
- **NEVER** introduce a mocking framework. Use hand-written Fakes.
- **NEVER** make scraping run on `dotnet build`. It must stay manual.
- **NEVER** silently absorb a broken upstream version. Use `--add-known-missing` and document it.
