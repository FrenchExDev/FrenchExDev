# FrenchExDev

Monorepo for FrenchExDev libraries, tools, and developer infrastructure — spanning .NET libraries, Roslyn source generators, PowerShell automation modules, and homelab orchestration.

## Repository layout

```
FrenchExDev_i2/
├── Net/FrenchExDev/          .NET libraries, source generators, and CLI tools
├── PoSh/FrenchExDev/         PowerShell modules (43 modules)
├── Skills/Net/               Claude Code skill definitions
├── Doc/                      Cross-cutting documentation
└── Plans/                    Project plans
```

---

## Net -- .NET projects

All .NET projects target **.NET 10** and use **Central Package Management** (`Directory.Packages.props`).
Central solution: [`FrenchExDev.Net.slnx`](Net/FrenchExDev/FrenchExDev.Net.slnx). Each project also has its own standalone `.slnx`.

Full details: [Net/FrenchExDev/README.md](Net/FrenchExDev/README.md)

### Foundation libraries

| Project | Description |
|---------|-------------|
| [Result](Net/FrenchExDev/Result/) | Immutable `Result`, `Result<T>`, `Result<T, TError>` with composable Map, Bind, Recover extensions. 47 tests, 100% coverage. |
| [Builder](Net/FrenchExDev/Builder/) | Source-generator-powered async builder pattern with validation, circular graph detection, and multiple instantiation strategies (`init`, `ctor`, `factory`, `custom`). |
| [FiniteStateMachine](Net/FrenchExDev/FiniteStateMachine/) | Hierarchical FSM library with 3 tiers, 2 source generators, regions. 92 tests. |
| [QualityGate](Net/FrenchExDev/QualityGate/) | Custom test runner + coverage analyzer + mutation testing orchestrator. 256 tests, 100% coverage. |

### BinaryWrapper ecosystem

Type-safe .NET wrappers for CLI binaries, generated from scraped `--help` text via Roslyn source generators.

| Project | Description | Versions |
|---------|-------------|----------|
| [BinaryWrapper](Net/FrenchExDev/BinaryWrapper/) | Core framework — scraping, parsing, and source generation infrastructure. | -- |
| [Docker](Net/FrenchExDev/Docker/) | Typed Docker CLI wrapper. | 77 |
| [DockerCompose](Net/FrenchExDev/DockerCompose/) | Docker Compose wrapper with schema-driven Bundle source generation (32 compose-spec schemas). | -- |
| [Vagrant](Net/FrenchExDev/Vagrant/) | HashiCorp Vagrant wrapper. | 7 (2.4.3--2.4.9) |
| [Podman](Net/FrenchExDev/Podman/) | Podman CLI wrapper. 374 generated source files. | 55 (4.1.1--5.8.0) |
| [PodmanCompose](Net/FrenchExDev/PodmanCompose/) | Podman Compose wrapper. | -- |
| [Packer](Net/FrenchExDev/Packer/) | HashiCorp Packer wrapper with HCL2-first Bundle system + Vos orchestration. | 21 |
| [GitLab.Cli](Net/FrenchExDev/GitLab.Cli/) | GitLab `glab` CLI wrapper. | 60 (1.47.0+) |
| [Git](Net/FrenchExDev/Git/) | Git CLI wrapper with custom parser and build-from-source pipeline. | -- |

### Applications and tools

| Project | Description |
|---------|-------------|
| [Doc2Pdf](Net/FrenchExDev/Doc2Pdf/) | Library and CLI for converting DOCX, XLSX, PPTX, RTF, TXT to PDF. |
| [DockAi](Net/FrenchExDev/DockAi/) | AI-powered document indexing and search with Claude, OpenAI, and Ollama providers. |
| [HttpClient](Net/FrenchExDev/HttpClient/) | HTTP client library. |
| [Alpine.Version](Net/FrenchExDev/Alpine.Version/) | Alpine Linux version utilities. |
| [AttaQwant](Net/FrenchExDev/AttaQwant/) | Qwant search forensics. |

### Domain modeling (Diem CMF)

Content Management Framework — each DSL is its own project, composed by Diem.

| Project | Description |
|---------|-------------|
| [Diem](Net/FrenchExDev/Diem/) | Main CMF composition layer + CLI. |
| [Dsl](Net/FrenchExDev/Dsl/) | Domain-specific language (absorbs M3/Meta). |
| [Requirements](Net/FrenchExDev/Requirements/) | Requirements management DSL. |
| [Ddd](Net/FrenchExDev/Ddd/) | Domain-driven design patterns. |

### Infrastructure libraries

