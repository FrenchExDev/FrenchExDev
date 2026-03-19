# FrenchExDev.Net

.NET libraries, source generators, and tools built on .NET 10. All projects use Central Package Management via `Directory.Packages.props`.

## Projects

### Foundation

| Project | Description |
|---------|-------------|
| [Result](Result/) | Lightweight, immutable Result types (`Result`, `Result<T>`, `Result<T, TError>`) replacing exceptions and null with explicit, composable success/failure values. Sealed records with Map, Bind, Recover, Tap, and Ensure extensions. |
| [Builder](Builder/) | Source-generator-powered async object construction framework. Automates validation, thread-safe building, and circular graph detection for domain objects that require async operations before construction. |

### BinaryWrapper Framework

| Project | Description |
|---------|-------------|
| [BinaryWrapper](BinaryWrapper/) | Type-safe .NET wrappers for CLI binaries, generated from help text. Scrapes `--help` output into JSON, then a Roslyn source generator produces command classes, fluent builders, and typed clients with multi-version support. |
| [Vagrant](Vagrant/) | Strongly-typed wrapper for HashiCorp Vagrant built on BinaryWrapper. Covers all Vagrant commands (versions 2.4.3--2.4.9) with structured output parsing, event streaming, and result collection. |
| [Packer](Packer/) | Typed C# wrapper for HashiCorp Packer built on BinaryWrapper. Provides fluent API for build, validate, init, fmt, inspect, and plugins commands with standard and machine-readable output parsing. |
| [Docker](Docker/) | Typed wrapper for Docker CLI built on BinaryWrapper. Cobra parser, GitHub tags version collector. |
| [DockerCompose](DockerCompose/) | Typed wrapper for Docker Compose CLI built on BinaryWrapper. Cobra parser, GitHub releases version collector. |
| [Podman](Podman/) | Typed wrapper for Podman CLI built on BinaryWrapper. 55 versions scraped (4.1.1--5.8.0), 374 generated source files. Cobra parser, GitHub releases version collector. |
| [PodmanCompose](PodmanCompose/) | Typed wrapper for Podman Compose built on BinaryWrapper. Argparse parser, GitHub releases version collector. |
| [GitLab.Cli](GitLab.Cli/) | Typed wrapper for glab (GitLab CLI) built on BinaryWrapper. 60 versions scraped (1.47.0--1.89.0). Custom GlabHelpParser, GitLab API v4 version collector. |

### Quality & Tooling

| Project | Description |
|---------|-------------|
| [QualityGate](QualityGate/) | Test runner, code coverage analyzer, and mutation testing orchestrator. CLI tool that runs tests, collects coverage, and produces quality reports. 256 tests, 100% line/branch coverage. |

### Applications

| Project | Description |
|---------|-------------|
| [Doc2Pdf](Doc2Pdf/) | .NET library and CLI tool for converting document formats (DOCX, XLSX, PPTX, RTF, TXT) to PDF. Supports batch processing with high-fidelity formatting, image, and layout preservation. |
| [DockAi](DockAi/) | AI-powered document indexing, analysis, and search system with multi-provider LLM support (Claude, OpenAI, Ollama). Includes a web API for entity extraction and ontology generation, and a Blazor viewer for document rendering. |
| [HttpClient](HttpClient/) | HTTP client library. |
| [Alpine.Version](Alpine.Version/) | Alpine Linux version utilities. |
| [AttaQwant](AttaQwant/) | Qwant search forensic investigation. |

## Dependency graph

```
Result
  └── Builder (uses Result<T> for validation)
        └── BinaryWrapper (uses Builder for fluent command builders)
              ├── Vagrant
              ├── Packer
              ├── Docker
              ├── DockerCompose
              ├── Podman
              ├── PodmanCompose
              └── GitLab.Cli

QualityGate      (standalone — runs tests & coverage on any solution)
Doc2Pdf          (standalone)
DockAi           (standalone)
```

## Central Package Management

All NuGet package versions are defined in [`Directory.Packages.props`](Directory.Packages.props). Individual `.csproj` files use `<PackageReference>` without `Version=`.
