# PACKER-BUNDLE — Architecture

## Project layout (5 projects)

```
Packer/src/
  FrenchExDev.Net.Packer.Bundle/                 # consumer library, hand + generated code
    HclWriter.cs
    PackerBundle.cs                              # mutable workspace
    PackerBundleWriter.cs                        # materializer
    BundleFile.cs                                # companion file
    Records/
      PackerConfig.cs                            # immutable record + builder
      PackerVariable.cs
      PackerLocal.cs
      PackerSource.cs
      PackerBuild.cs
    Communicators/
      SshCommunicator.cs
      WinrmCommunicator.cs
    obj/Generated/                               # plugin records + builders
  FrenchExDev.Net.Packer.Bundle.Attributes/      # [PackerBundle] marker (multi-target)
  FrenchExDev.Net.Packer.Bundle.SourceGenerator/ # IIncrementalGenerator (netstandard2.0)
    PackerBundleGenerator.cs
    PluginSpecReader.cs
    PluginRecordEmitter.cs
    BuilderHelper.cs
    NamingHelper.cs
  FrenchExDev.Net.Packer.Bundle.SourceGenerator.Lib/  # emitter logic, no Roslyn dep
  FrenchExDev.Net.Packer.Bundle.Design/          # GitHub scraper CLI (net10.0)
    Program.cs
    PackerPluginScraper.cs
    GitHubHcl2SpecFetcher.cs
    Hcl2SpecParser.cs
    PluginRegistry.cs
```

Plus a tests project: `test/FrenchExDev.Net.Packer.Bundle.Tests`.

## Dependency graph

```
Bundle.Attributes  (netstandard2.0 + net10.0)
       ▲
Bundle.SourceGenerator.Lib  (netstandard2.0, no Roslyn)
       ▲
Bundle.SourceGenerator      (netstandard2.0, Roslyn analyzer)
   ├── Bundle.SourceGenerator.Lib
   ├── Builder.SourceGenerator.Lib
   ├── Microsoft.CodeAnalysis.CSharp
       ▲
Bundle  (net10.0)
   ├── Bundle.Attributes
   ├── Bundle.SourceGenerator         (Analyzer)
   ├── Builder.SourceGenerator.Lib    (Analyzer)
   ├── Builder
   ├── Result
```

## The mutable workspace

```csharp
public class PackerBundle
{
    public PackerConfigBuilder Config { get; } = new();
    public List<PackerVariable> Variables { get; } = [];
    public List<PackerLocal> Locals { get; } = [];
    public List<PackerSource> Sources { get; } = [];
    public PackerBuildBuilder Build { get; } = new();

    public VagrantfileConfig Vagrantfile { get; set; } = new();
    public EnvTemplate EnvTemplate { get; set; } = new();
    public EnvValues EnvValues { get; set; } = new();

    public SortedList<string, BundleFile> Files { get; } = new();

    public IEnumerable<BundleFile> Scripts   => Files.Values.Where(f => f.Directory == "scripts");
    public IEnumerable<BundleFile> HttpFiles => Files.Values.Where(f => f.Directory == "http");

    public PackerBundle Apply(params IPackerBundleContributor[] contributors)
    {
        foreach (var c in contributors) c.Contribute(this);
        return this;
    }
}
```

`PackerBundle` is intentionally **not** a record. It is mutable, not
serializable, not equatable. Contributors mutate it in place; the writer
materializes a snapshot to disk.

`BundleFile` is also mutable so contributors can modify file content (e.g.
append shell snippets to a script).

## HclWriter — the only HCL2 emitter

There is exactly one HCL2 emitter in the codebase. It wraps an
`IndentedTextWriter` and exposes a small surface:

```csharp
public class HclWriter : IDisposable
{
    public HclWriter(TextWriter writer);

    public IDisposable Block(string type, params string[] labels);

    public void Argument(string name, string value);
    public void Argument(string name, int value);
    public void Argument(string name, bool value);
    public void Argument(string name, IEnumerable<string> list);
    public void Argument(string name, IDictionary<string, string> map);

    public void ArgumentListOfLists(string name, IEnumerable<IEnumerable<string>> lists);
    public void Expression(string name, string hclExpression);

    public void Comment(string text);
    public void BlankLine();
}
```

Strings are escaped per HCL2 rules (double-quoted, `\\`, `\"`, `\n`).
Heredocs (`<<EOF ... EOF`) are emitted via `Expression` for multi-line
content.

## Plugin scraping pipeline

