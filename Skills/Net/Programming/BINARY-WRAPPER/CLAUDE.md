# BINARY-WRAPPER — Claude Context

Type-safe .NET facade over external CLI tools derived from `--help` scraping. Three non-negotiable phases: Design (scrape into JSON) then Build (SG reads JSON, emits command classes) then Runtime (executor spawns process, streams events). Multi-version with `[SinceVersion]`/`[UntilVersion]` annotations. Parser plugins per CLI framework.

## Skill docs
- [Philosophy](PHILOSOPHY.md) — design rationale and trade-offs
- [Architecture](ARCHITECTURE.md) — internal structure
- [How-To](HOW-TO.md) — step-by-step tasks
- [Requirements](REQUIREMENTS.md) — formal requirements

## Related skills
- [DESIGN-PHASED-PROJECT](../DESIGN-PHASED-PROJECT/) — 3-phase pattern
- [BUILDER-PATTERN](../BUILDER-PATTERN/) — SG shared emitter
- [RESULT-PATTERN](../RESULT-PATTERN/) — failure values
- [WRAPPER-VERSIONING](../WRAPPER-VERSIONING/) — version discovery

## Related packages
- [`FrenchExDev.Net.BinaryWrapper`](../../../../Net/FrenchExDev/BinaryWrapper/)
- [`FrenchExDev.Net.BinaryWrapper.Design.Lib`](../../../../Net/FrenchExDev/BinaryWrapper/)
- [`FrenchExDev.Net.BinaryWrapper.SourceGenerator`](../../../../Net/FrenchExDev/BinaryWrapper/)

## Notes for Claude
- NEVER run scraping on `dotnet build` — requires containers/network; must be manual CLI only
- SkippedCommands is mandatory — `help`, `completion`, `serve` must be explicit
- Vagrant 2.4.4-2.4.5 crash on `vagrant box -h` due to `server_mode?` Ruby bug — do NOT "fix"
- SG targets `netstandard2.0` — never import `net10.0`-only APIs
- Descriptor must be `partial` — SG emits a partial of the same name
- Help text is the source of truth — recursive `--help` scraping, no hand-written models
- Design pipelines must cleanup containers in finally block
