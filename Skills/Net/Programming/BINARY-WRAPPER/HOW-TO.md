# BINARY-WRAPPER — How To

How to add a brand-new typed wrapper for a CLI binary using the BinaryWrapper framework. The end result is a consumer package with a single 6-line descriptor and N hundred generated source files.

## Step 1 — Create the Project Layout

```
MyTool/
├── FrenchExDev.Net.MyTool.slnx
├── src/
│   ├── FrenchExDev.Net.MyTool/                # Library + generated code
│   │   ├── MyToolDescriptor.cs                # The 6-line descriptor
│   │   ├── scrape/                            # JSON command trees
│   │   │   └── mytool-1.0.0.json
│   │   └── FrenchExDev.Net.MyTool.csproj
│   └── FrenchExDev.Net.MyTool.Design/         # Scraping exe
│       ├── Program.cs
│       └── FrenchExDev.Net.MyTool.Design.csproj
└── test/
    └── FrenchExDev.Net.MyTool.Tests/
```

The Library project references `BinaryWrapper`, `BinaryWrapper.Attributes`, `BinaryWrapper.SourceGenerator` (as Analyzer), `Builder`, `Result`. The Design project references `BinaryWrapper.Design` and `BinaryWrapper.Design.Lib`.

## Step 2 — Pick a Version Collector

Decide where versions live. Use `Wrapper.Versioning` collectors:

```csharp
// GitHub releases (binary publishes formal releases)
new GitHubReleasesVersionCollector("containers", "podman");

// GitHub tags (releases don't match CLI versions, e.g. docker/cli)
new GitHubTagsVersionCollector("docker", "cli");

// GitLab releases (e.g. glab)
new GitLabReleasesVersionCollector("gitlab-org%2Fcli");

// Custom (HashiCorp releases.hashicorp.com, etc.)
public sealed class HashicorpVersionCollector : IVersionCollector { ... }

// Static list (testing)
new StaticVersionCollector(["1.0.0", "2.0.0"]);
```

If your binary lives behind a non-GitHub/GitLab API, implement `IVersionCollector` directly. It is a 1-method interface.

## Step 3 — Pick (or Write) a Help Parser

```csharp
// Built-ins
HelpParsers.Create("standard");   // generic GNU
HelpParsers.Create("cobra");      // Go/cobra: docker, podman, helm
HelpParsers.Create("argparse");   // Python argparse: podman-compose
HelpParsers.Create("packer");     // HashiCorp Packer
new VagrantHelpParser();          // Vagrant's quirky format
```

If your binary uses a parser family that already exists, you are done — pass the factory result to `UseScraper`.

If the format is unique, implement `IHelpParser`:

```csharp
public sealed class MyToolHelpParser : IHelpParser
{
    private static readonly HashSet<string> SkippedCommands =
        new(StringComparer.OrdinalIgnoreCase) { "help", "completion" };

    public CommandNode? Parse(string helpText, string commandName)
    {
        var builder = new CommandNodeBuilder(commandName);
        // walk lines, fill in commands/options/arguments
        return builder.Build();
    }
}
```

Wrap it in `LoggingHelpParser` while iterating: `new LoggingHelpParser(inner, logger, LogLevel.Debug)`.

**Always include a `SkippedCommands` set.** `help` causes infinite recursion. `completion` is never useful. `serve` (Vagrant) and similar starts a server and hangs the scrape.

## Step 4 — Build the Design Pipeline

```csharp
// Program.cs in MyTool.Design

using FrenchExDev.Net.BinaryWrapper.Design;
using FrenchExDev.Net.BinaryWrapper.Design.Lib;

Func<string, ILogger, IHelpParser> parser = (_, _) => HelpParsers.Create("cobra");

var pipeline = new DesignPipeline()
    .UseImageBuild(
        imageTagPrefix: "mytool-scrape",
        baseImage: "alpine:3.19",
        installScript: v =>
            "apk add --no-cache curl tar > /dev/null && " +
            $"curl -fsSL https://example.com/mytool-{v}-linux-amd64.tar.gz -o /tmp/m.tgz && " +
            "tar xzf /tmp/m.tgz -C /usr/local/bin && " +
            "chmod +x /usr/local/bin/mytool")
    .UseContainer()
    .UseScraper("mytool", parser)
    .Build();

var reparsePipeline = new DesignPipeline()
    .UseCachedHelp()
    .UseScraper("mytool", parser)
    .Build();

return await new DesignPipelineRunner
{
    VersionCollector = new GitHubReleasesVersionCollector("acme", "mytool"),
    Pipeline = pipeline,
    ReparsePipeline = reparsePipeline,
    DefaultMinVersion = "1.0.0",
    OutputFilePattern = "mytool-{version}.json",
    OutputDir = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..",
        "FrenchExDev.Net.MyTool", "scrape")),
}.RunAsync(args);
```

