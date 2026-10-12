# BinaryWrapper — Claude Context

Framework for generating type-safe .NET wrappers around CLI binaries by scraping their `--help` text into JSON command trees and emitting commands, builders, and clients via a Roslyn source generator.

## Package docs
- [README](README.md)
- [Architecture](doc/ARCHITECTURE.md)
- [How-To](doc/HOW-TO.md)
- [Comparison Table](doc/COMPARISON-TABLE.md)
- [Scripts](doc/SCRIPTS.md)

## Relevant skills
- [BINARY-WRAPPER](../../../Skills/Net/Programming/BINARY-WRAPPER/PHILOSOPHY.md)
- [SG (Source Generators)](../../../Skills/Net/Programming/SG/PHILOSOPHY.md)
- [BUILDER-PATTERN](../../../Skills/Net/Programming/BUILDER-PATTERN/PHILOSOPHY.md)
- [SOLID](../../../Skills/Net/Programming/SOLID/PHILOSOPHY.md)
- [Solution Layout](../../../Skills/Net/Programming/SOLUTION-LAYOUT/ARCHITECTURE.md)
- [Central Package Management](../../../Skills/Net/Programming/CENTRAL-PACKAGE-MANAGEMENT/ARCHITECTURE.md)

## Solution
- `FrenchExDev.Net.BinaryWrapper.slnx`

## Notes for Claude
- Source generator and any project it transitively references **must** target `netstandard2.0`. Roslyn requirement, not negotiable.
- The Attributes project targets `netstandard2.0;net10.0` so it can be referenced from both runtime and SG.
- Generator uses Roslyn 5.3.0 — keep it pinned in `Directory.Packages.props`, never `Version=` in csproj.
- Naming collisions are resolved at the emitter (`NamingHelper`, `ClientClassEmitter.PruneClashingLeaves`). Never paper over with ad-hoc suffixes — add a test against the offending JSON.
- Two-phase scraping (`UseImageBuild` + `UseContainer`) is the default. Use `UseInlineContainer` only when install is fast enough not to need a cached image.
- `--reparse` mode reads cached `.help.txt` files and is the right move for parser iteration. Do not re-run the full container pipeline to debug a parser.
- Tests: 84 core + 146 SG + 40 design. Keep them green.