| Project | Description |
|---------|-------------|
| [Vos](Net/FrenchExDev/Vos/) | VM orchestration framework. |
| [Vos.Alpine](Net/FrenchExDev/Vos.Alpine/) | Alpine Linux Vos variants. |
| [Vos.Alpine.DockerHost](Net/FrenchExDev/Vos.Alpine.DockerHost/) | Alpine Docker host Vos config. |
| [Packer.Alpine](Net/FrenchExDev/Packer.Alpine/) | Alpine image building. |
| [Packer.Alpine.DockerHost](Net/FrenchExDev/Packer.Alpine.DockerHost/) | Alpine Docker host images. |
| [Traefik](Net/FrenchExDev/Traefik/) | Traefik reverse proxy configuration. |
| [HomeLab](Net/FrenchExDev/HomeLab/) | Home lab configuration. |
| [Wrapper.Versioning](Net/FrenchExDev/Wrapper.Versioning/) | Version management for BinaryWrapper. |
| [IEC61499](Net/FrenchExDev/IEC61499/) | Industrial automation (IEC 61499). |

---

## PoSh -- PowerShell modules

43 PowerShell modules for developer tooling and infrastructure automation.

### Developer environment

| Module | Description |
|--------|-------------|
| [DevPoSh__](PoSh/FrenchExDev/DevPoSh__/) | Developer shell profile -- VS Code integration, UTF-8, logging, module auto-loading. |
| [Claude](PoSh/FrenchExDev/Claude/) | Claude Code VM management (`Restart-ClaudeVm`, `Reset-ClaudeVm`). |
| [FrenchExDev.DevPoSh.PoSh](PoSh/FrenchExDev/FrenchExDev.DevPoSh.PoSh/) | Developer PowerShell helpers. |
| [FrenchExDev.DevBash.PoSh](PoSh/FrenchExDev/FrenchExDev.DevBash.PoSh/) | Bash integration helpers. |
| [FrenchExDev.DevSetup.PoSh](PoSh/FrenchExDev/FrenchExDev.DevSetup.PoSh/) | Development environment setup. |
| [FrenchExDev.DevNetwork.PoSh](PoSh/FrenchExDev/FrenchExDev.DevNetwork.PoSh/) | Network configuration. |
| [FrenchExDev.MyRoot.VsCode.PoSh](PoSh/FrenchExDev/FrenchExDev.MyRoot.VsCode.PoSh/) | VS Code workspace configuration. |
| [FrenchExDev.VsCode.PoSh](PoSh/FrenchExDev/FrenchExDev.VsCode.PoSh/) | VS Code utilities. |

### .NET and version control

| Module | Description |
|--------|-------------|
| [FrenchExDev.Dotnet.PoSh](PoSh/FrenchExDev/FrenchExDev.Dotnet.PoSh/) | .NET project/solution helpers (Get-DotnetProject, Clean-DotnetSolution, Publish-DotnetSolutionPackage). |
| [FrenchExDev.Net.PoSh](PoSh/FrenchExDev/FrenchExDev.Net.PoSh/) | .NET-specific utilities. |
| [FrenchExDev.Git.PoSh](PoSh/FrenchExDev/FrenchExDev.Git.PoSh/) | Git operations. |
| [FrenchExDev.GitHub.PoSh](PoSh/FrenchExDev/FrenchExDev.GitHub.PoSh/) | GitHub integration. |

### Container and VM orchestration

| Module | Description |
|--------|-------------|
| [FrenchExDev.Docker.PoSh](PoSh/FrenchExDev/FrenchExDev.Docker.PoSh/) | Docker commands. |
| [FrenchExDev.DockerCompose.PoSh](PoSh/FrenchExDev/FrenchExDev.DockerCompose.PoSh/) | Docker Compose helpers. |
| [FrenchExDev.Vagrant.PoSh](PoSh/FrenchExDev/FrenchExDev.Vagrant.PoSh/) | Vagrant automation. |
| [FrenchExDev.Vagrant.Catalog.PoSh](PoSh/FrenchExDev/FrenchExDev.Vagrant.Catalog.PoSh/) | Vagrant box catalog management. |
| [FrenchExDev.VirtualBox.PoSh](PoSh/FrenchExDev/FrenchExDev.VirtualBox.PoSh/) | VirtualBox management. |
| [FrenchExDev.Kubernetes.PoSh](PoSh/FrenchExDev/FrenchExDev.Kubernetes.PoSh/) | Kubernetes operations. |

### Packer image building

