# IEC-61499 — Claude Context

Industrial control via function blocks: event-driven, ECC (Execution Control Chart) backed by typed FSM, ports as `static readonly` fields, algorithms as methods. Basic blocks (ECC + algorithms) vs Composite blocks (child wiring). Source-generated, AOT-compatible.

## Skill docs
- [Philosophy](PHILOSOPHY.md) — design rationale and trade-offs
- [Architecture](ARCHITECTURE.md) — internal structure
- [How-To](HOW-TO.md) — step-by-step tasks
- [Requirements](REQUIREMENTS.md) — formal requirements

## Related skills
- [FINITE-STATE-MACHINE](../FINITE-STATE-MACHINE/) — ECC implementation
- [MAPPER-PATTERN](../MAPPER-PATTERN/) — schema-driven generation

## Related packages
- [`FrenchExDev.Net.FiniteStateMachine`](../../../../Net/FrenchExDev/FiniteStateMachine/)

## Notes for Claude
- Ports MUST be `static readonly` — instance fields invisible to source generator
- Function block class MUST be `partial`
- Cannot read DataInputs outside algorithm methods — sampling happens at event time
- Cannot write DataOutputs outside algorithms — propagation is event-driven
- No blocking, sleeping, or I/O inside algorithms — runtime event loop would starve
- Cannot spawn threads from function blocks — runtime owns scheduling
- Always-running agent with hot-reload via assembly load contexts (no restart to deploy)
