# PACKER-BUNDLE — Claude Context

In-memory C# workspace emitting HCL2 Packer templates as multi-file directories. Plugin records and builders auto-scraped from Go `.hcl2spec.go` source on GitHub. Custom `HclWriter` (no third-party HCL2 lib). Contributor pipeline for composition.

## Skill docs
- [Philosophy](PHILOSOPHY.md) — design rationale and trade-offs
- [Architecture](ARCHITECTURE.md) — internal structure
- [How-To](HOW-TO.md) — step-by-step tasks
- [Requirements](REQUIREMENTS.md) — formal requirements

## Related skills
- [BUILDER-PATTERN](../BUILDER-PATTERN/) — AbstractBuilder reuse
- [SG](../SG/) — schema-driven generation

## Related packages
- [`FrenchExDev.Net.Packer`](../../../../Net/FrenchExDev/Packer/)

## Notes for Claude
- HCL2 only — no JSON Packer templates ever
- Never edit `scrape/*.json` by hand — re-run the Design project to refresh
- Design project hits GitHub — never invoke from build pipeline, only manually with `GITHUB_TOKEN`
- `PackerBundle` is intentionally NOT a record (mutable workspace, not serializable)
- `BundleFile` is mutable so contributors can append/replace content
- Shared communicators (SSH/WinRM) detected by regex matching Go's `mapstructure:",squash"` annotation
- Multi-file template: packer.pkr.hcl, variables.pkr.hcl, locals.pkr.hcl, sources.pkr.hcl, build.pkr.hcl
