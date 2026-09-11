# HomeLab C# Project — Implementation Plan

## Context

HomeLab is a **CLI-first Vos configurator** for standing up local infrastructure. It generates and manages: Packer images, Vos VM configs, Docker Compose service stacks, TLS certificates, DNS entries. Vos = Packer + Vagrant + data-driven Vagrantfile. Vos manages VirtualBox, Hyper-V, Parallels providers, networking, disks, and SSH.

**Three design principles:**
1. **CLI-first** — every action is a CLI command; E2E tests = CLI invocations
2. **Schema-validated** — JSON schemas generated from C# models; VSCode intellisense for YAML editing
3. **Git-composable** — shared configs via git submodules; personal overrides layered on top

Multiple instances coexist on one machine via VM/network isolation.

All config classes use `[Builder]` → `Result<Reference<T>>`. Pipeline returns `Result<T>`.

---

## CLI Surface (grouped by concern, like git)

```
homelab init [--name homelab]                            # create homelab project dir + config-homelab.yaml
             [--acme-name frenchexdev]                   #   organization name (used in box names, hostnames)
             [--acme-tld lab]                            #   TLD for internal DNS (gitlab.frenchexdev.lab)
             [--output ./]                               #   where to create the project
homelab validate [--config ./config-homelab.yaml]        # validate all configs against JSON schemas

homelab packer init [--distro alpine] [--version 3.21]   # generate packer .pkr.hcl files
                    [--flavor virt] [--kind dockerhost]   #   flavor=virt (Alpine ISO variant), kind=dockerhost
                    [--cpus 4] [--memory 256]             #   VirtualBox build VM resources (MB)
                    [--disk-size 20480]                   #   VM disk size (MB)
                    [--box-version 1.0.0]                 #   Vagrant box version tag
                    [--output ./packer]                   #   output directory for HCL2 files
homelab packer build [--output ./packer]                 # run packer build in output dir

homelab box add [--name frenchexdev/alpine-3.21-dockerhost]  # vagrant box add locally
                [--box-file ./packer/output-vagrant/*.box]    #   path to .box file from packer build
homelab box publish [--name frenchexdev/alpine-3.21-dockerhost]  # publish to Vagrant Registry
                    [--box-file ./packer/output-vagrant/*.box]
                    [--registry-url http://registry.frenchexdev.lab:8080]
                    [--version 1.0.0]

homelab vos init [--box frenchexdev/alpine-3.21-dockerhost]  # generate config-vos.yaml + Vagrantfile
                 [--instance-name main-01]               #   VM instance name
                 [--memory 2048] [--cpus 4]              #   VM runtime resources (MB)
                 [--private-subnet 192.168.56]           #   host-only network subnet (.0/24)
                 [--public-bridge ""]                     #   bridged adapter name (empty=none)
                 [--provider virtualbox]                  #   hypervisor: virtualbox|hyperv|parallels
                 [--output ./]                           #   where to write config-vos.yaml
homelab vos up [name] [--config ./config-vos.yaml]       # boot VM(s). name=instance name, omit=all
homelab vos halt [name] [--force]                        # stop VM(s)
                 [--config ./config-vos.yaml]
homelab vos destroy [name] [--force]                     # destroy VM(s) + disks
                    [--config ./config-vos.yaml]
homelab vos status [name]                                # show VM status (running/poweroff/not created)
                   [--config ./config-vos.yaml]
homelab vos ssh <name> [--config ./config-vos.yaml]      # interactive SSH into VM
homelab vos ssh-command <name> <cmd>                     # run command on VM, return output
                        [--config ./config-vos.yaml]

homelab compose init [--traefik true] [--gitlab true]    # generate docker-compose.yaml + traefik configs
                     [--gitlab-runner true]               #   which services to include
                     [--gitlab-image gitlab/gitlab-ce]    #   GitLab container image
                     [--gitlab-version latest]            #   GitLab image tag
                     [--traefik-image traefik]            #   Traefik container image
                     [--traefik-version latest]
                     [--domain frenchexdev.lab]           #   FQDN base for service routing
                     [--output ./docker-compose]          #   output directory
homelab compose deploy [name]                            # docker compose up -d on VM via DOCKER_HOST=tcp://{ip}:2375
                       [--config ./config-vos.yaml]      #   resolves VM IP from vos config
homelab compose down [name]                              # docker compose down on VM
                     [--config ./config-vos.yaml]

homelab dns add <hostname> <ip>                          # add entry to hosts file (or PiHole API if --pihole)
                [--pihole-url ""]                         #   PiHole API URL (empty=use hosts file)
                [--pihole-token ""]                       #   PiHole API token
homelab dns remove <hostname> [--pihole-url ""]          # remove entry
homelab dns list [--pihole-url ""]                       # list managed entries

homelab tls init [--provider native]                     # generate self-signed CA + domain certs
                 [--domain frenchexdev.lab]               #   wildcard cert for *.{domain}
                 [--ca-name "HomeLab CA"]                 #   CA common name
                 [--output ./data/certs]                  #   cert output directory
homelab tls install [--cert-dir ./data/certs]            # copy certs to Traefik mount point (./data/traefik/certs/)
homelab tls trust [--cert-dir ./data/certs]              # install CA into OS trust store (mkcert provider only)

homelab gitlab configure [--url https://gitlab.frenchexdev.lab]  # configure GitLab via REST API
                         [--admin-password]               #   initial root password (prompted if omitted)
                         [--wait-timeout 300]              #   seconds to wait for GitLab health (default 5 min)
homelab gitlab runner register [--url https://gitlab.frenchexdev.lab]  # register runner
                               [--runner-name runner-01]
                               [--executor docker]        #   runner executor type
                               [--docker-image alpine:latest]  # default CI image
homelab gitlab status [--url https://gitlab.frenchexdev.lab]   # health + runner list
```