## Step 5 — Run the Scraper

```bash
cd MyTool/src/FrenchExDev.Net.MyTool.Design

# First scrape — all versions in parallel
dotnet run -- --parallel 8

# Incremental: only new versions
dotnet run -- --missing --parallel 8

# Re-parse only (no containers, uses cached help text)
dotnet run -- --reparse --parallel 10

# List target versions and exit
dotnet run -- --list

# Mark known-broken versions to skip permanently
dotnet run -- --add-known-missing 0.1.0,0.2.0
```

JSON files land in the sibling library's `scrape/` folder.

## Step 6 — Write the Descriptor

```csharp
using FrenchExDev.Net.BinaryWrapper.Attributes;

namespace FrenchExDev.Net.MyTool;

[BinaryWrapper("mytool")]
public partial class MyToolDescriptor;
```

For Go/HashiCorp-style binaries:

```csharp
[BinaryWrapper("packer",
    FlagPrefix = "-",
    FlagValueSeparator = "=",
    UseBoolEqualsFormat = true)]
public partial class PackerDescriptor;
```

## Step 7 — Wire Up the csproj

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
    <CompilerGeneratedFilesOutputPath>$(BaseIntermediateOutputPath)/Generated</CompilerGeneratedFilesOutputPath>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\..\BinaryWrapper\src\FrenchExDev.Net.BinaryWrapper\FrenchExDev.Net.BinaryWrapper.csproj" />
    <ProjectReference Include="..\..\..\BinaryWrapper\src\FrenchExDev.Net.BinaryWrapper.Attributes\FrenchExDev.Net.BinaryWrapper.Attributes.csproj" />
    <ProjectReference Include="..\..\..\BinaryWrapper\src\FrenchExDev.Net.BinaryWrapper.SourceGenerator\FrenchExDev.Net.BinaryWrapper.SourceGenerator.csproj"
                      OutputItemType="Analyzer"
                      ReferenceOutputAssembly="false" />
    <ProjectReference Include="..\..\..\Builder\src\FrenchExDev.Net.Builder\FrenchExDev.Net.Builder.csproj" />
    <ProjectReference Include="..\..\..\Result\src\FrenchExDev.Net.Result\FrenchExDev.Net.Result.csproj" />
  </ItemGroup>

  <ItemGroup>
    <AdditionalFiles Include="scrape\mytool-*.json" />
  </ItemGroup>
</Project>
```

Note: no `Version=` on `<PackageReference>` — Central Package Management governs versions in `Net/FrenchExDev/Directory.Packages.props`.

## Step 8 — Use the Generated API

```csharp
var binding = new BinaryBinding
{
    Identifier = new BinaryIdentifier("mytool"),
    ExecutablePath = "/usr/local/bin/mytool",
    DetectedVersion = SemanticVersion.Parse("2.0.0"),
};

var client = MyTool.Create(binding);

var cmd = client.Build(b => b
    .WithForce(true)
    .WithOutput("/tmp/out")
    .WithTemplate("template.json"));

var executor = new CommandExecutor(new DictionaryBinaryResolver(binding));
var result = await executor.ExecuteAsync(new BinaryIdentifier("mytool"), cmd);
```

## Step 9 — Optional: Output Parsing

If your binary emits machine-readable output, implement `IOutputParser<TEvent>` and `IResultCollector<TEvent, TResult>`:

```csharp
public sealed class MyToolEventParser : IOutputParser<MyToolEvent> { ... }
public sealed class MyToolBuildCollector : IResultCollector<MyToolEvent, BuildReport> { ... }

await foreach (var evt in execution)
{
    switch (evt) { case BuildProgress p: ...; }
}

var report = await execution.ExecuteAsync(new MyToolBuildCollector());
```

## Common Gotchas

- **Skip `help`, `completion`, and any server-mode subcommand.** Otherwise scraping never terminates.
- **Use a custom `tagToVersion` lambda** if upstream tags don't strip the `v` prefix or use `release-` style.
- **Some versions are permanently broken.** Mark them with `--add-known-missing` instead of trying to fix the parser.
- **Re-running `--reparse` is the right move when you change the parser**, not re-running the full pipeline.
- **`DotEnvLoader` finds `.env` by walking up.** Place a single `.env` with `GITHUB_TOKEN=...` at `Net/FrenchExDev/.env` for all consumers.
- **Generator targets `netstandard2.0`.** Do not import `net10.0`-only APIs in any source-generator project.
- **Generated files appear in `obj/Generated/`** when `EmitCompilerGeneratedFiles` is on. Inspect them when debugging.
- **The descriptor must be `partial`.** The generator emits a partial of the same name.
