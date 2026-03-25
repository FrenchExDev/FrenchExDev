# Philosophy

## Why Three Tiers?

No single state machine representation fits all use cases. Each tier targets a different trade-off between flexibility, type safety, and domain richness:

- **Dynamic** exists for cases where the FSM structure is not known at compile time — user-configurable workflows, rule engines, FSMs loaded from configuration or databases. Strings are the universal interchange format.

- **Typed** is the workhorse for protocol-level state machines where every state and event is known upfront. Enums provide exhaustiveness checking, IDE support, and O(1) lookup. The source generator eliminates boilerplate without sacrificing type safety.

- **Rich** serves domain-driven design where states carry domain data (e.g., `ShippedState(TrackingNumber)`) and events carry payloads (e.g., `ShipEvent(Carrier, TrackingNumber)`). Target states are computed from event payloads, enabling true event-sourced workflows.

All three tiers share the same `StateMachineEngine` runtime. This means guards, actions, listeners, hierarchy, regions, and concurrency work identically regardless of tier choice. The tier decision is about representation, not capability.

## Async-Only API

The entire API surface is `Task`-returning. There are no synchronous overloads.

**Rationale**: Guards, actions, and listeners frequently need I/O — database checks, HTTP calls, message publishing. Providing sync overloads would either block the calling thread (bad) or require maintaining two parallel code paths (worse). A single async API keeps the codebase small and forces callers to think about cancellation from the start.

`ConfigureAwait(false)` is used throughout the engine to avoid deadlocks in synchronization-context-bound environments (ASP.NET, WPF, etc.).

## Result<T> Over Exceptions

`FireAsync` returns `Result<Transition<TState>>` instead of throwing on denied transitions or missing transitions. This is intentional:

- A denied guard is a **normal business outcome**, not an exceptional condition. Throwing exceptions for control flow is expensive and obscures intent.
- The `Result` type composes with `Map`, `Bind`, `Recover` — enabling functional pipelines over FSM operations.
- Callers can pattern match on success/failure without try/catch ceremony.

Exceptions are reserved for truly exceptional conditions: bugs in guard/action implementations, null references, concurrency violations.

## Definition/Machine Split

For Dynamic and Typed tiers, the **definition** (immutable graph) and the **machine** (running instance with mutable state) are separate objects. This enables:

- Creating many machines from one definition (e.g., one per request, one per entity)
- Thread-safe definition sharing across machines
- Static graph introspection without instantiating a machine

The Rich tier intentionally breaks this pattern. Rich definitions ARE machines because computed target states (`Func<TEvent, TState, TState>`) couple the transition logic to the instance. A separate definition would be a leaky abstraction.

## Interfaces for States, Not Abstract Classes

Rich tier states implement interfaces (`IState`, domain-specific `IOrderState`, etc.) rather than inheriting from abstract classes. This is deliberate:

- **IDE navigation**: "Find All Implementations" on `IOrderState` lists every state — the primary mechanism for understanding the FSM's state space.
- **Multiple inheritance**: A state can implement multiple interfaces (`IOrderState + IAuditable`).
- **No forced base class**: Users choose records, classes, or structs freely.
- **SOLID**: Guards, actions, and listeners depend on abstractions, not concretions.

## Visitor + Match over Switch

The Rich tier source generator produces visitor interfaces and `Match` extension methods instead of relying on manual `switch`/`is` checks:

- **Exhaustiveness**: Adding a new state type causes a compile error in every `Match` call and every visitor implementation. No forgotten case at runtime.
- **No default branch**: Unlike `switch`, there is no `_` to sweep new cases under.
- **Composability**: Visitors are objects — injectable, testable, composable.

## Guards: AND Within, First-Match Across

Multiple guards on a single transition use AND logic (all must pass). Multiple transitions from the same `(state, event)` use first-match logic (first transition whose guards pass wins).

This mirrors how most real-world state machines work: a single transition has preconditions (AND), while alternative transitions represent mutually exclusive paths (first-match). The alternative — OR within a transition — would require awkward guard composition and unclear semantics for "which guard approved?"

## Hierarchy via Composition, Not Inheritance

State hierarchy uses a separate `StateHierarchy<TState>` object attached to the engine, not a type hierarchy. This keeps the state types flat (enums, records, interfaces) while still supporting nested state machines with proper LCA resolution, entry/exit cascading, and history modes.

## No Sync-Over-Async, No Async-Over-Sync

The library does not wrap `Task.Run(() => syncMethod())` to make sync code async, nor does it use `.Result`/`.GetAwaiter().GetResult()` to make async code sync. The API is async from top to bottom, and callers are expected to use `await` throughout.

## Minimal Dependencies

- **Core library**: Depends only on `FrenchExDev.Net.Result` (same org). No third-party packages.
- **Attributes**: Zero dependencies.
- **Source generators**: Depend only on Roslyn (build-time) and `SourceGenerator.Lib` (shared emission).
- **Testing**: Depends only on core. No test framework dependency — works with xUnit, NUnit, MSTest.

## ConfigureAwait(false)

Used consistently throughout the engine and all async paths. Library code should never assume a synchronization context exists. Callers in UI/web frameworks can `await` normally — the library won't deadlock regardless of the caller's context.

## Concurrency: Opt-In, Not Default

`ConcurrencyMode.None` is the default. Adding synchronization has a measurable cost (SemaphoreSlim allocation, contention overhead) that most single-threaded FSMs don't need. Users who need thread safety explicitly opt in with `ConcurrencyMode.Semaphore`.

There is no `lock()`-based option because `lock` + `await` is an anti-pattern in .NET. `SemaphoreSlim` is the correct async synchronization primitive.

## Testing: Paths, Not States

The testing library emphasizes **path-based testing** over **state-based testing**. `StateMachinePathGenerator` enumerates all valid paths through the FSM graph (via DFS with cycle detection), enabling exhaustive model-based testing:

- Every reachable state is tested
- Every transition is exercised
- Dead-end states and unreachable states surface naturally
- Guards are evaluated in context, not in isolation

This catches the category of bugs where individual transitions work but specific sequences don't — the most common FSM defect class.
