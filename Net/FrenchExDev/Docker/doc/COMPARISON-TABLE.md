# Comparison Table

Detailed comparison of FrenchExDev.Net.Docker with sibling BinaryWrapper projects.

## Overview

| | Docker | Podman | Vagrant | Packer | GitLab CLI |
|---|---|---|---|---|---|
| **Binary name** | `docker` | `podman` | `vagrant` | `packer` | `glab` |
| **Binary language** | Go | Go | Ruby | Go | Go |
| **CLI framework** | cobra | cobra | custom | custom | cobra (modified) |

## Generation

| | Docker | Podman | Vagrant | Packer | GitLab CLI |
|---|---|---|---|---|---|
| **Versions scraped** | 129 | 58 | 7 | -- | -- |
| **Generated files** | 350 | 386 | -- | -- | -- |
| **Command classes** | 174 | 192 | -- | -- | -- |
| **Builder classes** | 174 | 192 | -- | -- | -- |
| **Command groups** | 16 | 18 | 4 | -- | -- |
| **Nested sub-groups** | 2 | 2 | 1 | -- | -- |

## Scraping

| | Docker | Podman | Vagrant | Packer | GitLab CLI |
|---|---|---|---|---|---|
| **Help parser** | cobra | cobra | custom (`VagrantHelpParser`) | standard | custom (`GlabHelpParser`) |
| **Version collector** | `GitHubTagsVersionCollector` | `GitHubReleasesVersionCollector` | custom | -- | `GitLabReleasesVersionCollector` |
| **Base image** | alpine:3.19 | alpine:3.19 | pre-built vagrant images | -- | alpine:3.19 |
| **Binary source** | download.docker.com (static .tgz) | GitHub releases (static .tar.gz) | HashiCorp releases (.deb) | -- | GitLab releases (.tar.gz) |
| **DefaultMinVersion** | 23.0.0 | 4.1.0 | 2.4.3 | -- | 1.47.0 |
| **Skipped commands** | -- | `help`, `completion` | `help`, `list-commands`, `serve` | -- | `help`, `completion`, `check-update` |
| **Known broken versions** | -- | 4.1.0, 4.3.0 | 2.4.4, 2.4.5 | -- | -- |

## CLI Conventions

| | Docker | Podman | Vagrant | Packer | GitLab CLI |
|---|---|---|---|---|---|
| **Flag prefix** | `--` (default) | `--` (default) | `--` (default) | `-` (custom) | `--` (default) |
| **Flag value separator** | space (default) | space (default) | space (default) | `=` (custom) | space (default) |
| **Bool format** | presence (default) | presence (default) | presence (default) | `=true/false` (custom) | presence (default) |
| **BinaryWrapper attribute** | `[BinaryWrapper("docker")]` | `[BinaryWrapper("podman")]` | `[BinaryWrapper("vagrant")]` | `[BinaryWrapper("packer", FlagPrefix="-", ...)]` | `[BinaryWrapper("glab")]` |

## Custom Code

| | Docker | Podman | Vagrant | Packer | GitLab CLI |
|---|---|---|---|---|---|
| **Output parsers** | None | None | `VagrantOutputParser`, `VagrantMachineReadableParser` | Custom HCL parser | None |
| **Event types** | None | None | 6 event types | -- | None |
| **Hand-written source** | 6 lines | 6 lines | ~500 lines | -- | 6 lines |
| **Auth required for scraping** | No | No | No | -- | No (--help only) |

## Version History Highlights

### Docker
- **18.09.0**: First scraped version, full legacy CLI
- **20.10.0**: Last version with `docker deploy`
- **23.0.0**: Major restructuring -- removed top-level `build`, `exec`, `images`, `login`, `logout` (replaced by sub-group equivalents); version numbering changed from `YY.MM.patch` to sequential
- **29.3.0**: Latest scraped version

### Podman
- **4.1.0**: First scraped version (broken static binary)
- **4.4.0**: Asset naming changed from `podman-remote-static.tar.gz` to `podman-remote-static-linux_amd64.tar.gz`
- **5.8.1**: Latest scraped version

### Vagrant
- **2.4.3**: First scraped version
- **2.4.4-2.4.5**: `server_mode?` bug causes crashes on `vagrant box/cloud/plugin -h`
- **2.4.9**: Latest scraped version
