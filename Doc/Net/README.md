# FrenchExDev.Net Documentation

Documentation index for the **FrenchExDev.Net** mono-repository.

## Repository overview

FrenchExDev.Net is a .NET 10 mono-repo built around two foundational libraries (**Result** and **Builder**) and a code-generation framework (**BinaryWrapper**) that produces typed C# clients for CLI tools. Several generated CLI wrappers ship on top of this framework, alongside standalone utilities.

### Architecture at a glance

```
Net/FrenchExDev/
 |
 |-- Result/               Result monad (Result, Result<T>, Result<T,TError>)
 |-- Builder/              Async builder pattern + Roslyn source generator
 |
 |-- BinaryWrapper/        Framework: scrape CLI help -> JSON -> generated C# client
 |   |-- Docker/           Generated Docker CLI wrapper          (77 versions)
 |   |-- DockerCompose/    Generated Docker Compose wrapper      (in progress)
 |   |-- Vagrant/          Generated Vagrant CLI wrapper         (7 versions)
 |   |-- Podman/           Generated Podman CLI wrapper          (55 versions)
 |   |-- PodmanCompose/    Generated Podman Compose wrapper      (in progress)
 |   +-- Packer/           Generated Packer CLI wrapper          (21 versions)
 |
 |-- DockAi/               AI-powered document search & viewer (Lucene.Net)
 +-- Doc2Pdf/              Document-to-PDF conversion utility
```

## Packages

### Result

`Result`, `Result<T>`, and `Result<T, TError>` types implementing the Result/Either monad pattern. Includes extension methods for `Map`, `Bind`, `Recover`, and `FromTry`. Multi-targets `netstandard2.0` and `net10.0`.

- 47 tests, 100 % branch coverage
- Solution: `Result/FrenchExDev.Net.Result.slnx`

### Builder

Source-generator-based builder pattern with async validation.

| Project | Role |
|---------|------|
| `FrenchExDev.Net.Builder` | `AbstractBuilder<T>` and `AbstractBuilder<T, TException>` base classes |
| `FrenchExDev.Net.Builder.Attributes` | `[Builder]` attribute (netstandard2.0) |
| `FrenchExDev.Net.Builder.SourceGenerator` | Roslyn incremental generator — emits properties, validators, and instantiation bridge |
| `FrenchExDev.Net.Builder.Testing` | Test helpers |

Annotate a class with `[Builder]` to get generated input properties, per-property `Validate{Prop}` hooks, and a full `ValidateAsync` override. Set `[Builder(Exception = typeof(MyEx))]` to switch to the `AbstractBuilder<T, TException>` variant.

- Solution: `Builder/FrenchExDev.Net.Builder.slnx`

### BinaryWrapper (framework)

Framework for generating strongly-typed .NET wrappers around arbitrary CLI tools.

**Pipeline:** scrape binary `--help` output into JSON metadata -> Roslyn source generator reads JSON -> emits command classes, builder classes, and a client facade.

| Project | Role |
|---------|------|
| `FrenchExDev.Net.BinaryWrapper` | Runtime infrastructure |
| `FrenchExDev.Net.BinaryWrapper.Attributes` | `[BinaryWrapper("name")]` attribute with `FlagPrefix`, `FlagValueSeparator`, `UseBoolEqualsFormat` |
| `FrenchExDev.Net.BinaryWrapper.Design` | Design-time scraping tool |
| `FrenchExDev.Net.BinaryWrapper.SourceGenerator` | Incremental generator (command, builder, client emitters + version differ) |
| `FrenchExDev.Net.BinaryWrapper.Testing` | Test helpers |

Key source-generator features:

- **Multi-version support** — reads multiple JSON files per binary and generates a unified API with version-aware members
- **Version diffing** — `VersionDiffer` detects API additions and removals across versions
- **Clash pruning** — `ClientClassEmitter.PruneClashingLeaves` handles leaf commands that collide with sub-group names
- **Name deduplication** — `NamingHelper.DeduplicateOptions` resolves options that map to the same PascalCase name

84 tests passing.

- Solution: `BinaryWrapper/FrenchExDev.Net.BinaryWrapper.slnx` (within the central solution)

### Docker (generated wrapper)

Typed wrapper for the Docker CLI. 77 scraped versions (23.0.0 through 27.x).

- Descriptor: `[BinaryWrapper("docker")]`
- Solution: `Docker/FrenchExDev.Net.Docker.slnx`

### Docker Compose (generated wrapper — in development)

Typed wrapper for the Docker Compose CLI. Structure in place, scrape data not yet collected.

- Descriptor: `[BinaryWrapper("docker-compose")]`
- Solution: `DockerCompose/FrenchExDev.Net.DockerCompose.slnx`

