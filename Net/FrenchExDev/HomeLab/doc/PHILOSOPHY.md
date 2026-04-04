# HomeLab -- Philosophy

## CLI-first means testable-first

Every HomeLab action is a CLI command. There is no web interface, no interactive wizard, no manual step that can't be scripted. This is not minimalism -- it's a testing strategy.

E2E tests are CLI invocations. If `homelab packer build` succeeds, the image was built. If `homelab vos up` succeeds, the VM is running. If `homelab gitlab status` returns healthy, the lab is operational. The CLI is both the user interface and the test harness.

Interactive tools are harder to test, harder to reproduce, and harder to debug. A CLI command that fails has a clear exit code, a clear error message, and a clear invocation to reproduce.

---

## Schema-validated YAML over freeform config

`config-homelab.yaml` is not a bag of arbitrary keys. It is validated against a JSON schema generated from C# config models at build time. This means:

- VSCode shows intellisense while editing YAML
- Typos are caught before any command runs
- The schema is always in sync with the code (it is generated, not hand-maintained)
- `homelab validate` catches structural errors in CI

Configuration files without schema validation are a liability. They look simple until someone misspells a key and spends an hour debugging why the VM has 1GB of RAM instead of 4GB.

---

## Git-composable: shared base, personal overrides

A team shares a lab configuration via git. But team members have different machines: different RAM, different network adapters, different VirtualBox versions. Personal overrides (memory, IPs, secrets) go in `local/config-homelab-local.yaml`, which is gitignored.

This is the same layering pattern as Vos (`config-vos.yaml` + `local/config-vos-local.yaml`): base config is committed and shared, local overrides are personal and ephemeral. Deep merge resolves them at read time.

The alternative -- everyone maintains their own fork of the config -- leads to drift, merge conflicts, and "it works on my machine" problems.

---

## Meta-orchestrator, not a new tool

HomeLab does not implement image building, VM provisioning, or container management. It orchestrates existing tools that already do those things well:

- Packer builds images
- Vagrant manages VMs (via Vos)
- Docker Compose manages services
- Traefik routes traffic
- GitLab runs CI

HomeLab's job is to glue them together with sensible defaults, validated configuration, and a unified CLI. Each underlying tool can still be used independently. HomeLab adds coordination, not coupling.

---

## Multiple instances by isolation, not by convention

Two labs can coexist on the same machine because each lab has its own:

- VM names (prefixed by lab name)
- IP subnet (configurable)
- DNS entries
- TLS certificates
- Docker networks

This isolation is structural, not just naming convention. Two labs with different subnets can't interfere with each other's networking. Two labs with different VM names can't accidentally destroy each other's VMs.

---

## The 10-step pipeline is the specification

The E2E pipeline (init → packer → box → vos → dns → tls → compose → gitlab → verify) is not just documentation. It is the specification of what HomeLab must support. Every step is a CLI command. Every step is an E2E test. If any step breaks, the pipeline fails.

This is deliberately rigid. A lab environment is either fully operational or it isn't. There is no "partially working" state worth supporting. The pipeline either runs end-to-end or it doesn't.
