# FrenchExDev.Net

.NET 10 libraries, source generators, and tools. All projects use Central Package Management via `Directory.Packages.props` and share build settings through `Directory.Build.props`.

## Projects

### Foundation

| Project | Description |
|---------|-------------|
| [Result](Result/) | Immutable Result types (`Result`, `Result<T>`, `Result<T, TError>`) replacing exceptions and null with explicit, composable success/failure values. Map, Bind, Recover, Tap, Ensure, Combine, and FromTry extensions. 47 tests, 100% branch coverage. |
| [Builder](Builder/) | Source-generator-powered async object construction framework. `[Builder]` attribute generates fluent `With*()` methods, per-property validation, and thread-safe building. Four instantiation strategies (`init`, `ctor`, `factory:X`, `custom`). Supports circular object graphs via `Reference<T>`. Shared `BuilderEmitter` reused by downstream generators. 24 tests. |
| [FiniteStateMachine](FiniteStateMachine/) | Three-tier FSM library: Dynamic (string-based), Typed (enum-based, source-generated), and Rich (interface-based, domain-driven). Hierarchical states, parallel regions, async guards/actions, deferred events, timer transitions, Mermaid/Graphviz visualization. Returns `Result<Transition<TState>>`. 92 tests. |

### Metamodeling & DSLs

| Project | Description |
|---------|-------------|
| [Dsl](Dsl/) | M3 metamodeling foundation. Five primitives (`[MetaConcept]`, `[MetaProperty]`, `[MetaReference]`, `[MetaConstraint]`, `[MetaInherits]`) used to define all other DSLs. Roslyn source generator produces `MetamodelRegistry`. 9 tests. |
| [Ddd](Ddd/) | Domain-Driven Design DSL built on Dsl. 13 attributes (`[AggregateRoot]`, `[Entity]`, `[ValueObject]`, `[Command]`, `[DomainEvent]`, etc.) generate entity implementations, builders, EF Core configs, and repositories from attributed partial classes. 16 tests. |
| [Requirements](Requirements/) | Type-safe requirements engineering DSL. Requirement hierarchy (`Epic` > `Feature<T>` > `Story<T>` > `RequirementTask<T>`, `Bug`) with `[ForRequirement]`, `[Verifies]`, `[TestsFor]` linking attributes. Compile-time diagnostics via analyzers (REQ100+). 10 tests. |

### Content Management Framework

| Project | Description |
|---------|-------------|
| [Diem](Diem/) | Content Management Framework built on Dsl and Ddd. Four sub-DSLs: **Content** (Parts, Blocks, StreamFields), **Admin** (Lists, Forms, Actions), **Pages** (Widgets, Layouts, Routing), **Workflow** (StateMachine, Gates, Scheduling, Locales). 9 built-in parts, 4 built-in blocks. CLI (`cmf`) for scaffolding. Aspire AppHost for local dev. 81 tests across 6 test projects. |

### BinaryWrapper Framework

| Project | Description |
|---------|-------------|
| [BinaryWrapper](BinaryWrapper/) | Framework for generating type-safe .NET wrappers from CLI help text. Two-phase pipeline: scrape `--help` into JSON at design time, then Roslyn source generator produces command classes, fluent builders, and versioned clients. Multi-version support via `VersionDiffer`, `[SinceVersion]`/`[UntilVersion]` annotations, and `VersionGuard` runtime checks. |
| [Wrapper.Versioning](Wrapper.Versioning/) | Reusable design-time pipeline infrastructure for version discovery and parallel downloading. Version collectors for GitHub Releases, GitHub Tags, GitLab Releases, and static lists. Composable middleware pipeline (`UseHttpDownload`, `UseContentTransform`, `UseSave`). CLI runners with `--parallel`, `--missing`, `--list`. 142 tests, quality score 1.0. |
| [Vagrant](Vagrant/) | Typed wrapper for HashiCorp Vagrant. Custom `VagrantHelpParser` for "Common commands:"/"Available subcommands:" headers. Build-from-source scraping pipeline. |
| [Packer](Packer/) | Typed wrapper for HashiCorp Packer. Commands: build, validate, init, fmt, inspect, hcl2-upgrade, console, plugins. Standard and machine-readable output parsing. |
| [Docker](Docker/) | Typed wrapper for Docker CLI. Cobra parser, GitHub tags version collector. |
| [DockerCompose](DockerCompose/) | Typed wrapper for Docker Compose V2. 57 versions scraped (2.20.0--5.1.0), 37 command classes. Cobra parser, pipelined Phase 1/Phase 2 scraping via `Channel<string>`. |
| [Podman](Podman/) | Typed wrapper for Podman. 55 versions scraped (4.1.1--5.8.0), 374 generated source files. Custom Cobra-aware `PodmanHelpParser` with type hints (`string`, `int`, `uint`, `stringArray`). GitHub releases version collector. |
| [PodmanCompose](PodmanCompose/) | Typed wrapper for Podman Compose. 37 commands covering lifecycle, inspection, build, deploy, execution. Argparse parser, GitHub releases version collector. |
| [GitLab.Cli](GitLab.Cli/) | Typed wrapper for glab (GitLab CLI). 60 versions scraped (1.47.0--1.89.0). Custom `GlabHelpParser` for ALL-CAPS Cobra headers. GitLab API v4 version collector (`GITLAB_TOKEN` supported). |
| [Git](Git/) | Typed wrapper for Git CLI. 134 versions scraped (2.30.0--2.53.0), 422 generated source files. Custom `GitHelpParser` handling 3 distinct help modes. Build-from-source Alpine pipeline. |