| Module | Description |
|--------|-------------|
| [FrenchExDev.Packer.PoSh](PoSh/FrenchExDev/FrenchExDev.Packer.PoSh/) | Packer API wrapper with HCL2 generation. |
| [FrenchExDev.Packer.Alpine.PoSh](PoSh/FrenchExDev/FrenchExDev.Packer.Alpine.PoSh/) | Alpine Packer image building. |
| [FrenchExDev.Packer.Alpine.Docker.PoSh](PoSh/FrenchExDev/FrenchExDev.Packer.Alpine.Docker.PoSh/) | Alpine Docker images via Packer. |
| [FrenchExDev.Packer.Alpine.Kubernetes.PoSh](PoSh/FrenchExDev/FrenchExDev.Packer.Alpine.Kubernetes.PoSh/) | Alpine Kubernetes images via Packer. |
| [FrenchExDev.Packer.Debian.PoSh](PoSh/FrenchExDev/FrenchExDev.Packer.Debian.PoSh/) | Debian Packer images. |

### Vos VM orchestration

| Module | Description |
|--------|-------------|
| [FrenchExDev.Vos.PoSh](PoSh/FrenchExDev/FrenchExDev.Vos.PoSh/) | Vos VM orchestration with config builders. |
| [FrenchExDev.Vos.Alpine.PoSh](PoSh/FrenchExDev/FrenchExDev.Vos.Alpine.PoSh/) | Alpine Vos VM variants. |
| [FrenchExDev.Vos.Alpine.Docker.PoSh](PoSh/FrenchExDev/FrenchExDev.Vos.Alpine.Docker.PoSh/) | Alpine Docker Vos. |
| [FrenchExDev.Vos.Alpine.Kubernetes.PoSh](PoSh/FrenchExDev/FrenchExDev.Vos.Alpine.Kubernetes.PoSh/) | Alpine Kubernetes cluster orchestration. |

### Services and infrastructure

| Module | Description |
|--------|-------------|
| [FrenchExDev.MyInfra.PoSh](PoSh/FrenchExDev/FrenchExDev.MyInfra.PoSh/) | Master infrastructure orchestration (configure, build, deploy). |
| [FrenchExDev.LocalHost.HomeLab.PoSh](PoSh/FrenchExDev/FrenchExDev.LocalHost.HomeLab.PoSh/) | Home lab localhost configuration. |
| [FrenchExDev.Alpine.PoSh](PoSh/FrenchExDev/FrenchExDev.Alpine.PoSh/) | Alpine Linux utilities. |
| [FrenchExDev.Traefik.PoSh](PoSh/FrenchExDev/FrenchExDev.Traefik.PoSh/) | Traefik reverse proxy. |
| [FrenchExDev.Traefik.DockerCompose.PoSh](PoSh/FrenchExDev/FrenchExDev.Traefik.DockerCompose.PoSh/) | Traefik via Docker Compose. |
| [FrenchExDev.Keycloak.PoSh](PoSh/FrenchExDev/FrenchExDev.Keycloak.PoSh/) | Keycloak identity management. |
| [FrenchExDev.Keycloak.DockerCompose.PoSh](PoSh/FrenchExDev/FrenchExDev.Keycloak.DockerCompose.PoSh/) | Keycloak via Docker Compose. |
| [FrenchExDev.GitLab.DockerCompose.PoSh](PoSh/FrenchExDev/FrenchExDev.GitLab.DockerCompose.PoSh/) | GitLab via Docker Compose. |
| [FrenchExDev.PiHole.PoSh](PoSh/FrenchExDev/FrenchExDev.PiHole.PoSh/) | Pi-hole DNS management. |
| [FrenchExDev.PiHole.DockerCompose.PoSh](PoSh/FrenchExDev/FrenchExDev.PiHole.DockerCompose.PoSh/) | Pi-hole via Docker Compose. |
| [FrenchExDev.Sonatype.Nexus.DockerCompose.PoSh](PoSh/FrenchExDev/FrenchExDev.Sonatype.Nexus.DockerCompose.PoSh/) | Sonatype Nexus via Docker Compose. |

### System and networking

| Module | Description |
|--------|-------------|
| [FrenchExDev.System.PoSh](PoSh/FrenchExDev/FrenchExDev.System.PoSh/) | System utilities. |
| [FrenchExDev.Env.PoSh](PoSh/FrenchExDev/FrenchExDev.Env.PoSh/) | Environment variable management. |
| [FrenchExDev.EtcHosts.PoSh](PoSh/FrenchExDev/FrenchExDev.EtcHosts.PoSh/) | `/etc/hosts` file management. |
| [FrenchExDev.SshConfig.PoSh](PoSh/FrenchExDev/FrenchExDev.SshConfig.PoSh/) | SSH configuration management. |
| [FrenchExDev.MkCert.PoSh](PoSh/FrenchExDev/FrenchExDev.MkCert.PoSh/) | Local TLS certificates via mkcert. |