### Vagrant (generated wrapper)

Typed wrapper for HashiCorp Vagrant. 7 scraped versions (2.4.3 – 2.4.9).

Includes custom output parsers (`VagrantMachineReadableParser`, `VagrantOutputParser`) and event/result types. Design project uses a custom `VagrantHelpParser` for Vagrant's non-standard help headers. Scraping runs in pre-built `vagrant-scrape:{version}` Podman images with a WSL compatibility patch.

- Descriptor: `[BinaryWrapper("vagrant")]`
- 135 generated source files
- Solution: `Vagrant/FrenchExDev.Net.Vagrant.slnx`

### Podman (generated wrapper)

Typed wrapper for Podman. 55 scraped versions (4.1.1 – 5.8.0; 4.1.0 and 4.3.0 excluded due to broken static binaries on Alpine).

Design project includes a Cobra-aware help parser that recognizes typed flag hints (`string`, `int`, `uint`, `stringArray`, etc.). Two-phase scraping: phase 1 builds `podman-scrape:{version}` images, phase 2 scrapes.

- Descriptor: `[BinaryWrapper("podman")]`
- 374 generated source files
- Solution: `Podman/FrenchExDev.Net.Podman.slnx`

### Podman Compose (generated wrapper — in development)

Typed wrapper for the Podman Compose CLI. Structure in place, scrape data not yet collected.

- Descriptor: `[BinaryWrapper("podman-compose")]`
- Solution: `PodmanCompose/FrenchExDev.Net.PodmanCompose.slnx`

### Packer (generated wrapper)

Typed wrapper for HashiCorp Packer. 21 scraped versions (1.10.0 – 1.11.2).

Uses a non-default flag format (single dash, equals separator, boolean equals format). Includes `PackerBuildParser` for structured build output and machine-readable output parsing.

- Descriptor: `[BinaryWrapper("packer", FlagPrefix = "-", FlagValueSeparator = "=", UseBoolEqualsFormat = true)]`
- 29 generated source files, 146 tests passing
- Solution: `Packer/FrenchExDev.Net.Packer.slnx`

### DockAi

AI-powered document search and viewer built on ASP.NET Core. Uses Lucene.Net for full-text indexing with support for multiple document formats (Office via OpenXml/ClosedXML, PDF via PdfPig, Markdown via Markdig, HTML via HtmlAgilityPack, CSV via CsvHelper).

| Project | Role |
|---------|------|
| `DockAi.Api` | ASP.NET Core API backend with integrated Lucene search |
| `DockAi.Viewer` | Web-based document viewer |

### Doc2Pdf

Standalone utility for converting documents to PDF format.

## Build infrastructure

### Central Package Management

All NuGet package versions are declared in `Net/FrenchExDev/Directory.Packages.props`. Individual `.csproj` files must **never** specify a `Version=` attribute on `<PackageReference>` elements.

### Directory.Build.props

Global build settings applied to every project:

- `LangVersion`: latest
- `Nullable`: enable
- `TreatWarningsAsErrors`: true
- `EnforceCodeStyleInBuild`: true
- `ImplicitUsings`: enable

### SDK

.NET 10.0.200-preview. The `NETSDK1057` warning is expected and can be ignored.

### Encoding caveat

`Code.cs` files in the Builder and Result packages use **UTF-16LE** encoding. Standard `Write`/`Edit` tools emit UTF-8, so edits to those files must use PowerShell:

```powershell
[System.IO.File]::WriteAllText($path, $content, [System.Text.Encoding]::Unicode)
```

### Solutions

| Solution | Scope |
|----------|-------|
| `FrenchExDev.Net.slnx` | Central — includes all packages |
| `Builder/FrenchExDev.Net.Builder.slnx` | Builder only |
| `Result/FrenchExDev.Net.Result.slnx` | Result only |
| `Docker/FrenchExDev.Net.Docker.slnx` | Docker wrapper |
| `DockerCompose/FrenchExDev.Net.DockerCompose.slnx` | Docker Compose wrapper |
| `Vagrant/FrenchExDev.Net.Vagrant.slnx` | Vagrant wrapper |
| `Podman/FrenchExDev.Net.Podman.slnx` | Podman wrapper |
| `PodmanCompose/FrenchExDev.Net.PodmanCompose.slnx` | Podman Compose wrapper |
| `Packer/FrenchExDev.Net.Packer.slnx` | Packer wrapper |
| `Doc2Pdf/FrenchExDev.Net.Doc2Pdf.slnx` | Doc2Pdf utility |

## Tooling documentation

- [Update-Packages.ps1](Update-Packages.md) — Interactive NuGet package updater for Central Package Management
