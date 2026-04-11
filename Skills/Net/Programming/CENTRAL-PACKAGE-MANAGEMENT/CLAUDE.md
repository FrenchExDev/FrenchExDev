# CENTRAL-PACKAGE-MANAGEMENT — Claude Context

Single `Directory.Packages.props` at `Net/FrenchExDev/` governs all NuGet versions;
never add `Version=` to `<PackageReference>` in project files.

## Skill docs
- [Philosophy](PHILOSOPHY.md) — design rationale and trade-offs
- [Architecture](ARCHITECTURE.md) — internal structure
- [How-To](HOW-TO.md) — step-by-step tasks
- [Requirements](REQUIREMENTS.md) — formal requirements

## Related skills
- [SOLUTION-LAYOUT](../SOLUTION-LAYOUT/) — directory structure
- [LOCAL-NUGET-REGISTRY](../LOCAL-NUGET-REGISTRY/) — local feed

## Related packages
- All packages (consume central versions)

## Notes for Claude
- Inline `Version=` on `<PackageReference>` silently overrides central version — future bumps won't apply
- Roslyn version mismatch across SG projects produces opaque `CS0103` errors — always align pin in Directory.Packages.props
- `CentralPackageTransitivePinningEnabled = true` forces transitives through the pin
- All Roslyn packages must match: CSharp, CSharp.Workspaces, Analyzers, Workspaces.MSBuild
