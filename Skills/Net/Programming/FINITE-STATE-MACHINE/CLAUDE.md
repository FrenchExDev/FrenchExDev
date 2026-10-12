# FINITE-STATE-MACHINE — Claude Context

Three-tier FSM library: Dynamic (runtime strings), Typed (enums), Rich (domain types). Same underlying async engine, `Result<Transition<T>>` return type, hierarchy with LCA resolution, parallel regions, and 8 listener hooks.

## Skill docs
- [Philosophy](PHILOSOPHY.md) — design rationale and trade-offs
- [Architecture](ARCHITECTURE.md) — internal structure
- [How-To](HOW-TO.md) — step-by-step tasks
- [Requirements](REQUIREMENTS.md) — formal requirements

## Related skills
- [IEC-61499](../IEC-61499/) — ECC implementation uses FSM

## Related packages
- [`FrenchExDev.Net.FiniteStateMachine`](../../../../Net/FrenchExDev/FiniteStateMachine/)
- [`FrenchExDev.Net.Result`](../../../../Net/FrenchExDev/Result/)

## Notes for Claude
- Async-only API (`Task`/`ValueTask`) — real transitions trigger I/O
- Deferred events queue replays when leaving a state
- Listeners run inside the lifecycle — recursive `FireAsync` from listener = deadlock
- `ConcurrencyMode.None` requires external serialization guarantee
- Three separate facade builders, not a single builder with modes
- SG generates typed/rich tiers from `[Transition]` attributes
- `StateMachinePathGenerator` uses DFS with max-depth to prevent infinite graphs
