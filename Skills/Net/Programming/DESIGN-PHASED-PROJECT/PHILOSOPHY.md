# DESIGN-PHASED-PROJECT — Philosophy

## 3 Phases: Design, Attributes, Source Generation

Complex code generation projects are decomposed into 3 distinct phases:

1. **Design** (`.Design` project) — scrape/download external data into JSON files
2. **Attributes** (`.Attributes` project) — declare code generation triggers
3. **Source Generation** (`.SourceGenerator` + runtime) — read JSON + attributes, generate code

This separation keeps each phase independently runnable, testable, and debuggable.

## Design-Time Is a First-Class Concern

`.Design` projects are **standalone CLI tools**, not build steps. They run manually (`dotnet run --project`), produce deterministic JSON output, and commit that output to source control.

Why CLI, not MSBuild target: design-time scraping involves containers, HTTP calls, and multi-version iteration. These are slow, flaky, and require specific infrastructure. Build steps should be fast and deterministic.

## Middleware Pipeline for Scraping

The `DesignPipeline` composes scraping steps as middleware:

```csharp
pipeline
    .UseImageBuild(...)     // Build container image
    .UseContainer(...)      // Run container
    .UseScraper(...)        // Scrape --help recursively
    .Build();               // Compose and execute
```

Each middleware step is independently replaceable. New steps compose without editing the runner.

See: `BinaryWrapper/src/FrenchExDev.Net.BinaryWrapper.Design.Lib/DesignPipeline.cs`

## Version-as-Dimension

Every external tool is scraped across **multiple versions**. The source generator reads all version JSON files and produces code that accounts for version differences (new commands, deprecated flags).

Examples: Vagrant (55 versions), Podman (55 versions), glab, DockerCompose (32 schema versions).

## Deterministic, Checked-In JSON

Design phase output (`scrape/{tool}-{version}.json`) is checked into source control. This means:
- Builds are deterministic — no network calls at build time
- No runtime dependency on external tools
- Diffs are reviewable (JSON changes are visible in PRs)
- Reproducibility — `--reparse` mode re-extracts from cached help text

The trade-off is repo size (55 versions x N commands = large directories), but the benefits outweigh the cost.

## Cleanup Is Mandatory

Design pipelines that use containers must include cleanup logic:
- `UseContainer` has try/finally that removes containers
- `UseImageBuild` tags images for cleanup
- The `finally` block in `DesignPipelineRunner` removes containers AND images

Never leave orphaned containers or images after a scrape run.
