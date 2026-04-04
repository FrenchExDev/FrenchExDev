# HomeLab

CLI-first infrastructure configurator for standing up local lab environments. Orchestrates Packer image builds, Vos VM provisioning, Docker Compose service stacks, TLS certificates, DNS entries, and GitLab configuration -- all from a single `config-homelab.yaml`.

**Status: design/specification phase** -- see [PLAN.md](doc/PLAN.md) for the full implementation plan. No source code yet.

## Design Principles

1. **CLI-first** -- every action is a CLI command; E2E tests = CLI invocations
2. **Schema-validated** -- JSON schemas generated from C# models; VSCode intellisense for YAML editing
3. **Git-composable** -- shared configs via git submodules; personal overrides layered on top

## CLI Surface (9 command groups)

| Group | Purpose |
|-------|---------|
| `homelab init` | Bootstrap project: scaffold `config-homelab.yaml` + VSCode settings |
| `homelab validate` | Validate all configs against JSON schemas |
| `homelab packer` | Generate + build Packer images (Alpine, DockerHost) |
| `homelab box` | Vagrant box management (add, publish to registry) |
| `homelab vos` | VM orchestration (up, halt, destroy, status, ssh) |
| `homelab compose` | Docker Compose service stacks (init, deploy, down) |
| `homelab dns` | DNS management (hosts file + PiHole API) |
| `homelab tls` | TLS certificate generation and trust (native + mkcert) |
| `homelab gitlab` | GitLab configuration + runner registration |

## E2E Pipeline

```bash
homelab init --name test-lab
homelab packer init && homelab packer build
homelab box add --local
homelab vos init && homelab vos up
homelab dns add gitlab.frenchexdev.lab 192.168.56.10
homelab tls init --provider native && homelab tls install
homelab compose init --traefik --gitlab && homelab compose deploy
homelab gitlab configure && homelab gitlab runner register
```

## Ecosystem Dependencies

| Project | Role in HomeLab |
|---------|----------------|
| Packer + Packer.Alpine | Image building (HCL2 generation) |
| Vos + Vos.Alpine.DockerHost | VM lifecycle (Vagrant orchestration) |
| Docker + DockerCompose | Container management on VMs |
| Traefik | Reverse proxy configuration |
| Git | Version control integration |
| GitLab.Cli | GitLab CLI operations |

## What's Missing (To Be Created)

| Gap | Planned Project |
|-----|----------------|
| Compose file contributors | `IComposeFileContributor` in DockerCompose.Bundle |
| Traefik compose service | `Traefik.DockerCompose` |
| GitLab compose service | `GitLab.DockerCompose` |
| HomeLab orchestrator + CLI | `HomeLab` lib + `HomeLab.Cli` |
| Hosts file manager | `EtcHosts` |
| TLS cert generation | `Tls` (native + mkcert providers) |
| Vagrant box registry | `Vagrant.Registry` |
| GitLab API client | `GitLab.Api.Bundle` |
| PiHole API client | `PiHole.Api.Bundle` |

## Documentation

- [ARCHITECTURE.md](doc/ARCHITECTURE.md) -- project decomposition, dependency graph, implementation phases
- [HOW-TO.md](doc/HOW-TO.md) -- planned CLI usage for each command group
- [PHILOSOPHY.md](doc/PHILOSOPHY.md) -- why CLI-first, why schema-validated, why git-composable
- [PLAN.md](doc/PLAN.md) -- full implementation plan with phases and verification steps
