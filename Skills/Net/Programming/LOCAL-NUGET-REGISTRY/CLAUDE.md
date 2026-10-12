# LOCAL-NUGET-REGISTRY — Claude Context

File-system NuGet feed (flat folder of `.nupkg` files) for rapid local iteration,
air-gapped builds, and SG package testing without nuget.org latency.

## Skill docs
- [Philosophy](PHILOSOPHY.md) — design rationale and trade-offs
- [Architecture](ARCHITECTURE.md) — internal structure
- [How-To](HOW-TO.md) — step-by-step tasks
- [Requirements](REQUIREMENTS.md) — formal requirements

## Related skills
- [CENTRAL-PACKAGE-MANAGEMENT](../CENTRAL-PACKAGE-MANAGEMENT/) — version declaration

## Related packages
- All local packages land here

## Notes for Claude
- Fixed path: `C:\code\FrenchExDev.Net\FrenchExDev.Net_i2\FrenchExDev.Net\__Local_Nuget_Registry__`
- Never overwrite existing `.nupkg` — always bump version
- SG packages need `<IncludeBuildOutput>false</IncludeBuildOutput>` and `<PackagePath>analyzers/dotnet/cs</PackagePath>`
- CI builds must NOT rely on local feed — use Azure Artifacts or pack-and-test in one job
- `dotnet nuget locals all --clear` needed after deleting old `.nupkg` to avoid stale cache
- Every local package version must be declared in `Directory.Packages.props`
