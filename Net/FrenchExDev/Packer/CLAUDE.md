# Packer — Claude Context

Typed C# wrapper for HashiCorp Packer built on the BinaryWrapper framework, with structured output parsing for `packer build` machine-readable events.

## Package docs
- [README](README.md)
- [Architecture](doc/ARCHITECTURE.md)
- [How-To](doc/HOW-TO.md)
- [Philosophy](doc/PHILOSOPHY.md)
- [Comparison Table](doc/COMPARISON-TABLE.md)
- [Plan](doc/PLAN.md)

## Relevant skills
- [BINARY-WRAPPER](../../../Skills/Net/Programming/BINARY-WRAPPER/PHILOSOPHY.md)
- [WRAPPER-VERSIONING](../../../Skills/Net/Programming/WRAPPER-VERSIONING/PHILOSOPHY.md)
- [PACKER-BUNDLE](../../../Skills/Net/Programming/PACKER-BUNDLE/PHILOSOPHY.md)
- [DESIGN-PHASED-PROJECT](../../../Skills/Net/Programming/DESIGN-PHASED-PROJECT/PHILOSOPHY.md)
- [SOLID](../../../Skills/Net/Programming/SOLID/PHILOSOPHY.md)
- [Solution Layout](../../../Skills/Net/Programming/SOLUTION-LAYOUT/ARCHITECTURE.md)
- [Central Package Management](../../../Skills/Net/Programming/CENTRAL-PACKAGE-MANAGEMENT/ARCHITECTURE.md)

## Solution
- `FrenchExDev.Net.Packer.slnx`

## Notes for Claude
- Packer uses HashiCorp's flag style: `[BinaryWrapper("packer", FlagPrefix="-", FlagValueSeparator="=", UseBoolEqualsFormat=true)]`. Do not change to GNU defaults.
- Help parser is `PackerHelpParser` (built-in), not Cobra. Help flag is `-h`, not `--help`.
- Pipeline uses `UseInlineContainer` (single-step) rather than `UseImageBuild` + `UseContainer`. Install is a single ZIP from `releases.hashicorp.com`, fast enough not to need image caching.
- Version collector is `PackerVersionCollector`, a custom `IVersionCollector` against the HashiCorp releases JSON index — not GitHub.
- HCL2 templates are the modern format; legacy JSON templates are deprecated upstream but the wrapper still emits typed commands for both.
- Output parsing implemented: `PackerBuildParser` + `PackerMachineReadableParser` + `PackerBuildCollector` → `PackerBuildResult`. 7 event types under `PackerEvent`.
- Pre-1.0 versions (`0.1.0`–`0.12.3`) are marked known-missing; do not try to scrape them.
