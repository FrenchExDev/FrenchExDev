# FiniteStateMachine — Claude Context

Comprehensive .NET FSM library: three tiers (Dynamic / Typed / Rich), two source generators, async-first, hierarchical states, parallel regions, listener hooks, path generation, Mermaid/DOT exporters. Returns `Result<Transition<TState>>`.

## Package docs
- [README](README.md)
- [Architecture](doc/ARCHITECTURE.md)
- [How-To](doc/HOW-TO.md)
- [Philosophy](doc/PHILOSOPHY.md)

## Relevant skills
- [FINITE-STATE-MACHINE](../../../Skills/Net/Programming/FINITE-STATE-MACHINE/PHILOSOPHY.md)
- [SG](../../../Skills/Net/Programming/SG/PHILOSOPHY.md)
- [SOLID](../../../Skills/Net/Programming/SOLID/PHILOSOPHY.md)
- [Solution Layout](../../../Skills/Net/Programming/SOLUTION-LAYOUT/ARCHITECTURE.md)
- [Central Package Management](../../../Skills/Net/Programming/CENTRAL-PACKAGE-MANAGEMENT/ARCHITECTURE.md)

See also: `../Result/README.md` for the Result<T> integration.

## Solution
- `FrenchExDev.Net.FiniteStateMachine.slnx`

## Notes for Claude
- Three tiers (Dynamic / Typed / Rich) all share **one engine**. Pick the tier per use case, not per project. Don't merge them.
- Two source generators (Typed in `.SourceGenerator`, Rich in `.Design`) deliberately stay separate because the two tiers have different code shapes.
- `FireAsync` returns `Result<Transition<TState>>` — denied events are values, never exceptions. Don't `try/catch` around fire calls.
- `ConcurrencyMode.Semaphore` is the default. Switch to `None` only after measuring and only if callers serialize externally.
- Listeners must NOT call `FireAsync` from inside a hook — use deferred events instead. Recursive fires deadlock.
- ~92 tests in the box. The IEC61499.2 ECC code generation depends on this library — never duplicate FSM machinery in IEC packages.
- The `[StateMachine]`-decorated class must be `partial` and contain the magic `private static partial void DefineTransitions()` method.
