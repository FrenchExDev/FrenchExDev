# DESIGN-PHASED-PROJECT — Requirements

Non-negotiable rules for design-phased projects.

## Output

- **Design output (JSON) must be checked into source control.** The `scrape/` directory is part of the repo. Builds must not require network calls or external tools.
- **Output format: `{tool}-{version}.json`** — one file per version in the `scrape/` directory. No combined files.
- **Output must be deterministic.** Same version + same tool = same JSON. Use `--reparse` to re-extract from cached help text and verify.

## Design Projects

- **Design projects are standalone CLI tools.** They are NOT build steps and NOT MSBuild targets. Run manually via `dotnet run --project {Design}`.
- **Every Design project has a `Program.cs`** that configures `DesignPipeline` and calls `DesignPipelineRunner`.

## Version Collection

- **Every external tool needs an `IVersionCollector`.** Reuse `GitHubReleasesVersionCollector("org", "repo")` when possible.
- **Custom collectors only for non-GitHub sources** — `GitLabReleasesVersionCollector` for GitLab API, `VagrantVersionCollector` for HashiCorp releases.
- **`DefaultMinVersion` must be set** when early versions have broken assets or incompatible formats.

## Help Parsing

- **Every external tool needs a custom `IHelpParser`.** Each tool has unique `--help` format. Never assume a generic parser will work.
- **Parsers must handle version differences gracefully.** Older versions may have different headers, missing subcommands, or broken `--help` output.

## Scraping

- **SkippedCommands must be explicit.** Always declare `help`, `completion`, and any hanging/recursive commands in the skip list.
- **Broken versions must be documented.** If a version cannot be scraped (e.g., Podman 4.1.0, 4.3.0 — static binaries not truly static on Alpine), document the reason and add to known-missing list.

## Cleanup

- **DesignPipeline middleware must include cleanup.** `UseContainer` must have try/finally that removes containers. `UseImageBuild` should tag images for cleanup.
- **Never leave orphaned containers or images** after a scrape run, even on failure.

## Container Images

- **Container images are tagged `{tool}-scrape:{version}`** for identification and cleanup.
- **Base images must be minimal** — Alpine or Debian slim. No full desktop distributions.
- **WSL workarounds must be documented** — e.g., Vagrant's `@_wsl = false` patch.
