# Architecture

## Namespace Layout

```
FrenchExDev.Net.FiniteStateMachine           Core abstractions + shared engine
FrenchExDev.Net.FiniteStateMachine.Dynamic   Tier 1: string-based builder + definition
FrenchExDev.Net.FiniteStateMachine.Typed     Tier 2: enum-based builder + definition + TransitionTable
FrenchExDev.Net.FiniteStateMachine.Rich      Tier 3: interface-based builder + definition
FrenchExDev.Net.FiniteStateMachine.Graph     Visualization (Mermaid, Graphviz DOT)
```

## Three-Tier Design

All three tiers share a single `StateMachineEngine<TState, TEvent>` runtime. The tiers differ only in how states and events are represented, how transitions are defined, and how lookup is performed.

| Aspect | Dynamic | Typed | Rich |
|--------|---------|-------|------|
| State type | `string` | `enum` (struct) | Interface (class/record) |
| Event type | `string` | `enum` (struct) | Interface (class/record) |
| State data | None | None | Per-state fields (records) |
| Event payload | None | None | Per-event fields (records) |
| Compile-time safety | None | Full | Full (via generics + `is` matching) |
| Lookup | Dictionary `(string, string)` | Array-indexed `[int, int]` O(1) | Linear scan with `IsInstanceOfType` |
| Target state | Fixed | Fixed | Fixed or computed from event payload |
| Source generation | N/A | `[StateMachine]` + `[Transition]` | `[RichStateMachine]` + `[State]` + `[Event]` |
| Serialization | JSON round-trip | N/A | N/A |
| Definition/Machine split | Separate | Separate | Combined (definition IS the machine) |

## Core Abstractions

### Interfaces

```
IStateMachineDefinition<TState, TEvent>   Immutable graph: states, transitions, introspection
IStateMachine<TState, TEvent>             Running instance: CurrentState, FireAsync, CanFireAsync
IStateMachineListener<TState, TEvent>     Observer: 8 lifecycle hooks
IGuard<TState, TEvent>                    Async precondition: EvaluateAsync → Task<bool>
IStateAction<TState, TEvent>              Entry/exit action: ExecuteAsync
ITransitionAction<TState, TEvent>         Transition action: ExecuteAsync
IInvokedService<TEvent>                   Long-running service tied to state lifecycle
ICompositeStateMachine<TState, TEvent>    Parallel regions
IRegion<TState, TEvent>                   Named region wrapping a machine
```

### Value Types

```
Transition<TState>                   Success payload: From, To, IsReentrant
TransitionDefinition<TState, TEvent> Immutable: source, event, target, guards, actions, isInternal
TransitionRecord<TState, TEvent>     History entry: from, event, to, timestamp
HierarchyDefinition<TState>         Parent-child pair
ConcurrencyMode                      None | Semaphore
HistoryMode                          None | Shallow | Deep
```

## Engine Lifecycle (`StateMachineEngine`)

The `StateMachineEngine<TState, TEvent>` is the shared async runtime for all tiers. Every `FireAsync` call follows this exact sequence:

```
1. Find matching transitions for (CurrentState, event)
   └─ If hierarchy: walk parent chain until match found
2. Evaluate guards in order — first allowed wins
   └─ All denied → OnTransitionDeniedAsync → return failure Result
3. OnTransitioningAsync (listener notification)
4. [if not internal transition]
   ├─ OnStateExitingAsync
   ├─ Execute exit actions
   └─ OnStateExitedAsync
5. Execute transition actions
6. _currentState = target
7. [if not internal transition]
   ├─ OnStateEnteringAsync
   ├─ Execute entry actions
   └─ OnStateEnteredAsync
8. OnTransitionedAsync (listener notification)
9. Return success Result<Transition<TState>>
```

### Internal Transitions

When `TransitionDefinition.IsInternal == true`, steps 4 and 7 are skipped entirely — no exit/entry actions fire, no state lifecycle notifications. The state value may or may not change.

### Concurrency

- **`ConcurrencyMode.None`**: No synchronization. Caller must ensure single-threaded access.
- **`ConcurrencyMode.Semaphore`**: `SemaphoreSlim`-based async locking. Recommended for multi-threaded use. No `lock()`-based option since `lock` + `await` is an anti-pattern.

## Hierarchy (`StateHierarchy<TState>`)

States can form parent-child trees. The engine uses hierarchy for transition fallback:

```
AddChild(parent, child, isInitial: false)
TryGetParent(state) → parent?
GetChildren(state) → IReadOnlyList<TState>
TryGetInitialChild(parent) → child?
IsInState(current, query) → bool     Checks if current equals or descends from query
TryGetLCA(a, b) → lca?               Least Common Ancestor for transition scoping
```