```
PluginRegistry         ← hardcoded list of (org, repo, paths[]) for ~25 repos
       │
       ▼
GitHubHcl2SpecFetcher  ← GET /repos/{owner}/{repo}/contents/{path}, GITHUB_TOKEN
       │
       ▼ raw .hcl2spec.go content
Hcl2SpecParser         ← regex over `HCL2Spec()` map literal
       │
       ▼ ScrapedPluginType { TypeName, Fields[], NestedBlocks[] }
PackerPluginScraper    ← orchestrator, writes scrape/{type}.json
       │
       ▼
Bundle/scrape/*.json   ← committed to source control
```

`Hcl2SpecParser` runs two regexes:

```
&hcldec.AttrSpec\{Name:\s*"([^"]+)",\s*Type:\s*([^,]+),\s*Required:\s*(true|false)\}
&hcldec.BlockListSpec\{TypeName:\s*"([^"]+)"
```

No Go compiler, no AST. The `.hcl2spec.go` files are auto-generated and
follow a fixed shape, so regex is robust enough.

`GitHubHcl2SpecFetcher` honors `GITHUB_TOKEN` from the environment for
authenticated requests (5000/hr vs 60/hr unauthenticated). Same convention
as `GitLabReleasesVersionCollector`.

## Type mapping (cty → C#)

| cty type | C# |
|----------|-----|
| `cty.String` | `string?` |
| `cty.Bool` | `bool?` |
| `cty.Number` | `int?` (or `uint?` for sizes) |
| `cty.List(cty.String)` | `List<string>?` |
| `cty.List(cty.Number)` | `List<int>?` |
| `cty.List(cty.List(cty.String))` | `List<List<string>>?` (vboxmanage) |
| `cty.Map(cty.String)` | `Dictionary<string, string>?` |
| `hcldec.BlockListSpec` | nested record list |
| `time.Duration` | `string?` (Go duration format, validated by HCL2) |

## Source generator pipeline

```
context.AdditionalTextsProvider
   .Where(f => f.Path matches "scrape/*.json")
   .Collect()
   ↓
PluginSpecReader.Parse(json) → PluginSpecModel
   ↓
PluginRecordEmitter.Emit(model) → record + nested types
   ↓
BuilderHelper.From(model) → BuilderEmitModel
   ↓
BuilderEmitter.Emit(model) → builder source [shared]
```

Plugin records are emitted as immutable `record` types (positional or with
init). Each gets a builder via the shared `BuilderEmitter` from
`Builder.SourceGenerator.Lib`. The builder's `CreateInstance` constructs the
record from its private fields.

## PackerBundleWriter

Materializes a bundle to a directory:

```csharp
public class PackerBundleWriter
{
    public void Write(PackerBundle bundle, string outputDir)
    {
        Directory.CreateDirectory(outputDir);

        WriteHclFile(outputDir, "packer.pkr.hcl",    w => EmitConfig(w, bundle.Config.Build()));
        WriteHclFile(outputDir, "variables.pkr.hcl", w => EmitVariables(w, bundle.Variables));
        WriteHclFile(outputDir, "locals.pkr.hcl",    w => EmitLocals(w, bundle.Locals));
        WriteHclFile(outputDir, "sources.pkr.hcl",   w => EmitSources(w, bundle.Sources));
        WriteHclFile(outputDir, "build.pkr.hcl",     w => EmitBuild(w, bundle.Build.Build()));

        foreach (var file in bundle.Files.Values)
        {
            var path = Path.Combine(outputDir, file.Directory, $"{file.Name}.{file.Extension}");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, file.Content);
        }

        WriteEnvTemplate(outputDir, bundle.EnvTemplate);
        WriteEnvValues(outputDir, bundle.EnvValues);
        WriteVagrantfile(outputDir, bundle.Vagrantfile);
    }
}
```

## Contributor pattern

```csharp
public interface IPackerBundleContributor
{
    void Contribute(PackerBundle bundle);
}
```

Contributors typically live in downstream packages (e.g.
`Packer.Alpine`, `Packer.Alpine.DockerHost`). Each contributor is responsible
for one concern: a base OS, a Docker installation, a tooling layer.

A contributor can:

- Add records to `Variables`, `Locals`, `Sources`
- Mutate `Build` via its builder
- Add provisioning scripts via `Files`
- Populate `EnvTemplate` and `EnvValues`
- Modify `Vagrantfile` for downstream Vagrant compatibility

It must not assume order: the bundle may have already been touched by other
contributors.

## Test layout

| Test category | Asserts |
|---------------|---------|
| Scrape parser tests | Regex correctly extracts fields and types |
| Plugin SG tests | Generated records have expected properties |
| HclWriter tests | Output matches `terraform fmt`-style HCL2 |
| BundleWriter tests | Multi-file directory structure is correct |
| Contributor tests | Each contributor produces expected mutations |
| End-to-end tests | `dotnet new` style: bundle → write → `packer validate` |