CLI is thin — each command delegates to lib. `homelab vos *` delegates to `VosOrchestrator`. `homelab compose *` sets `DOCKER_HOST` and delegates to `DockerComposeClient`.

---

## JSON Schemas + VSCode Intellisense

- **Source of truth**: C# `[Builder]` config classes (`HomeLabConfig`, `VosMachineType`, etc.)
- **Generation**: `JsonSchema.Net` or `NJsonSchema` generates `.schema.json` from C# types at build time
- **Output**: `homelab-config.schema.json`, `vos-config.schema.json` shipped alongside YAML
- **VSCode**: `homelab init` generates `.vscode/settings.json` mapping YAML files → schemas
  ```json
  { "yaml.schemas": {
      "./schemas/homelab-config.schema.json": "config-homelab.yaml",
      "./schemas/vos-config.schema.json": "config-vos.yaml"
  }}
  ```
- **Workflow**: `homelab init` → edit YAML in VSCode with intellisense → `homelab validate` → `homelab packer build`

---

## Git Composability

- **Shared machine types** via git submodules (e.g., `frenchexdev/vos-alpine-dockerhost` → `modules/alpine-dockerhost/`)
- **Shared provisioning scripts** via submodules (e.g., `frenchexdev/provisioning-alpine/`)
- **Shared compose contributors** via submodules (e.g., `frenchexdev/compose-traefik/`)
- **Personal overrides** in `local/` directory (gitignored) — same merge strategy as Vos (config + local override)
- `homelab init --submodule frenchexdev/vos-alpine-dockerhost` → adds submodule + wires config

---

## What Already Exists