When no transition is found at the current state, the engine walks up the parent chain searching for a match. Entry/exit actions fire for all states traversed during the ancestor walk.

## Composite State Machines (Parallel Regions)

`CompositeStateMachine<TState, TEvent>` manages multiple concurrent FSMs. Events are broadcast to all regions via `FireAllAsync`. Each region is a named `IRegion<TState, TEvent>` wrapping an `IStateMachine`.

```
ICompositeStateMachine<TState, TEvent>
├─ Regions: IReadOnlyList<IRegion<TState, TEvent>>
├─ AllRegionsTerminal: bool
├─ FireAllAsync(event) → IReadOnlyList<Result<Transition<TState>>>
└─ GetRegion(name) → IRegion?
```

## Deferred Events

`DeferredEventQueue<TState, TEvent>` queues events that cannot be handled in the current state for FIFO replay on state change:

```
Defer(state, event)           Register (state, event) as deferred
IsDeferred(state, event)      Check if current pair is deferred
Enqueue(event)                Queue for later
ReplayAsync(machine) → int   Replay all queued events, return count
```

## Timer Transitions

`TimerTransition<TState, TEvent>` auto-fires a timeout event after a configured duration:

```
new TimerTransition<State, Event>(machine, Event.Timeout, TimeSpan.FromSeconds(5))
Start()   — begin countdown
Cancel()  — stop countdown
```

## Graph Export

`StateGraph<TState, TEvent>` captures the static structure (states, transitions, dead-ends). Exporters produce visualization formats:

- **`MermaidExporter`**: `stateDiagram-v2` with `[*]` markers, configurable direction (`LR`/`TB`), optional final-state highlighting
- **`DotExporter`**: Graphviz DOT format

Both accept `IStateMachineDefinition` directly or a pre-built `StateGraph`.

## Source Generators

### SG #1: `FiniteStateMachineGenerator` (Typed Tier)

**Input**: Classes with `[StateMachine(typeof(StateEnum), typeof(EventEnum), InitialState = "...")]`

**Output per class**:
- `_engine` field + constructors (default, with initialState)
- `CurrentState` property
- `Fire{Event}Async()` — one per event member
- `CanFire{Event}Async()` — one per event member
- `PermittedEventsAsync()`
- `AddListener()`, `RemoveListener()`
- `CreateEngine()` — wires transitions, guards, entry/exit actions
- `HookListener` nested class — dispatches to user-defined partial hook methods

### SG #2: `RichStateMachineGenerator` (Rich Tier)

**Input**: Interfaces with `[RichStateMachine]`, records with `[State]`/`[Event]`, events with `[RichTransition(from, to)]`

**Output**:
- Visitor interfaces: `I{Domain}StateVisitor<TResult>`, `I{Domain}StateVisitor`
- Event visitors (if events exist): `I{Domain}EventVisitor<TResult>`, `I{Domain}EventVisitor`
- Extension methods: `{Domain}StateExtensions.Match(...)` (GADT-style exhaustive matching)
- Extension methods: `{Domain}EventExtensions.Match(...)` (if events exist)
- `Accept(visitor)` on each state and event record

### Shared Emission: `SourceGenerator.Lib`

Both generators share `SourceGenerator.Lib` — a Roslyn-free library containing pure C# string emission logic:
- `StateMachineEmitter` + `StateMachineEmitModel` (Typed tier)
- `RichEmitter` + `RichEmitModel` (Rich tier)

## Result Integration

`FireAsync` returns `Result<Transition<TState>>` from `FrenchExDev.Net.Result`:
- **Success**: transition executed, contains `Transition<TState>` with `From`, `To`, `IsReentrant`
- **Failure**: guard denied or no transition found, contains error message

This composes with the `Result` ecosystem: `Map`, `Bind`, `Recover`, `FromTry`.

## Listener System

`IStateMachineListener<TState, TEvent>` provides 8 lifecycle hooks:

| Hook | When |
|------|------|
| `OnTransitioningAsync` | Before transition (after guard approval) |
| `OnTransitionedAsync` | After transition completes |
| `OnStateEnteringAsync` | Before entry actions |
| `OnStateEnteredAsync` | After entry actions |
| `OnStateExitingAsync` | Before exit actions |
| `OnStateExitedAsync` | After exit actions |
| `OnTransitionDeniedAsync` | All guards denied the event |
| `OnErrorAsync` | Exception during lifecycle |

On `net10.0`, these use default interface methods (`=> Task.CompletedTask`). On `netstandard2.0`, use `StateMachineListenerBase<TState, TEvent>` which provides no-op overridable implementations.

Multiple listeners can be attached to a single machine. The built-in `HistoryListener<TState, TEvent>` records transitions to a ring buffer.
