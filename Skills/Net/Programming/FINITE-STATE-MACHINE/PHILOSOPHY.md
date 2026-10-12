# FINITE-STATE-MACHINE — Philosophy

A finite state machine library that respects three different audiences and refuses to choose between them. The same machine can be expressed at runtime with strings, at compile time with enums, or in the type system with full domain types — and the engine semantics are identical at every tier.

## Three Tiers, One Engine

Most FSM libraries pick one shape and force you to use it. Three are needed because three different jobs need three different ergonomics:

| Tier | Identifier type | When |
|---|---|---|
| **Dynamic** | strings | Runtime-defined machines (rules engines, JSON-loaded workflows, end-user editors) |
| **Typed** | enums | Compile-time-known states with strong autocomplete; the 80% case |
| **Rich** | interfaces / records | States carry data; transitions are domain operations |

The library implements one engine and exposes three façades. Lifecycle, listeners, guards, actions, hierarchy, regions — all behave identically across tiers. You pick a tier per use case, not per project.

This is not three libraries glued together. It is one engine with three input languages.

## Async-First, Always

Every public method returns `Task` or `ValueTask`. There is no synchronous fire path.

The reason is simple: real transitions trigger I/O. Sending a notification, persisting state to a database, calling a webhook, evaluating a guard against a remote service — all of these are async operations. A synchronous fire path either blocks the calling thread or forces the developer to wrap everything in `Task.Run`. Neither is acceptable.

Async-first costs nothing for in-memory use cases (the task completes synchronously) and unlocks correctness for I/O-bound ones.

## Failure Is a Value

`FireAsync(event)` returns `Result<Transition<TState>>`. Not `void`. Not `bool`. Not "throws on rejection."

Three outcomes are possible:

| Outcome | Result |
|---|---|
| Transition allowed and executed | `Result.Success(new Transition(from, to, event))` |
| Event refused (no matching transition, guard failed, in final state) | `Result.Failure(reason)` |
| Action threw an exception | `Result.Failure(exception)` |

The caller decides what to do with each. The engine never throws on a denied event — that would be using exceptions as control flow for an entirely expected outcome.

This integrates with the Result pattern used throughout the codebase. A workflow handler can chain `FireAsync` calls with `Bind`/`Map` and react to the failure case explicitly.

## Hierarchy Without Pain

States can have parents. The engine resolves the lowest common ancestor for transitions, fires entry/exit actions in the correct order, and lets a parent state handle events its children don't override.

This is what makes FSMs useful for real workflows. Without hierarchy, every leaf state has to re-declare the same "Cancel" transition. With hierarchy, you declare it once on the parent and it applies to every child.

The pattern: parent states absorb shared behavior; leaf states refine it. The engine handles LCA resolution so the developer doesn't have to think about it.

## Parallel Regions for Orthogonal Concerns

A real-world entity may be in multiple states at once. An order is `Submitted ∧ AwaitingPayment`. A document is `Draft ∧ Locked`. A device is `Online ∧ Idle`.

The library expresses this with `ICompositeStateMachine` — a top-level container that runs N regions concurrently. Each region is its own state machine with its own current state, its own listeners, its own transitions. Events fire into all regions; each region decides whether to react.

The alternative — encoding the cross-product of states as flat states — explodes combinatorially. Two regions of 5 states each would need 25 flat states. Three regions of 5 each would need 125. Composite machines keep the model size linear.

## Listeners, Not Hard-Coded Hooks

The engine exposes 8 lifecycle hook points via `IStateMachineListener`:

```
OnBeforeFire / OnAfterFire
OnBeforeGuard / OnAfterGuard
OnEntry / OnExit
OnTransition
OnDenied
```

Anything that wants to react to state changes — logging, metrics, history recording, debugging UI, audit trails — implements `IStateMachineListener` and registers with the machine. The engine itself does not hard-code logging or metrics; those are listener concerns.

`HistoryListener` ships in the box: a ring-buffer recorder for transition history. It is a normal listener, not a special engine feature. You can write your own with the same interface.

## Source Generation for the Typed Tier

Hand-writing typed transitions with `When(...).On(...).TransitionTo(...)` is fine for small machines. For larger ones, the typed source generator produces strongly-typed fire methods (`FireOpenAsync`, `FireLockAsync`, `PermittedEventsAsync`) directly from `[Transition]` attributes:

```csharp
[StateMachine(typeof(DoorState), typeof(DoorEvent), InitialState = nameof(DoorState.Closed))]
public partial class DoorStateMachine
{
    [Transition(DoorState.Closed, DoorEvent.Open,   DoorState.Open)]
    [Transition(DoorState.Open,   DoorEvent.Close,  DoorState.Closed)]
    [Transition(DoorState.Closed, DoorEvent.Lock,   DoorState.Locked)]
    [Transition(DoorState.Locked, DoorEvent.Unlock, DoorState.Closed)]
    private static partial void DefineTransitions();
}
```

The generator emits the wiring. The developer sees only the model. A second generator handles the Rich tier, where states are types and the generator builds a typed `When<State>().On<Event>()` chain.

## Path Generation for Model-Based Testing

A state machine is a graph. The library exposes `StateMachinePathGenerator` — a DFS path enumerator — so you can ask "what are all the event sequences that reach state X?" and "what are all the cycles in this machine?" automatically.

Combined with `StateMachineAssert.PathReachesAsync(...)`, this enables model-based testing: write the machine, ask the generator for every reachable path, assert that each path produces the expected outcome.

This is not gold-plating. It is the only way to be confident that a 50-state workflow behaves correctly under every input sequence — manual test cases will miss combinations.

## Visualization Is Not Optional

The library ships Mermaid (`stateDiagram-v2`) and Graphviz DOT exporters. Every machine renders as a diagram with one method call.

Why ship this in the box: a state machine that can't be visualized is a state machine the team will refuse to maintain. The diagram is the documentation. Generating it from the model guarantees the documentation is never out of date.

## Concurrency Modes

Two modes are available:

- `ConcurrencyMode.None` — fire-and-forget, no locking. Fast, dangerous. Use only when callers serialize access externally.
- `ConcurrencyMode.Semaphore` — `SemaphoreSlim(1, 1)` around every fire. Safe for concurrent fire calls, marginally slower.

The default is `Semaphore`. Switch to `None` only when you know what you're doing and have measured a hot path that needs it.

## Trade-offs Accepted

| Trade-off | Decision |
|---|---|
| Three façades = more API surface | Three audiences need three ergonomics. The engine is shared. |
| Async-only API | Sync would either block or force `Task.Run`. Async is universal. |
| `Result<Transition<T>>` instead of bool | Failures need a reason; bool drops it. |
| Parallel regions add a top-level container | Cross-product encoding explodes. Composites keep models linear. |
| Two source generators (Typed + Rich) | Two tiers have different code shapes; one generator can't fit both cleanly. |
| Mermaid + DOT exporters in the core | Diagrams are documentation; documentation must stay in sync. |

## What This Pattern Is Not

- **Not a workflow engine.** No persistence, no human task queues, no compensating actions. A workflow engine can be built **on top of** an FSM, but the FSM itself is just the transition machine.
- **Not a rules engine.** Guards are async predicates, not declarative rule sets.
- **Not a saga framework.** Sagas need correlation, persistence, and compensation. An FSM is a primitive a saga can use.
- **Not a behavior tree.** Trees are decision-driven; FSMs are state-driven. Different problem.

Use an FSM whenever a domain object has discrete states and the transitions between them carry meaning. Use something else when the entity has continuous state, or when state is incidental to the behavior you're modeling.
