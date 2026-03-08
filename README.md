# FrenchExDev

Monorepo for FrenchExDev libraries, tools, and developer infrastructure.

## Repository layout

```
FrenchExDev_i2/
├── Net/FrenchExDev/          .NET libraries, source generators, and CLI tools
├── PoSh/FrenchExDev/         PowerShell modules and developer shell profile
├── Skills/Net/               Claude Code skill definitions for .NET projects
└── Doc/                      Cross-cutting documentation and media assets
```

## Net -- .NET projects

All .NET projects target **.NET 10** and use **Central Package Management** (`Directory.Packages.props`).

Full details: [Net/FrenchExDev/README.md](Net/FrenchExDev/README.md)

| Project | Description |
|---------|-------------|
| [Result](Net/FrenchExDev/Result/) | Immutable Result types (`Result`, `Result<T>`, `Result<T, TError>`) with composable Map, Bind, Recover extensions. |
| [Builder](Net/FrenchExDev/Builder/) | Source-generator-powered async object construction with validation and circular graph detection. |
| [BinaryWrapper](Net/FrenchExDev/BinaryWrapper/) | Type-safe .NET wrappers for CLI binaries, generated from scraped help text via Roslyn source generator. |
| [Vagrant](Net/FrenchExDev/Vagrant/) | Typed HashiCorp Vagrant wrapper built on BinaryWrapper (versions 2.4.3--2.4.9). |
| [Packer](Net/FrenchExDev/Packer/) | Typed HashiCorp Packer wrapper built on BinaryWrapper. |
| [Doc2Pdf](Net/FrenchExDev/Doc2Pdf/) | Library and CLI for converting DOCX, XLSX, PPTX, RTF, TXT to PDF. |
| [DockAi](Net/FrenchExDev/DockAi/) | AI-powered document indexing and search with Claude, OpenAI, and Ollama providers. |
| [Docker](Net/FrenchExDev/Docker/) | Typed Docker CLI wrapper (planned). |
| [Podman](Net/FrenchExDev/Podman/) | Typed Podman CLI wrapper (planned). |

## PoSh -- PowerShell

| Module | Description |
|--------|-------------|
| [DevPoSh](PoSh/FrenchExDev/DevPoSh/) | Developer shell profile -- VS Code integration, UTF-8 encoding, logging, module auto-loading. |
| [Claude](PoSh/FrenchExDev/Claude/) | Claude Code VM management utilities (`Restart-ClaudeVm`, `Reset-ClaudeVm`). |

## Skills

| Skill | Description |
|-------|-------------|
| [Net/Documentation](Skills/Net/Documentation/) | Claude Code skill for generating standardized project documentation (architecture, how-to, README). |

## Doc

| Document | Description |
|----------|-------------|
| [Update-Packages](Doc/Net/Update-Packages.md) | Documentation for the NuGet package updater script with live table UI. |

## Tools

| Tool | Location | Description |
|------|----------|-------------|
| `Update-Packages.ps1` | [Net/FrenchExDev/](Net/FrenchExDev/Update-Packages.ps1) | Interactive NuGet updater with parallel fetching, animated spinners, and selective apply. [Docs](Doc/Net/Update-Packages.md) |
| `Run-Coverage.ps1` | [Net/FrenchExDev/Result/](Net/FrenchExDev/Result/Run-Coverage.ps1) | Code coverage runner for the Result test suite. |

## Workspace

Open `FrenchExDev_i2.code-workspace` in VS Code for a multi-root workspace with three folders:

- **Root** -- repository root
- **Net** -- .NET projects (`Net/FrenchExDev/`)
- **PoSh** -- PowerShell modules (`PoSh/FrenchExDev/`)

## Requirements

- .NET 10.0 (preview)
- PowerShell 5.1+ or 7+
- For BinaryWrapper scraping: podman or docker
