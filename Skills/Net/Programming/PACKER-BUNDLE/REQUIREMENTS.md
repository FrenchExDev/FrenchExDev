# PACKER-BUNDLE — Requirements

A packer-bundle implementation MUST satisfy the following constraints.

## Project structure

- [ ] Five projects exist: `Bundle`, `Bundle.Attributes`, `Bundle.SourceGenerator`,
      `Bundle.SourceGenerator.Lib`, `Bundle.Design`. Plus a `Bundle.Tests`
      project.
- [ ] `Bundle.Attributes` multi-targets `netstandard2.0;net10.0`.
- [ ] `Bundle.SourceGenerator` and `Bundle.SourceGenerator.Lib` target
      `netstandard2.0` only.
- [ ] `Bundle.Design` targets `net10.0`.
- [ ] `Bundle.SourceGenerator.Lib` has **no Roslyn dependency**.
- [ ] All package versions are declared in `Directory.Packages.props`.

## HCL2 emission

- [ ] Exactly one `HclWriter` class exists. No other class emits raw HCL2.
- [ ] `HclWriter` correctly escapes strings (`\\`, `\"`, `\n`).
- [ ] `HclWriter` produces valid HCL2 that `terraform fmt` would not change
      (apart from trailing whitespace).
- [ ] `HclWriter` emits blocks via an `IDisposable` returned by `Block(...)`,
      enforcing balanced braces.
- [ ] No JSON Packer template emission anywhere in the codebase.

## Mutable workspace

- [ ] `PackerBundle` is a `class`, not a `record`. It is mutable.
- [ ] `PackerBundle` exposes `Variables`, `Locals`, `Sources` as `List<T>`,
      `Build` and `Config` as builders, `Files` as `SortedList<string, BundleFile>`.
- [ ] `PackerBundle.Apply(params IPackerBundleContributor[])` returns `this`
      for chaining.
- [ ] `BundleFile` is mutable so contributors can append/replace content.

## Plugin scraping

- [ ] `PluginRegistry` is a hardcoded list of `(org, repo, paths[])` entries
      covering at least the 25 official Packer plugin repos.
- [ ] `GitHubHcl2SpecFetcher` honors `GITHUB_TOKEN` from the environment.
- [ ] `Hcl2SpecParser` parses `&hcldec.AttrSpec{...}` and
      `&hcldec.BlockListSpec{...}` via regex. No Go compiler.
- [ ] One JSON file is produced per plugin type, named `scrape/{type-name}.json`.
- [ ] All scraped JSON files are committed under `Bundle/scrape/`.
- [ ] The Design project is invoked manually, never from the build.

## Source generator behavior

- [ ] The generator is `IIncrementalGenerator`.
- [ ] All `scrape/*.json` files are declared as `<AdditionalFiles>` in
      `Bundle.csproj`.
- [ ] Every plugin type produces an immutable record + `AbstractBuilder<T>`.
- [ ] Builders are emitted via `BuilderEmitter.Emit(BuilderEmitModel)` from
      `Builder.SourceGenerator.Lib`. No locally-rolled builder emitter.
- [ ] cty type mapping covers `String`, `Bool`, `Number`, `List(String)`,
      `List(Number)`, `List(List(String))`, `Map(String)`, `BlockListSpec`.
- [ ] Shared communicator records (`SshCommunicator`, `WinrmCommunicator`)
      are detected and substituted when squashed in Go source.
- [ ] All exceptions in the generator are caught and emitted to a
      `GenerateError.g.cs` file.

## Multi-file template output

- [ ] `PackerBundleWriter.Write(bundle, outputDir)` produces:
      - `packer.pkr.hcl`
      - `variables.pkr.hcl` (only if any variables)
      - `locals.pkr.hcl` (only if any locals)
      - `sources.pkr.hcl`
      - `build.pkr.hcl`
- [ ] Companion files in `Files` are written to `outputDir/{Directory}/{Name}.{Extension}`.
- [ ] Empty sub-directories are not created.
- [ ] `EnvTemplate` is written to `.env.template` if non-empty.
- [ ] `EnvValues` is written to `.env` if non-empty.
- [ ] `Vagrantfile` is written when the model is non-default.

## Contributor pattern

- [ ] `IPackerBundleContributor` exists with a single
      `void Contribute(PackerBundle bundle)` method.
- [ ] Contributors mutate the bundle in place.
- [ ] Contributors do not assume any specific order.
- [ ] At least one downstream package (e.g. `Packer.Alpine`) ships a
      contributor that the bundle is fully driven by.

## Tests

- [ ] `HclWriter` has unit tests covering blocks, arguments, lists, maps,
      list-of-lists, expressions, escaping.
- [ ] `Hcl2SpecParser` has unit tests covering at least one real
      `.hcl2spec.go` excerpt per cty type.
- [ ] `PackerBundleWriter` has tests asserting the multi-file output shape.
- [ ] At least one end-to-end test composes a bundle, writes it, and runs
      `packer validate` against the result.

## Anti-requirements

- [ ] No JSON Packer templates. Ever.
- [ ] No HTML/Markdown plugin doc scraping. Only `.hcl2spec.go` files.
- [ ] No Go compiler invocation. Only regex parsing.
- [ ] No third-party HCL2 library (HCL1-only, GPL, or abandoned options
      exist; none qualify).
- [ ] No edits to `scrape/*.json` files by hand.
- [ ] No edits to `.g.cs` files.
- [ ] No `PackerBundle` serialization. It is in-memory only.
- [ ] No build-time GitHub access. The Design project is the only network
      consumer.
