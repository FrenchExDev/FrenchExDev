# IEC-61499 — Requirements

Acceptance criteria for any function block, composite, or deployment topology built on this pattern.

## Function Block Quality Bar

Every function block class must:

- [ ] Be `partial`
- [ ] Carry `[FunctionBlock("Name")]` exactly once
- [ ] Declare ports as `static readonly` fields (not properties, not instance fields)
- [ ] Have at least one `[EventInput]` (a function block with no inputs is useless)
- [ ] Have at least one `[EccState(Initial = true)]`
- [ ] Have at least one `[Algorithm]` per input event (otherwise the event has no effect)

## Port Declaration Quality Bar

For every event port:

- [ ] `[EventInput]` declares its `With = [...]` data inputs
- [ ] `[EventOutput]` declares its `With = [...]` data outputs
- [ ] The names in `With` reference real `[DataInput]` / `[DataOutput]` fields
- [ ] No two events share a name within the same function block

For every data port:

- [ ] `[DataInput]` / `[DataOutput]` decorates a `static readonly DataPort<T>` field
- [ ] The type parameter is a value type or an immutable reference type
- [ ] No data port is both an input and an output

## ECC Quality Bar

- [ ] Exactly one `[EccState(Initial = true)]` per function block
- [ ] Every state is reachable from the initial state via at least one input event
- [ ] Every algorithm names a real input event in `On = ...`
- [ ] Every algorithm names a real output event in `Emits = ...`
- [ ] When two algorithms share the same input event, they must declare different `From` states (otherwise the transition is ambiguous)

## Algorithm Quality Bar

Every `[Algorithm]` method must:

- [ ] Be a private instance method
- [ ] Take no parameters
- [ ] Return `void` (or `Task` for async — but blocking is forbidden)
- [ ] Read inputs only via `Port.Get()`
- [ ] Drive outputs only via `Port.Set(value)`
- [ ] Be deterministic (no clock, no RNG, no I/O — talk to the outside world via events)
- [ ] Be fast (algorithms run in the runtime's event loop; long-running work blocks the queue)

If you need I/O or long-running work, expose it as an output event the runtime can route asynchronously.

## Composite Function Block Quality Bar

- [ ] No `[Algorithm]` methods (composite behavior is the wiring)
- [ ] Every `[Child]` references a real function block type
- [ ] Every `[EventConnection]` and `[DataConnection]` references real port names (validated at compile time by the SG)
- [ ] No connection cycles unless the cycle is intentional and documented
- [ ] All children are wired to at least one event (orphan children are dead code)

## Infrastructure Compliance

For deployment topologies:

- [ ] Every `[Application]` has at least one `[Map]` referring to it
- [ ] Every `[Map]` references a real function block type and a real resource
- [ ] Every `[Resource]` belongs to a real `[Device]`
- [ ] Cross-resource connections are declared via `[CrossResourceConnection]` and use a supported `Transport`

## Generator Compliance

- [ ] The function block SG is referenced from the project
- [ ] The infrastructure SG is referenced from the deployment project (separate from the function block library)
- [ ] No hand-written runtime classes shadow the generated ones
- [ ] No hand-written FSM declarations duplicate the ECC

## Reuse of FiniteStateMachine

The IEC 61499 layer **must** delegate ECC implementation to `FrenchExDev.Net.FiniteStateMachine`:

- [ ] No custom FSM implementation in the IEC 61499 packages
- [ ] Every ECC-driven test uses `StateMachineAssert` from the FSM library
- [ ] Every ECC visualization uses `MermaidExporter` from the FSM library

Two implementations of the same primitive will drift. Reuse is not optional.

## Testing Quality Bar

Every function block needs at least:

- [ ] One happy-path test that fires every input event and asserts every output port
- [ ] One ECC-coverage test using `StateMachinePathGenerator` for blocks with ≥ 3 states
- [ ] One determinism test that runs the same input twice and asserts identical outputs
- [ ] One visualization fixture test that compares the emitted Mermaid against a checked-in baseline

For composite blocks, also:

- [ ] One end-to-end test firing the top-level input and asserting the top-level output
- [ ] One isolation test for each child block

## Runtime Compliance (Agent + Runtime)

- [ ] One agent process per device
- [ ] Single-threaded scheduling per resource (multi-threading is opt-in and requires synchronization)
- [ ] Hot-reload via assembly load contexts; never restart the process to deploy
- [ ] All blocks expose state and metrics via the agent's monitoring interface

## Things You Must Never Do

- Make ports non-`static` or non-`readonly`
- Make a function block class non-`partial`
- Read inputs or write outputs outside an algorithm method
- Block, sleep, or do I/O inside an algorithm
- Spawn threads from inside a function block
- Hand-write a state machine instead of using `[EccState]` + `[Algorithm]`
- Duplicate FSM machinery inside the IEC 61499 packages — call into `FrenchExDev.Net.FiniteStateMachine`
- Co-locate `[Application]` / `[Device]` / `[Resource]` attributes with function block code (keep behavior portable)
- Talk to OPC-UA directly from algorithm code (use the adapter and event ports)
- Edit generated runtime files (they are regenerated on every build)
- Restart the agent process to deploy new code (use hot-reload)