---

## Scripts and tools

### .NET tooling

| Script | Location | Description |
|--------|----------|-------------|
| `Update-Packages.ps1` | [Net/FrenchExDev/](Net/FrenchExDev/Update-Packages.ps1) | Interactive NuGet updater with parallel fetching, animated spinners, and selective apply. Supports `-Apply`, `-IncludePrerelease`, `-ParallelMax`. [Docs](Doc/Net/Update-Packages.md) |
| `coverage-report.ps1` | [Net/FrenchExDev/](Net/FrenchExDev/coverage-report.ps1) | Parses `coverage.cobertura.xml` and displays per-class coverage summary. |
| `Run-Coverage.ps1` | [Net/FrenchExDev/Result/](Net/FrenchExDev/Result/Run-Coverage.ps1) | Test runner with Coverlet coverage, HTML reports, and watch mode (`-Watch`, `-OpenReport`). |

### Infrastructure orchestration

| Script | Location | Description |
|--------|----------|-------------|
| `configure.ps1` | [PoSh/.../MyInfra](PoSh/FrenchExDev/FrenchExDev.MyInfra.PoSh/configure.ps1) | Master configuration: chains Network, Packer, Vos, Dos, SSL setup. Params: Acme, Domain, Author, Alpine/Box versions, LogLevel, TimeZone. |
| `Build-Packer.ps1` | [PoSh/.../MyInfra](PoSh/FrenchExDev/FrenchExDev.MyInfra.PoSh/Build-Packer.ps1) | Triggers Packer image builds. |
| `Configure-JumpBox.ps1` | [PoSh/.../MyInfra](PoSh/FrenchExDev/FrenchExDev.MyInfra.PoSh/Configure-JumpBox.ps1) | JumpBox (entrypoint) machine configuration. |
| `Operate-VosAlpineKubernetes.ps1` | [PoSh/.../MyInfra](PoSh/FrenchExDev/FrenchExDev.MyInfra.PoSh/Operate-VosAlpineKubernetes.ps1) | Alpine-based Kubernetes cluster operations. |

### Repository management

| Script | Location | Description |
|--------|----------|-------------|
| `clone-posh-repos.ps1` | [Root](clone-posh-repos.ps1) | Clones all `FrenchExDev.*.PoSh` repositories from GitHub into `PoSh/FrenchExDev/`. |

---

## Quality and testing

- **QualityGate**: custom test/coverage/mutation orchestrator -- `dotnet run --project QualityGate/src/FrenchExDev.Net.QualityGate.Cli -- test`
- **Coverage**: Coverlet + reportgenerator integration via `Run-Coverage.ps1`
- **Build config**: `TreatWarningsAsErrors`, `EnforceCodeStyleInBuild`, nullable enabled, latest C# language version

---

## Skills (Claude Code)

| Skill | Description |
|-------|-------------|
| [Documentation](Skills/Net/Documentation/) | Generates standardized project documentation (ARCHITECTURE, HOW-TO, README). |
| [Programming/CQRS](Skills/Net/Programming/CQRS/) | CQRS pattern guidance. |
| [Programming/DDD](Skills/Net/Programming/DDD/) | Domain-driven design patterns. |
| [Programming/DESIGN-PHASED-PROJECT](Skills/Net/Programming/DESIGN-PHASED-PROJECT/) | Phased project design methodology. |
| [Programming/QUALITY-GATES](Skills/Net/Programming/QUALITY-GATES/) | Quality gate standards. |

---

## Documentation

Each .NET project follows a standard documentation structure:

```
<Project>/
├── README.md
└── doc/
    ├── ARCHITECTURE.md
    ├── HOW-TO.md
    └── PHILOSOPHY.md
```

Cross-cutting docs live under [Doc/](Doc/):

| Document | Description |
|----------|-------------|
| [Net/Update-Packages.md](Doc/Net/Update-Packages.md) | NuGet package updater with live table UI. |
| [Net/README.md](Doc/Net/README.md) | .NET architecture overview, package details, build infrastructure. |

---

## Workspace

Open [`FrenchExDev_i2.code-workspace`](FrenchExDev_i2.code-workspace) in VS Code for a multi-root workspace:

- **Root** -- repository root
- **Net** -- .NET projects (`Net/FrenchExDev/`)
- **PoSh** -- PowerShell modules (`PoSh/FrenchExDev/`)

---

## Requirements

- **.NET 10.0** (SDK 10.0.100+ with `latestFeature` roll-forward)
- **PowerShell** 5.1+ or 7+
- **Podman or Docker** for BinaryWrapper scraping
- **VS Code** (recommended) with the multi-root workspace
