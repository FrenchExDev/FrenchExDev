# Vagrant — Claude Context

Strongly-typed .NET wrapper for HashiCorp Vagrant, generated from scraped `--help` output across multiple Vagrant versions via the BinaryWrapper framework.

## Package docs
- [README](README.md)
- [Architecture](doc/ARCHITECTURE.md)
- [How-To](doc/HOW-TO.md)

## Relevant skills
- [BINARY-WRAPPER](../../../Skills/Net/Programming/BINARY-WRAPPER/PHILOSOPHY.md)
- [WRAPPER-VERSIONING](../../../Skills/Net/Programming/WRAPPER-VERSIONING/PHILOSOPHY.md)
- [DESIGN-PHASED-PROJECT](../../../Skills/Net/Programming/DESIGN-PHASED-PROJECT/PHILOSOPHY.md)
- [SOLID](../../../Skills/Net/Programming/SOLID/PHILOSOPHY.md)
- [Solution Layout](../../../Skills/Net/Programming/SOLUTION-LAYOUT/ARCHITECTURE.md)
- [Central Package Management](../../../Skills/Net/Programming/CENTRAL-PACKAGE-MANAGEMENT/ARCHITECTURE.md)

## Solution
- `FrenchExDev.Net.Vagrant.slnx`

## Notes for Claude
- Vagrant 2.4.4 and 2.4.5 have a `server_mode?` Ruby bug that crashes `vagrant box/cloud/plugin -h`. These versions appear as empty leaf groups — do not try to "fix" them.
- `VagrantHelpParser` is custom and handles Vagrant's `Common commands:` / `Available subcommands:` headers. Standard parsers do not work for Vagrant.
- `SkippedCommands` set: `help` (infinite recursion), `list-commands` (infinite recursion), `serve` (starts a GRPC server, hangs the scrape).
- Scraping uses Debian (`debian:bookworm`), not Alpine, because Vagrant ships as a `.deb`. Shell is `bash`, not `sh`.
- WSL fix is required in the install script: `sed -i 's/@_wsl = true/@_wsl = false/' /opt/vagrant/embedded/gems/gems/vagrant-*/lib/vagrant/util/platform.rb`.
- Pre-built images are tagged `vagrant-scrape:{version}`.
- `LoggingHelpParser` decorator is used in `Program.cs` for parser debugging — keep it.
- Output parsing implemented: `VagrantOutputParser` + `VagrantUpCollector` → `VagrantUpResult`.
