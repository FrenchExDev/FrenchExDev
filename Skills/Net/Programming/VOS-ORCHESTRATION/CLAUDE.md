# VOS-ORCHESTRATION — Claude Context

Backend-agnostic VM orchestration: YAML config (`config-vos.yaml`) + local override (deep-merged), `IVosBackend` with advertised `SupportedActions`, contributor patterns for machine types and bundles, 140+ event records via `IVosEventEmitter`.

## Skill docs
- [Philosophy](PHILOSOPHY.md) — design rationale and trade-offs
- [Architecture](ARCHITECTURE.md) — internal structure
- [How-To](HOW-TO.md) — step-by-step tasks
- [Requirements](REQUIREMENTS.md) — formal requirements

## Related skills
- [RESULT-PATTERN](../RESULT-PATTERN/) — failure values
- [SOLID](../SOLID/) — 1-method services
- [SG](../SG/) — Builder + Injectable
- [SYSTEM.COMMANDLINE](../SYSTEM.COMMANDLINE/) — Vos CLI

## Related packages
- [`FrenchExDev.Net.Vos`](../../../../Net/FrenchExDev/Vos/) (core)
- [`FrenchExDev.Net.Vos.Lib`](../../../../Net/FrenchExDev/Vos/)
- [`FrenchExDev.Net.Vos.Bundle`](../../../../Net/FrenchExDev/Vos/)
- [`FrenchExDev.Net.Vos.Infra.Vagrant`](../../../../Net/FrenchExDev/Vos/)
- [`FrenchExDev.Net.Vos.Infra.Podman`](../../../../Net/FrenchExDev/Vos/)
- [`FrenchExDev.Net.Vos.Cli`](../../../../Net/FrenchExDev/Vos/)

## Notes for Claude
- YAML reader always returns RESOLVED (merged) config; layers exposed separately for diffing
- Mutating commands take `--local` flag to target local override layer
- `bool` in option records must be `bool?` (SG limitation)
- `Dictionary<string, string>` concrete type in options, not `IReadOnlyDictionary`
- Vagrantfile is embedded static resource; C# code never parses or rewrites it
- `Failed` saga state MUST trigger production alerts
- `[Injectable]` auto-generates DI registration extension methods
