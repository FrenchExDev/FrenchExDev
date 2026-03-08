# BinaryWrapper Consumer Comparison

## Packer vs Vagrant vs Podman

| Feature | Packer | Vagrant | Podman |
|---------|--------|---------|--------|
| **Binary** | `packer` | `vagrant` | `podman` |
| **CLI framework** | Go (custom) | Ruby (Thor-like) | Go (cobra) |
| **Flag style** | `-flag=value` | `--flag VALUE` | `--flag value` |
| **Boolean style** | `-flag=true/false` | `--flag` (presence) | `--flag` (presence) |
| **Descriptor attribute** | `[BinaryWrapper("packer", FlagPrefix="-", FlagValueSeparator="=", UseBoolEqualsFormat=true)]` | `[BinaryWrapper("vagrant")]` | `[BinaryWrapper("podman")]` |
| **Help parser** | `PackerHelpParser` (custom) | `VagrantHelpParser` (custom) | `PodmanHelpParser` (custom) |
| **Version source** | GitHub Releases | HashiCorp Releases API | GitHub Releases |
| **Version collector** | `GitHubReleasesVersionCollector` | `VagrantVersionCollector` (custom) | `GitHubReleasesVersionCollector` |
| **Scraping approach** | Single-phase (container per version) | Two-phase (build image, then scrape) | Two-phase (build image, then scrape) |
| **Container base** | `alpine:3.19` | `debian:bookworm` | `alpine:3.19` |
| **Binary install** | Download `.zip` from releases | Download `.deb` + `dpkg -i` | Download `.tar.gz` from releases |
| **Versions scraped** | ~40 | 7 (2.4.3--2.4.9) | 55 (4.1.1--5.8.0) |
| **Generated files** | ~50 | ~60 | 374 |
| **Command groups** | 1 (`Plugins`) | 4 (`Box`, `Cloud`, `Plugin`, `Snapshot`) | 18 (Container, Image, Network, Pod, ...) |
| **Output parsers** | `PackerBuildParser`, `PackerMachineReadableParser` | `VagrantOutputParser`, `VagrantMachineReadableParser` | None yet |
| **Event hierarchy** | `PackerEvent` (6 types) | `VagrantEvent` (6 types) | None yet |
| **Result collectors** | `PackerBuildCollector` | `VagrantUpCollector` | None yet |
| **Cleanup** | Containers only | Containers + images | Containers + images |
| **Known broken versions** | None | 2.4.4--2.4.5 (`server_mode?` bug) | 4.1.0, 4.3.0 (non-static binaries) |

## Help Parser Comparison

| Feature | StandardHelpParser | PackerHelpParser | VagrantHelpParser | PodmanHelpParser |
|---------|-------------------|-----------------|-------------------|------------------|
| **Value detection** | UPPERCASE / `<bracketed>` | Packer-specific rules | Section-based | Cobra type hints |
| **Type hints** | None | None | None | `string`, `int`, `stringArray`, etc. |
| **Multi-value** | Via `UPPERCASE...` | Via repeated flags | N/A | Via `strings`, `stringSlice`, etc. |
| **Section headers** | `Options:`, `Commands:` | Packer-specific | `Common commands:`, `Available subcommands:` | `Available Commands:`, `Flags:`, `Global Flags:` |
| **Skip list** | None | None | `help`, `list-commands`, `serve` | `help`, `completion` |

## Scraping Pipeline Comparison

### Single-phase (Packer)

```
For each version:
  podman run alpine:3.19 → install binary → scrape --help → JSON → rm container
```

Simpler, but slower for many versions because each scrape re-downloads and re-installs.

### Two-phase (Vagrant, Podman)

```
Phase 1: For each version:
  podman run base → install binary → podman commit → image
Phase 2: For each version (parallel):
  podman run image → scrape --help → JSON → rm container
Finally:
  rm all images
```

Faster for parallel scraping since Phase 2 starts from pre-built images. Better for binaries with slow or complex installation (Vagrant `.deb`, Podman static binary download).

## When to Use Which Approach

| Scenario | Recommended |
|----------|-------------|
| Few versions, fast install | Single-phase (Packer-style) |
| Many versions, slow install | Two-phase (Vagrant/Podman-style) |
| Standard `--help` format | `StandardHelpParser` |
| Go/cobra CLI | `PodmanHelpParser` |
| Custom help format | Write a custom `IHelpParser` |
| Tool with machine-readable output | Add `IOutputParser` + `IResultCollector` |