### Infrastructure & Configuration

| Project | Description |
|---------|-------------|
| [Traefik](Traefik/) | Strongly-typed .NET configuration for Traefik reverse proxy, generated from official JSON schemas. Two-tier configuration (`TraefikStaticConfig` + `TraefikDynamicConfig`), fluent builders, YAML round-trip serialization. |
| [Vos](Vos/) | Virtual machine orchestration suite with CLI, Vagrant/FileSystem/PowerShell backends. HCL2-first design with Packer.Bundle integration. |
| [Packer.Alpine](Packer.Alpine/) | Packer templates and configuration for Alpine Linux VM images. |
| [Packer.Alpine.DockerHost](Packer.Alpine.DockerHost/) | Packer templates for Alpine-based Docker host images. |
| [Vos.Alpine](Vos.Alpine/) | Vos orchestration configurations for Alpine Linux VMs. |
| [Vos.Alpine.DockerHost](Vos.Alpine.DockerHost/) | Vos orchestration for Alpine-based Docker host environments. |

### Quality & Tooling

| Project | Description |
|---------|-------------|
| [QualityGate](QualityGate/) | Test runner, code coverage analyzer, and mutation testing orchestrator. CLI tool (`quality-gate test`) that runs tests, collects coverage, and produces quality reports. 4 SOLID interfaces (`ISolutionLoader`, `ICoverageReportParser`, `IMutationReportParser`, `IReportWriter`). 256 tests, 100% line/branch coverage, quality score 1.0. See [QUALITY-GATE.md](QualityGate/QUALITY-GATE.md). |

### Industrial Automation

| Project | Description |
|---------|-------------|
| [IEC61499](IEC61499/) | IEC 61499 reference implementation -- strongly-typed function blocks (`IBasicFunctionBlock<6 params>`), ECC, ports, builders. Reference code only. |
| [IEC61499.2](IEC61499.2/) | Clean-room IEC 61499 platform -- source-generated AOT function blocks + VSCode IDE extensions (12 planned: FB Type Designer, ECC Editor, Network Editor, Topology Manager, Library Manager, Language Server, Deployment Manager, Security Manager, Runtime Monitor, Debugger, Diagnostics, Simulation). Inspired by EcoStruxure Automation Expert. |
| [IFC61499](IFC61499/) | IEC 61499 platform variant -- same architecture as IEC61499.2 with alternative naming convention. |

### Applications

| Project | Description |
|---------|-------------|
| [Doc2Pdf](Doc2Pdf/) | Library and CLI for converting documents (DOCX, XLSX, PPTX, RTF, TXT) to PDF with high-fidelity formatting preservation. |
| [DockAi](DockAi/) | AI-powered document indexing, analysis, and search with multi-provider LLM support (Claude, OpenAI, Ollama). Web API for entity extraction and ontology generation, Blazor viewer for document rendering. |
| [HttpClient](HttpClient/) | HTTP client library. |
| [Alpine.Version](Alpine.Version/) | Alpine Linux version utilities. |
| [AttaQwant](AttaQwant/) | Qwant search forensic investigation tool. |
| [HomeLab](HomeLab/) | Home lab infrastructure configuration and documentation. |

## Dependency Graph

```
Dsl (M3 metamodel)
  ├── Ddd (DDD DSL)
  │     └── Diem (CMF — Content, Admin, Pages, Workflow)
  └── Requirements (Requirements DSL)

Result
  └── Builder (uses Result<T> for validation)
  │     └── BinaryWrapper (uses Builder for fluent command builders)
  │           ├── Vagrant
  │           ├── Packer
  │           ├── Docker
  │           ├── DockerCompose
  │           ├── Podman
  │           ├── PodmanCompose
  │           ├── GitLab.Cli
  │           └── Git
  ├── FiniteStateMachine (uses Result<Transition<TState>>)
  ├── Ddd (uses Result for invariants)
  └── Traefik (uses Builder + Result)

Wrapper.Versioning ── BinaryWrapper.Design, DockerCompose.Design, Traefik.Design

QualityGate          (standalone)
Doc2Pdf              (standalone)
DockAi               (standalone)
IEC61499             (standalone, reference code)
IEC61499.2           (standalone, clean-room platform)
IFC61499             (standalone, platform variant)
```

## Build Configuration

**SDK:** .NET 10.0.100 (`global.json` with `rollForward: latestFeature`)

**Shared build settings** (`Directory.Build.props`):
- `LangVersion`: latest
- `Nullable`: enable
- `TreatWarningsAsErrors`: true
- `EnforceCodeStyleInBuild`: true
- `ManagePackageVersionsCentrally`: true

**Central Package Management** (`Directory.Packages.props`): all NuGet versions defined centrally. Individual `.csproj` files use `<PackageReference>` without `Version=`.

**Quality Gate** (`quality-gate.yml`): root-level gates with permissive thresholds to accommodate source-generated code. Per-project `quality-gate.yml` files define stricter thresholds.

**Tool manifest** (`dotnet-tools.json`): `quality-gate` CLI tool (v1.0.0).

## Test Summary

| Project | Tests | Coverage |
|---------|------:|----------|
| Result | 47 | 100% branch |
| Builder | 24 | |
| FiniteStateMachine | 92 | |
| Dsl | 9 | |
| Ddd | 16 | |
| Requirements | 10 | |
| Diem | 81 | |
| BinaryWrapper | 270+ | |
| Wrapper.Versioning | 142 | score 1.0 |
| QualityGate | 256 | 100% line/branch, score 1.0 |
| Git | 15 | |
| **Total** | **960+** | |