| Project | What it provides |
|---------|-----------------|
| `Vos/` | `VosConfig`, `VosMachineType`, `VosConfigMerger`, `VosOrchestrator`, `VagrantBackend`, `NetworkGenerator`, `IMachineTypeContributor`. `IVosBackend` = 28 commands (full Vagrant surface). CLI is thin wrapper delegating to lib. |
| `Vos.Alpine/` | `AlpineVirtualBoxContributor : IMachineTypeContributor` |
| `Vos.Alpine.DockerHost/` | `DockerHostContributor : IMachineTypeContributor` |
| `Packer.Bundle/` | `PackerBundle`, `IPackerBundleContributor`, `PackerBundleWriter` |
| `Packer.Alpine/` + `.DockerHost/` | `AlpineBaseContributor`, `DockerContributor`. **Complete HCL2 pipeline**: `PackerBundle.Apply(contributors)` → `PackerBundleWriter.WriteAsync()` → multi-file HCL2 + scripts + http + vagrant. Proven in DockerHost CLI. |
| `DockerCompose.Bundle/` | Source-generated `ComposeFile`, `ComposeService`, builders, `ComposeSerializer` |
| `Traefik.Bundle/` | Source-generated `TraefikStaticConfig`, `TraefikDynamicConfig`, builders, `TraefikSerializer` |
| Binary wrappers | `PackerClient`, `VagrantClient`, `DockerComposeClient` |

---

## What's Missing

