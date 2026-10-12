# Wrapper.Versioning — Claude Context

Generic version-collection and design-time pipeline infrastructure: discover items from GitHub/GitLab, download artifacts in parallel, transform them through a composable middleware chain, and write them to disk.

## Package docs
- [README](README.md)
- [Architecture](doc/ARCHITECTURE.md)
- [How-To](doc/HOW-TO.md)
- [Philosophy](doc/PHILOSOPHY.md)
- [Tokens](doc/TOKENS.md)
- [Plan](doc/PLAN.md)
- [Plan Generalize](doc/PLAN-GENERALIZE.md)

## Relevant skills
- [WRAPPER-VERSIONING](../../../Skills/Net/Programming/WRAPPER-VERSIONING/PHILOSOPHY.md)
- [DESIGN-PHASED-PROJECT](../../../Skills/Net/Programming/DESIGN-PHASED-PROJECT/PHILOSOPHY.md)
- [SOLID](../../../Skills/Net/Programming/SOLID/PHILOSOPHY.md)
- [Solution Layout](../../../Skills/Net/Programming/SOLUTION-LAYOUT/ARCHITECTURE.md)
- [Central Package Management](../../../Skills/Net/Programming/CENTRAL-PACKAGE-MANAGEMENT/ARCHITECTURE.md)

## Solution
- `FrenchExDev.Net.Wrapper.Versioning.slnx`

## Notes for Claude
- Entire public API lives in a single ~622-line `Code.cs` with section headers. Do not split it into 15 files.
- `IVersionCollector` extends `IItemCollector<string>`. Explicit interface impl delegates `CollectItemsAsync` to `CollectVersionsAsync` — keep this shape.
- `GitHubReleasesVersionCollector.CompareVersionStrings` is the canonical version comparator. Reuse it across the codebase, do not introduce a SemVer NuGet package.
- Pre-release filtering is **per collector**, not central. GitHub releases use the `prerelease` flag, GitHub tags use the `-` heuristic, GitLab uses `upcoming_release`. Do not unify.
- Runner uses `SemaphoreSlim` + `Task.WhenAll`, not `Parallel.ForEachAsync`. The choice is deliberate — see PHILOSOPHY.
- Consumed via `ProjectReference` only. Never publish as a NuGet package.
- 142 xUnit tests, test quality score 1.0, full branch + line coverage. Keep it.
