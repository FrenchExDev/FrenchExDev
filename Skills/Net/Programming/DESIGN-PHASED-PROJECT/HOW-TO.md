# DESIGN-PHASED-PROJECT — How-To

## Adding a New Binary Wrapper

### Step 1: Create the Design project

```
{Tool}/src/FrenchExDev.Net.{Tool}.Design/
  Program.cs
  {Tool}HelpParser.cs       (if custom format)
  {Tool}VersionCollector.cs  (if non-GitHub)
```

### Step 2: Implement `IHelpParser`

Study the tool's `--help` output format. Create a parser that extracts:
- Command name and description
- Subcommands (recursive tree)
- Flags/options with names, types, defaults, descriptions

Reference parsers:
- `VagrantHelpParser` — for "Common commands:" / "Available subcommands:" format
- `PodmanHelpParser` — for Cobra CLI framework output
- `GlabHelpParser` — for customized Cobra with ALL-CAPS headers

### Step 3: Implement `IVersionCollector` (or reuse)

For GitHub-hosted tools:
```csharp
var collector = new GitHubReleasesVersionCollector("org", "repo");
```

For custom sources, implement `IVersionCollector`.

### Step 4: Wire the DesignPipeline

```csharp
// Program.cs
var pipeline = new DesignPipeline()
    .UseImageBuild(ctx => new ImageBuildOptions
    {
        Tag = $"{tool}-scrape:{ctx.Version}",
        Dockerfile = "...",
    })
    .UseContainer(ctx => new ContainerOptions
    {
        Image = $"{tool}-scrape:{ctx.Version}",
    })
    .UseScraper(ctx => new ScraperOptions
    {
        Parser = new MyToolHelpParser(),
        SkippedCommands = new[] { "help", "completion" },
    })
    .Build();

await DesignPipelineRunner.RunAsync(pipeline, collector, outputDir);
```

### Step 5: Create the main project with descriptor

```
{Tool}/src/FrenchExDev.Net.{Tool}/
  scrape/                     (empty, will be populated by Design)
  {Tool}Descriptor.cs
```

```csharp
[BinaryWrapper("{tool}")]
public partial class {Tool}Descriptor { }
```

### Step 6: Run the Design phase

```bash
dotnet run --project {Tool}/src/FrenchExDev.Net.{Tool}.Design
```

Verify JSON files appear in `scrape/`. Commit them.

### Step 7: Build — SG generates code

The `BinaryWrapperGenerator` reads the JSON files (via `AdditionalFiles`) and the `[BinaryWrapper]` attribute, then generates command classes, builders, and the typed client.

## Adding a Schema-Based Generator (DockerCompose Variant)

When the external data is JSON schemas (not CLI help), the pipeline uses HTTP download instead of containers:

```csharp
var pipeline = new DesignPipeline()
    .UseHttpDownload(ctx => new HttpDownloadOptions
    {
        Url = $"https://raw.githubusercontent.com/.../v{ctx.Version}/schema.json",
    })
    .UseContentTransform(ctx => new TransformOptions
    {
        Parser = new SchemaParser(),
    })
    .UseSave(ctx => new SaveOptions
    {
        OutputPath = Path.Combine(outputDir, $"schema-v{ctx.Version}.json"),
    })
    .Build();
```

Reference: `DockerCompose/src/FrenchExDev.Net.DockerCompose.Bundle.Design/Program.cs`

## Running the Design Phase

```bash
# Scrape all versions
dotnet run --project {Tool}/src/FrenchExDev.Net.{Tool}.Design

# Scrape only missing versions
dotnet run --project ... -- --missing

# Scrape specific version
dotnet run --project ... -- --version 2.4.5

# Parallel scraping
dotnet run --project ... -- --parallel

# Re-extract from cached help text (no container needed)
dotnet run --project ... -- --reparse

# List available versions
dotnet run --project ... -- --list
```

## Debugging Scrape Failures

1. **Check container logs** — run the container manually and inspect `{tool} --help` output.
2. **Use `--list`** — verify the version collector returns expected versions.
3. **Check known-missing** — some versions have broken binaries or incompatible formats.
4. **Inspect JSON output** — compare failing version's JSON with a working version's JSON.
5. **Test parser independently** — feed the raw `--help` text to the parser in a unit test.

## Customizing the Help Parser

When to subclass vs. use existing:
- **Same CLI framework** (e.g., Cobra) — start from `PodmanHelpParser` or `GlabHelpParser` and customize.
- **Unique format** — create a new parser from scratch. Study the `--help` output for at least 3 versions before coding.
- **Decorator** — use `LoggingHelpParser` to wrap any parser and log raw input/output for debugging.
