# SOLUTION-LAYOUT — Claude Context

Uniform 3-tier directory (doc/src/test) and 3-project structure (Runtime/Testing/Tests)
with two solution files (.slnx) across all 44+ packages.

## Skill docs
- [Philosophy](PHILOSOPHY.md) — design rationale and trade-offs
- [Architecture](ARCHITECTURE.md) — internal structure
- [How-To](HOW-TO.md) — step-by-step tasks
- [Requirements](REQUIREMENTS.md) — formal requirements

## Related skills
- [CENTRAL-PACKAGE-MANAGEMENT](../CENTRAL-PACKAGE-MANAGEMENT/) — version governance
- [SG](../SG/) — project decomposition mirrors SG pattern

## Related packages
- All packages under [`Net/FrenchExDev/`](../../../../Net/FrenchExDev/)

## Notes for Claude
- Runtime projects reference only other Runtime projects; never reference `.Testing` across packages
- Testing project is `<IsPackable>false</IsPackable>`
- Tests can reference another package's `.Testing` (shared fakes)
- Use relative paths in `.slnx` files, never absolute
- Baseline: `net10.0`, Nullable enabled, ImplicitUsings enabled
