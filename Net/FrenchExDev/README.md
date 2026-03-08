# FrenchExDev.Net

.NET libraries, source generators, and tools built on .NET 10. All projects use Central Package Management via `Directory.Packages.props`.

## Projects

| Project | Description |
|---------|-------------|
| [Result](Result/) | Lightweight, immutable Result types (`Result`, `Result<T>`, `Result<T, TError>`) replacing exceptions and null with explicit, composable success/failure values. Sealed records with Map, Bind, Recover, Tap, and Ensure extensions. |
| [Builder](Builder/) | Source-generator-powered async object construction framework. Automates validation, thread-safe building, and circular graph detection for domain objects that require async operations before construction. |
| [BinaryWrapper](BinaryWrapper/) | Type-safe .NET wrappers for CLI binaries, generated from help text. Scrapes `--help` output into JSON, then a Roslyn source generator produces command classes, fluent builders, and typed clients with multi-version support. |
| [Vagrant](Vagrant/) | Strongly-typed wrapper for HashiCorp Vagrant built on BinaryWrapper. Covers all Vagrant commands (versions 2.4.3--2.4.9) with structured output parsing, event streaming, and result collection. |
| [Packer](Packer/) | Typed C# wrapper for HashiCorp Packer built on BinaryWrapper. Provides fluent API for build, validate, init, fmt, inspect, and plugins commands with standard and machine-readable output parsing. |
| [Doc2Pdf](Doc2Pdf/) | .NET library and CLI tool for converting document formats (DOCX, XLSX, PPTX, RTF, TXT) to PDF. Supports batch processing with high-fidelity formatting, image, and layout preservation. |
| [DockAi](DockAi/) | AI-powered document indexing, analysis, and search system with multi-provider LLM support (Claude, OpenAI, Ollama). Includes a web API for entity extraction and ontology generation, and a Blazor viewer for document rendering. |
| [Docker](Docker/) | Typed wrapper for Docker CLI built on BinaryWrapper. Currently a stub -- project structure in place, implementation pending. |
| [Podman](Podman/) | Typed wrapper for Podman CLI built on BinaryWrapper. Currently a stub -- project structure in place, implementation pending. |

## Dependency graph

```
Result
  └── Builder (uses Result<T> for validation)
        └── BinaryWrapper (uses Builder for fluent command builders)
              ├── Vagrant
              ├── Packer
              ├── Docker (planned)
              └── Podman (planned)

Doc2Pdf          (standalone)
DockAi           (standalone)
```

## Central Package Management

All NuGet package versions are defined in [`Directory.Packages.props`](Directory.Packages.props). Individual `.csproj` files use `<PackageReference>` without `Version=`.
