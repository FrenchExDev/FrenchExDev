# HomeLab — Claude Context

CLI-first infrastructure configurator for local lab environments. Orchestrates
Packer image builds, Vos VM provisioning, Docker Compose stacks, TLS certs,
DNS entries, and GitLab configuration from a single `config-homelab.yaml`. 9
CLI command groups (`init`, `validate`, `packer`, `box`, `vos`, `compose`,
`dns`, `tls`, `gitlab`).

## Package docs
- [README](README.md)
- [Architecture](doc/ARCHITECTURE.md)
- [How-To](doc/HOW-TO.md)
- [Philosophy](doc/PHILOSOPHY.md)
- [Plan](doc/PLAN.md)

## Relevant skills
- [VOS-ORCHESTRATION](../../../Skills/Net/Programming/VOS-ORCHESTRATION/PHILOSOPHY.md)
- [PACKER-BUNDLE](../../../Skills/Net/Programming/PACKER-BUNDLE/PHILOSOPHY.md)
- [COMPOSE-BUNDLE](../../../Skills/Net/Programming/COMPOSE-BUNDLE/PHILOSOPHY.md)
- [SOLID](../../../Skills/Net/Programming/SOLID/PHILOSOPHY.md)
- [Solution Layout](../../../Skills/Net/Programming/SOLUTION-LAYOUT/ARCHITECTURE.md)
- [Central Package Management](../../../Skills/Net/Programming/CENTRAL-PACKAGE-MANAGEMENT/ARCHITECTURE.md)

## Solution
- `FrenchExDev.Net.HomeLab.slnx` (planned)

## Notes for Claude
- **Status: design / specification phase. NO source code yet.** README + doc/ describe the planned architecture only.
- Spec-only repo right now — there is no `src/` or `test/` to navigate. All work happens in the doc tree.
- Configuration is `config-homelab.yaml`, validated against JSON schemas generated from C# models. VSCode intellisense is a first-class requirement.
- Git submodules are the intended composition mechanism for shared configs; personal overrides layer on top (same pattern as Vos `local/`).
- The package depends on a long list of yet-to-exist projects (`HomeLab.Cli`, `EtcHosts`, `Tls`, `Vagrant.Registry`, `GitLab.Api.Bundle`, `PiHole.Api.Bundle`, etc.). Don't assume any of them exist.
- E2E tests are CLI invocations. The CLI surface IS the contract.
- `homelab init` scaffolds the file + VSCode settings; `homelab validate` is the JSON-schema gate.
