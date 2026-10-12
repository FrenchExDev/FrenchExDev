# QualityGate — Claude Context

A code-quality CLI and library that loads a solution via MSBuild, runs tests with coverage and mutation reports, and emits aggregate quality reports. Designed as the canonical SOLID + hand-written-fakes example in the monorepo.

## Package docs
- [README](README.md)
- [Architecture](doc/ARCHITECTURE.md)
- [How-To](doc/HOW-TO.md)
- [Philosophy](doc/PHILOSOPHY.md)
- [QUALITY-GATE.md](QUALITY-GATE.md)

## Relevant skills
- [Quality Gates](../../../Skills/Net/Programming/QUALITY-GATES/PHILOSOPHY.md)
- [Hand-Written Fakes](../../../Skills/Net/Programming/HAND-WRITTEN-FAKES/PHILOSOPHY.md)
- [SOLID](../../../Skills/Net/Programming/SOLID/PHILOSOPHY.md)
- [System.CommandLine](../../../Skills/Net/Programming/SYSTEM.COMMANDLINE/WHAT.md)
- [Solution Layout](../../../Skills/Net/Programming/SOLUTION-LAYOUT/ARCHITECTURE.md)
- [Central Package Management](../../../Skills/Net/Programming/CENTRAL-PACKAGE-MANAGEMENT/ARCHITECTURE.md)

## Solution
- `FrenchExDev.Net.QualityGate.slnx`

## Notes for Claude
- This package is the canonical reference for the SOLID skill (1-method interfaces) and the HAND-WRITTEN-FAKES skill — its `test/.../Fakes/` folder is the worked example. Do not introduce mocking frameworks here.
- Four core abstractions, each with exactly one method: `ISolutionLoader`, `ICoverageReportParser`, `IMutationReportParser`, `IReportWriter`. Do not bundle methods.
- 256 tests, 100% line/branch coverage, test quality score 1.0 — keep it that way; new code must come with tests.
- CLI entry: `dotnet run --project QualityGate/src/FrenchExDev.Net.QualityGate.Cli -- test`.
- `MsBuildSolutionLoader` requires MSBuildLocator registration and cannot be unit-tested without an MSBuild install — that's why a `FakeSolutionLoader` exists.
