# HomeLab -- Architecture

## 1. Overview

HomeLab is a meta-orchestrator that composes existing FrenchExDev infrastructure projects (Packer, Vos, Docker, DockerCompose, Traefik, Git, GitLab.Cli) into a single CLI for standing up complete local lab environments. It generates configs, builds images, provisions VMs, deploys services, configures DNS/TLS, and sets up GitLab CI.

**Status: design phase.** This document describes the planned architecture. See [PLAN.md](PLAN.md) for implementation details.

---

## 2. Planned Project Decomposition

```
HomeLab/
  src/
    FrenchExDev.Net.HomeLab/           Core lib: config, pipeline, schema generation
    FrenchExDev.Net.HomeLab.Cli/       System.CommandLine CLI
  test/
    FrenchExDev.Net.HomeLab.Tests/     Unit tests (artifact gen, config builder, YAML)
    FrenchExDev.Net.HomeLab.E2E/       E2E tests (full CLI pipeline)
```

Plus new supporting projects:

```
Traefik.DockerCompose/                 Traefik as Docker Compose service
GitLab.DockerCompose/                  GitLab as Docker Compose service
EtcHosts/                             Hosts file manager (local + PiHole)
Tls/                                  TLS cert generation (native + mkcert)
Vagrant.Registry/                     Self-hosted box registry
GitLab.Api.Bundle/                    GitLab REST API client
PiHole.Api.Bundle/                    PiHole REST API client
```

---

## 3. Dependency Graph

```
Layer 0 — Existing infrastructure (already implemented)
  Packer, Packer.Alpine, Packer.Alpine.DockerHost
  Vos, Vos.Alpine, Vos.Alpine.DockerHost
  Docker, DockerCompose, DockerCompose.Bundle
  Traefik, Traefik.Bundle
  Git, GitLab.Cli

Layer 1 — New compose contributors
  Traefik.DockerCompose  → DockerCompose.Bundle, Traefik.Bundle
  GitLab.DockerCompose   → DockerCompose.Bundle

Layer 2 — New tool wrappers
  EtcHosts               → (no deps)
  Tls                    → (no deps, optional mkcert binary)
  Vagrant.Registry       → Vagrant
  GitLab.Api.Bundle      → HttpClient
  PiHole.Api.Bundle      → HttpClient

Layer 3 — HomeLab core
  HomeLab                → All of the above
  HomeLab.Cli            → HomeLab, System.CommandLine
```

---

## 4. CLI Architecture

9 command groups, each mapping to a domain:

| Group | Underlying Project | Key Operations |
|-------|-------------------|----------------|
| `init` | HomeLab | Scaffold config-homelab.yaml + VSCode settings |
| `validate` | HomeLab | JSON schema validation of all configs |
| `packer` | Packer + Packer.Alpine | Generate HCL2, build images |
| `box` | Vagrant | Add/publish .box files |
| `vos` | Vos + Vos.Alpine.DockerHost | VM lifecycle (up/halt/destroy/ssh) |
| `compose` | DockerCompose.Bundle | Init/deploy/down service stacks |
| `dns` | EtcHosts + PiHole | Add/remove/list DNS entries |
| `tls` | Tls | Generate certs, install, trust |
| `gitlab` | GitLab.Api + GitLab.Cli | Configure server, register runners |

All config classes use `[Builder]` pattern → `Result<Reference<T>>`.

---

## 5. Configuration Model

### config-homelab.yaml (root)

```yaml
name: test-lab
acme:
  name: frenchexdev
  tld: lab                           # → gitlab.frenchexdev.lab
packer:
  distro: alpine
  version: "3.21"
  kind: dockerhost
  cpus: 4
  memory: 256
  disk_size: 20480
vos:
  box: frenchexdev/alpine-3.21-dockerhost
  memory: 2048
  cpus: 4
  subnet: "192.168.56"
  provider: virtualbox
compose:
  traefik: true
  gitlab: true
  gitlab_runner: true
  domain: frenchexdev.lab
tls:
  provider: native                   # native | mkcert
  domain: frenchexdev.lab
  ca_name: "HomeLab CA"
dns:
  provider: local                    # local | pihole
  pihole_url: ""
  pihole_token: ""
```

### JSON Schema Generation

HomeLab generates JSON schemas from C# config models at build time. These schemas are referenced in `.vscode/settings.json` for intellisense:

```json
{
  "yaml.schemas": {
    "./schemas/config-homelab.schema.json": "config-homelab.yaml"
  }
}
```

---

## 6. Implementation Phases

| Phase | Projects | Dependency |
|-------|----------|------------|
| **1: Foundation** | `IComposeFileContributor`, `Traefik.DockerCompose` | Parallel |
| **2: Core** | `GitLab.DockerCompose`, `HomeLab` lib + CLI, unit tests | Sequential |
| **3: Tool wrappers** | `EtcHosts`, `Tls`, `Git` (exists) | Parallel |
| **4: API clients** | `Vagrant.Registry`, `GitLab.Api.Bundle`, `PiHole.Api.Bundle` | Parallel |
| **5: E2E** | Full pipeline tests | After all above |

---

## 7. Git Composability

Shared configs via git submodules, personal overrides layered on top:

```
homelab/                        (your repo)
  config-homelab.yaml           (shared, committed)
  local/
    config-homelab-local.yaml   (gitignored, personal overrides)
  shared/                       (git submodule: team defaults)
```

Deep merge: local overrides shared overrides base. Same pattern as Vos `config-vos.yaml` + `local/config-vos-local.yaml`.