| Gap | Project to create |
|-----|------------------|
| No `IComposeFileContributor` | Add to `DockerCompose.Bundle` |
| No Traefik compose service definition | `FrenchExDev.Net.Traefik.DockerCompose` (inside Traefik solution) |
| No GitLab compose service definition | `FrenchExDev.Net.GitLab.DockerCompose` (inside HomeLab solution) |
| No HomeLab orchestrator + CLI | `FrenchExDev.Net.HomeLab` + `.Cli` |
| No JSON schema generation | Schema gen from C# models (build-time task) |
| No Git binary wrapper | `FrenchExDev.Net.Git` (BinaryWrapper) |
| No hosts file manager | `FrenchExDev.Net.EtcHosts` (cross-platform) |
| No TLS cert generation | `FrenchExDev.Net.Tls` — `ITlsCertificateProvider` with `MkCertProvider` (wraps mkcert) + `NativeTlsProvider` (pure C#, `System.Security.Cryptography`) |
| No Vagrant box registry | `FrenchExDev.Net.Vagrant.Registry` |
| No GitLab API client | `FrenchExDev.Net.GitLab.Api.Bundle` (OpenAPI schema-driven) |
| No PiHole API client | `FrenchExDev.Net.PiHole.Api.Bundle` (schema-driven) |
| No Docker TCP exposure | Provisioning script in `DockerHostContributor` |
| No health polling utility | `WaitForHealthAsync()` in HomeLab lib |

---

## Solution Decomposition

### 1. `IComposeFileContributor` — add to `DockerCompose.Bundle`

```csharp
public interface IComposeFileContributor { void Contribute(ComposeFile composeFile); }
public static class ComposeFileExtensions { ... Apply() ... }
```

### 2. `Traefik.DockerCompose` — inside Traefik solution

- `TraefikLabels.cs` — fluent label builder → `Dictionary<string, string>`
- `TraefikComposeConfig.cs` — `[Builder]`
- `TraefikComposeContributor.cs` — `IComposeFileContributor`

### 3. `GitLab.DockerCompose` — inside HomeLab solution

- `GitLabComposeConfig.cs` — `[Builder]`
- `GitLabComposeContributor.cs` + `GitLabRunnerComposeContributor.cs`

### 4. `HomeLab` lib + CLI

- `HomeLabConfig` — `[Builder]` root config
- `HomeLabPipeline` — orchestrates all generation, returns `Result<T>`
- `HomeLabSchemaGenerator` — generates JSON schemas from config types
- `HomeLabCli` — System.CommandLine, one command per CLI verb above
- Leaf configs are plain records (no `[Builder]`)

### 5. Tool wrappers

- `FrenchExDev.Net.Git` — `[BinaryWrapper("git")]`
- `FrenchExDev.Net.EtcHosts` — plain C#, cross-platform hosts file read/write
- `FrenchExDev.Net.Tls` — TLS certificate generation lib
  - `ITlsCertificateProvider` interface:
    ```csharp
    public interface ITlsCertificateProvider
    {
        Task<Result<TlsCertificateBundle>> GenerateCaAsync(string caName, CancellationToken ct = default);
        Task<Result<TlsCertificateBundle>> GenerateCertAsync(TlsCertificateBundle ca, string domain, string[] sans, CancellationToken ct = default);
    }
    public record TlsCertificateBundle(byte[] Certificate, byte[] PrivateKey, string? CertPath, string? KeyPath);
    ```
  - `NativeTlsProvider` — pure C# via `System.Security.Cryptography.X509Certificates` (RSA 2048, self-signed CA, leaf certs with SAN). Zero external dependencies.
  - `MkCertProvider` — wraps `mkcert` via `[BinaryWrapper("mkcert")]`. Uses mkcert's CA trust store integration (browsers trust the CA automatically).
  - HomeLab selects provider based on config (`tls.provider: "native" | "mkcert"`)
  - Output: CA cert + key, domain cert + key → mounted into Traefik via Vagrant shared folder

### 6. API clients + Registry

- `FrenchExDev.Net.Vagrant.Registry` — HTTP server for Vagrant catalog
- `FrenchExDev.Net.GitLab.Api.Bundle` — OpenAPI schema → SG → typed client
- `FrenchExDev.Net.PiHole.Api.Bundle` — REST API schema → SG → typed client

---

## Full E2E Pipeline (each step = CLI command)

```bash
# Step 0: Bootstrap
homelab init --name test-lab --acme-name frenchexdev --acme-tld lab
cd test-lab

# Step 1: Generate packer project
homelab packer init

# Step 2: Build image
homelab packer build

# Step 3: Register box
homelab box add --local

# Step 4: Generate Vos config
homelab vos init

# Step 5: Boot VMs
homelab vos up

# Step 6: DNS + TLS
homelab dns add gitlab.frenchexdev.lab 192.168.56.10
homelab tls init --provider native                       # generates CA + *.frenchexdev.lab cert
homelab tls install                                      # copies certs to ./data/certs/ (Vagrant synced → /etc/ssl/traefik/)

# Step 7: Deploy services
homelab compose init
homelab compose deploy

# Step 8: Configure GitLab
homelab gitlab configure
homelab gitlab runner register

# Step 9: Verify
homelab gitlab status

# Step 10: Test CI pipeline
# (create test project, push, wait for CI)
```

E2E test = run each command, assert exit code 0 + expected side effects.

---

## Implementation Order

### Phase 1: Foundation (parallel)
1. `IComposeFileContributor` in `DockerCompose.Bundle`
2. `Traefik.DockerCompose` project

### Phase 2: HomeLab core
3. `GitLab.DockerCompose` project
4. `HomeLab` lib — config, pipeline, schema generation
5. `HomeLab.Cli` — full CLI surface
6. Unit tests: artifact generation, config builder, YAML round-trip, schema validation

### Phase 3: Tool wrappers (parallel)
7. `FrenchExDev.Net.Git` (BinaryWrapper)
8. `FrenchExDev.Net.EtcHosts` (cross-platform)
9. `FrenchExDev.Net.Tls` (`ITlsCertificateProvider` + `NativeTlsProvider` + `MkCertProvider`)

### Phase 4: API clients + Registry (parallel)
10. `Vagrant.Registry`
11. `GitLab.Api.Bundle` (OpenAPI schema-driven)
12. `PiHole.Api.Bundle` (schema-driven)

### Phase 5: Full E2E
13. E2E tests — each step = CLI invocation, assert exit code + side effects

---

## Verification

1. `dotnet build` each solution
2. `dotnet test --filter "Category!=E2E"` — all `result.IsSuccess`
3. `homelab init` → project dir with `config-homelab.yaml` + `.vscode/settings.json` + schemas
4. `homelab validate` → all configs pass schema validation
5. `homelab packer init && homelab vos init && homelab compose init` → all artifacts generated, YAML round-trips
6. `dotnet test --filter "Category=E2E"` — full lifecycle: every CLI command succeeds, GitLab running, CI pipeline green
