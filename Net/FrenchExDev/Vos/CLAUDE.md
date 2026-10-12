# Vos — Claude Context

"Vagrant on Steroids": typed, backend-agnostic VM orchestration over Vagrant
and Podman Machine. Layered SOLID architecture with `IVosBackend` abstraction,
`[Builder]` option records, 140+ event records, System.CommandLine v2 CLI,
and PowerShell cmdlets (`gvm` alias).

## Package docs
- [README](README.md)
- [Architecture](doc/ARCHITECTURE.md)
- [How-To](doc/HOW-TO.md)
- [Philosophy](doc/PHILOSOPHY.md)
- [Plan V2](doc/PLAN-V2.md)
- [E2E Semi-Tests Plan](doc/PLAN-E2E-SEMI-TESTS.md)

## Relevant skills
- [VOS-ORCHESTRATION](../../../Skills/Net/Programming/VOS-ORCHESTRATION/PHILOSOPHY.md)
- [SOLID](../../../Skills/Net/Programming/SOLID/PHILOSOPHY.md)
- [BUILDER-PATTERN](../../../Skills/Net/Programming/BUILDER-PATTERN/PHILOSOPHY.md)
- [RESULT-PATTERN](../../../Skills/Net/Programming/RESULT-PATTERN/PHILOSOPHY.md)
- [Solution Layout](../../../Skills/Net/Programming/SOLUTION-LAYOUT/ARCHITECTURE.md)
- [Central Package Management](../../../Skills/Net/Programming/CENTRAL-PACKAGE-MANAGEMENT/ARCHITECTURE.md)

## Solution
- `FrenchExDev.Net.Vos.slnx`

## Notes for Claude
- Requires the **local NuGet registry** at `C:\code\FrenchExDev.Net\FrenchExDev.Net_i2\FrenchExDev.Net\__Local_Nuget_Registry__` to resolve sibling packages.
- All `[Builder]` option classes must use `bool?` (not `bool`) — SG limitation. The SG cannot tell "set to false" from "default".
- All `[Builder]` option classes must use concrete `Dictionary<,>` (not `IReadOnlyDictionary<,>`) — SG can't assign through readonly indexer.
- Builder SG needs `Builder.SourceGenerator.Lib` built FIRST. Solution-level build order matters.
- The Vagrantfile is shipped as an **embedded resource** in `Vos.Bundle` and is static — it reads `config-vos.yaml` at runtime. Never edit it from C#.
- `--local` flag on mutating commands targets `local/config-vos-local.yaml` (the layered override).
- Use the `Res = FrenchExDev.Net.Result` namespace alias to work around the namespace/type collision.
- Backends MUST honor `SupportedActions` — silently ignoring unsupported ops violates Liskov.
- Hand-written fakes in `test/.../Fakes/` are the only acceptable test doubles.
