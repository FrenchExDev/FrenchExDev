# Finite State Machine Library — Implementation Plan

## Context

The user wants a comprehensive FSM library (`FrenchExDev.Net.FiniteStateMachine`) with a source generator, offering three tiers of usage:
1. **Dynamic** — string-based, runtime-constructed FSMs for config-driven workflows
2. **Typed** — enum-based, compile-time type-safe FSMs (with optional SG augmentation)
3. **Rich** — interface-based FSMs with class/record implementations for full OO/SOLID extensibility and IDE navigation (Find All Implementations)

The existing GitHub project (`FrenchExDev/FrenchExDev.Net.FiniteStateMachine`) is an enum-only FSM with `FiniteStateMachineBuilder<TContext, TState, TEvent>`. This new library supersedes it with a unified architecture spanning all three tiers, rich listener capabilities, guards, visualization, and a source generator.

---

## 1. Project Decomposition

All under `Net/FrenchExDev/FiniteStateMachine/`.

| # | Project | TFM | Purpose |
|---|---------|-----|---------|
| 1 | `FrenchExDev.Net.FiniteStateMachine` | `netstandard2.0;net10.0` | Core abstractions + engine + all 3 tiers (namespaced) + visualization. Depends on `FrenchExDev.Net.Result`. |
| 2 | `FrenchExDev.Net.FiniteStateMachine.Attributes` | `netstandard2.0;net10.0` | Marker attributes: `[StateMachine]`, `[Transition]` (enum SG) + `[RichStateMachine]`, `[State]`, `[Event]` (Rich SG) |
| 3 | `FrenchExDev.Net.FiniteStateMachine.SourceGenerator` | `netstandard2.0` | Enum-based `IIncrementalGenerator` (Roslyn component) |
| 4 | `FrenchExDev.Net.FiniteStateMachine.SourceGenerator.Lib` | `netstandard2.0` | Pure C# emission logic shared by both SGs (no Roslyn dep) |
| 5 | `FrenchExDev.Net.FiniteStateMachine.Design` | `netstandard2.0` | Rich-tier SG: reads `[RichStateMachine]`-decorated C# interfaces + `[State]`/`[Event]` records via Roslyn → generates visitors, Match, factory, serializer. Also supports `.fsm.yaml` via AdditionalFiles (with JSON Schema for VS Code completion). Depends on Roslyn + SourceGenerator.Lib + YamlDotNet (bundled). |
| 6 | `FrenchExDev.Net.FiniteStateMachine.Testing` | `netstandard2.0;net10.0` | Assertion helpers + model-based test path generation |
| 7 | `FrenchExDev.Net.FiniteStateMachine.Tests` | `net10.0` | xUnit tests |

### Dependency graph
```
FiniteStateMachine (core) ← FrenchExDev.Net.Result
  Attributes ← no deps
  SourceGenerator ← Roslyn + SourceGenerator.Lib
  SourceGenerator.Lib ← no deps (shared emission logic for both SGs)
  Design ← Roslyn + SourceGenerator.Lib + YamlDotNet (bundled for .fsm.yaml support)
  Testing ← core
  Tests ← core + Attributes + SG (Analyzer) + SG.Lib (Analyzer) + Design (Analyzer) + Testing
```

### Solution file
`FiniteStateMachine/FrenchExDev.Net.FiniteStateMachine.slnx` with `/src/` and `/test/` folders.

---

## 2. Core Library Architecture

### Namespace layout inside `FrenchExDev.Net.FiniteStateMachine`
```
FrenchExDev.Net.FiniteStateMachine           — core abstractions + shared engine
FrenchExDev.Net.FiniteStateMachine.Dynamic   — Tier 1: string-based builder + definition
FrenchExDev.Net.FiniteStateMachine.Typed     — Tier 2: enum-based builder + definition
FrenchExDev.Net.FiniteStateMachine.Rich      — Tier 3: interface-based builder + definition (states/events are user interfaces)
FrenchExDev.Net.FiniteStateMachine.Graph     — visualization (DOT, Mermaid export)
```

### 2.1 Core abstractions (shared across all tiers)

```csharp
// Key types:
Transition<TState>               — success payload: From, To, Event, IsReentrant
TransitionRecord<TState, TEvent> — history entry (from, event, to, timestamp)
TransitionDefinition<TState, TEvent> — immutable (source, event, target, guards, actions)

// Fire returns Result<Transition<TState>> using FrenchExDev.Net.Result:
//   Success → Result<Transition<TState>>.Success(transition)
//   Denied  → Result<Transition<TState>>.Failure("Guard rejected: ...")
//   No transition → Result<Transition<TState>>.Failure("No transition from X on Y")
// This composes with existing Map/Bind/Recover ecosystem.

// Key interfaces — ALL async, no sync variants:
IStateMachineDefinition<TState, TEvent>  — immutable graph: states, transitions, introspection
IStateMachine<TState, TEvent>            — running instance: CurrentState, FireAsync, CanFireAsync
IStateMachineListener<TState, TEvent>    — observer: OnTransitioningAsync, OnTransitionedAsync, OnStateEnteringAsync, etc.
IGuard<TState, TEvent>                   — Task<bool> EvaluateAsync(...)
ITransitionAction<TState, TEvent>        — Task ExecuteAsync(...)
IStateAction<TState, TEvent>             — Task ExecuteAsync(...)

// Engine:
StateMachineEngine<TState, TEvent>       — shared runtime, all tiers delegate here, fully async

// History is a listener, not built into the engine:
HistoryListener<TState, TEvent>          — opt-in via AddListenerAsync, ring buffer, configurable max
```

### 2.2 FireAsync lifecycle (in `StateMachineEngine`)
All steps are async. `CancellationToken` threaded through every call.
1. Find matching transitions for `(CurrentState, event)`
2. `await` guards in order — first allowed wins
3. If all denied → `await OnTransitionDeniedAsync` → return rejection
4. `await OnTransitioningAsync` (before)
5. `await` state exit actions + `await OnStateExitingAsync` / `await OnStateExitedAsync`
6. `await` transition actions
7. Update current state
8. `await` state entry actions + `await OnStateEnteringAsync` / `await OnStateEnteredAsync`
9. `await OnTransitionedAsync` (after)
10. History listener records (if attached)

### 2.3 Thread safety
Configurable via `ConcurrencyMode` enum:
- `None` — no sync (single-threaded)
- `Semaphore` — `SemaphoreSlim` for async-safe locking (recommended for multi-threaded use)

No `lock()`-based option since the entire API is async (`lock` + `await` is an anti-pattern).

### 2.4 Async-only API
Everything is `async Task`-returning. No sync overloads.
- `FireAsync(event, ct)` → `Task<Result<Transition<TState>>>`
- Guards: `Task<bool> EvaluateAsync(..., ct)`
- Actions: `Task ExecuteAsync(..., ct)`
- Listeners: `Task OnTransitioningAsync(..., ct)`, `Task OnTransitionedAsync(..., ct)`, etc.
- `ConfigureAwait(false)` throughout the engine.

---

## 3. Tier 1 — Dynamic FSM (string-based)

### API
```csharp
var definition = new DynamicStateMachineBuilder()
    .InitialState("Created")
    .FinalState("Delivered")
    .FinalState("Cancelled")
    .When("Created")
        .On("Submit").TransitionTo("Submitted")
            .WithGuard((from, evt, to) => hasItems ? Allow() : Deny("No items"))
            .WithAction((from, evt, to) => { /* side effect */ })
        .On("Cancel").TransitionTo("Cancelled")
    .When("Submitted")
        .On("Approve").TransitionTo("Approved")
        .On("Reject").TransitionTo("Created")
    .When("Approved")
        .On("Ship").TransitionTo("Shipped")
    .When("Shipped")
        .On("Deliver").TransitionTo("Delivered")
    .Build(); // validates graph, returns Result<DynamicStateMachineDefinition>

var machine = definition.Value.CreateMachine();
var result = await machine.FireAsync("Submit"); // Result<Transition<string>>
```

### Validation at `.Build()`
- Initial state is defined
- All transition targets exist as declared states
- Warns on unreachable states
- Warns on dead-end states not marked as final
- No duplicate `(from, event)` without guards

### JSON serialization
`DynamicStateMachineDefinition` is round-trippable to/from JSON via `System.Text.Json`:
```csharp
var json = definition.Value.ToJson();                      // serialize
var restored = DynamicStateMachineDefinition.FromJson(json); // deserialize
```
Enables config-driven workflows, persistence, and cross-process FSM definitions. Guards and actions are excluded from serialization (they're code — re-attach after deserialization via a `WithGuard`/`WithAction` overlay API).

---

## 4. Tier 2 — Typed FSM (enum-based)

### 4a. Fluent builder API (without SG)
```csharp
public enum DoorState { Closed, Open, Locked }
public enum DoorEvent { Open, Close, Lock, Unlock }

var definition = new TypedStateMachineBuilder<DoorState, DoorEvent>()
    .InitialState(DoorState.Closed)
    .When(DoorState.Closed)
        .On(DoorEvent.Open).TransitionTo(DoorState.Open)
        .On(DoorEvent.Lock).TransitionTo(DoorState.Locked)
    .When(DoorState.Open)
        .On(DoorEvent.Close).TransitionTo(DoorState.Closed)
    .When(DoorState.Locked)
        .On(DoorEvent.Unlock).TransitionTo(DoorState.Closed)
    .Build();

var machine = definition.Value.CreateMachine();
await machine.FireAsync(DoorEvent.Open);
```

### 4b. Source-generated API (see section 6)
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

// Usage:
var machine = new DoorStateMachine();
await machine.FireOpenAsync();       // strongly typed, async
await machine.CanFireLockAsync();    // strongly typed, async
await machine.PermittedEventsAsync(); // Task<IReadOnlyList<DoorEvent>>
```

### Internal optimization
`TypedStateMachineDefinition` uses array-indexed lookup: `table[stateInt][eventInt]` for O(1) transition resolution.

---

## 5. Tier 3 — Rich FSM (interface-based)

States and events are **interfaces**, so users get full IDE support: "Find All Implementations", "Go To Implementation", refactoring, navigation. Concrete states/events are classes or records implementing these interfaces.

### Core interfaces (in core library)
```csharp
public interface IState { string Name { get; } }
public interface IEvent { string Name { get; } }
```

### User-defined interfaces (per domain)
```csharp
// State interface — users can "Find All Implementations" to see every state
public interface IOrderState : IState { }

// Event interface — users can "Find All Implementations" to see every event
public interface IOrderEvent : IEvent { }
```

### Concrete states (records with data, implementing the interface)
```csharp
public record CreatedState() : IOrderState
{
    public string Name => "Created";
}

public record ShippedState(string TrackingNumber) : IOrderState
{
    public string Name => "Shipped";
}

public record DeliveredState(DateTime DeliveredAt) : IOrderState
{
    public string Name => "Delivered";
}
```

### Concrete events (records with payload, implementing the interface)
```csharp
public record SubmitEvent(List<OrderItem> Items) : IOrderEvent
{
    public string Name => "Submit";
}

public record ShipEvent(string TrackingNumber) : IOrderEvent
{
    public string Name => "Ship";
}

public record DeliverEvent() : IOrderEvent
{
    public string Name => "Deliver";
}
```

### Builder API
```csharp
var definition = new RichStateMachineBuilder<IOrderState, IOrderEvent>()
    .InitialState(new CreatedState())
    .FinalState<DeliveredState>()
    .When<CreatedState>()
        .On<SubmitEvent>()
            .TransitionTo((evt, current) => new SubmittedState(DateTime.UtcNow))
            .WithGuard((evt, current) => evt.Items.Count > 0
                ? Allow() : Deny("No items"))
    .When<ShippedState>()
        .On<DeliverEvent>()
            .TransitionTo((_, _) => new DeliveredState(DateTime.UtcNow))
    .Build();

var machine = definition.Value.CreateMachine();
await machine.FireAsync(new ShipEvent("TRACK-123"));
```

### Why interfaces (not abstract classes)
- **IDE navigation**: "Find All Implementations" on `IOrderState` lists every state — the primary navigation mechanism for understanding the FSM
- **Multiple inheritance**: a state can implement multiple interfaces (e.g., `IOrderState` + `IAuditable`)
- **No forced base class**: users choose records, classes, or structs freely
- **SOLID**: depend on abstractions, not concretions — guards/actions/listeners take `IOrderState`/`IOrderEvent`

### Key difference: computed target states
In Rich tier, `TransitionTo` takes a factory `Func<TEvent, TState, TState>` — the target state is computed from the event payload and current state. This enables events to carry data into the next state. The lambda receives the concrete event type (not `IOrderEvent`) thanks to the generic `When<T>/On<T>` constraints.

### Type matching
`When<CreatedState>()` matches via `is` check: `CurrentState is CreatedState`. Same for `On<SubmitEvent>()`.

---

## 6. Source Generator

### 6.1 Attributes (in `FrenchExDev.Net.FiniteStateMachine.Attributes`)

```csharp
[StateMachine(Type stateEnum, Type eventEnum)]
  - InitialState: string (required, enum member name)

[Transition(object from, object event, object to)]
  - Guard: string? (optional, name of partial method)
  - AllowMultiple = true
  - Applied to: static partial void DefineTransitions()

[TerminalState]  — on enum members, suppresses dead-end warnings
```

### 6.2 What the SG generates (for `DoorStateMachine`)

Generated class inherits `StateMachineBase<DoorState, DoorEvent>` (which provides `Fire`, listeners, thread safety, history).

**Transition table + core wiring:**
1. **Constructor** — `base(DoorState.Closed)`, second overload `DoorStateMachine(DoorState initialState)`
2. **`sealed override CreateTransitionTable()`** — pre-built table from `[Transition]` attributes
3. **`DefineTransitions()`** body — empty (anchor for attributes)

**Strongly-typed fire methods (all async):**
4. **`Fire{EventName}Async(CancellationToken ct = default)`** — one per event member, returns `Task<Result<Transition<TState>>>`
5. **`CanFire{EventName}Async(CancellationToken ct = default)`** — one per event member, returns `Task<bool>` (evaluates guards)
6. **`PermittedEventsAsync(CancellationToken ct = default)`** — `Task<IReadOnlyList<TEvent>>`

**State lifecycle hooks (virtual, async, overridable):**
7. **`virtual OnEntry{StateName}Async(CancellationToken ct)`** — one per state member, returns `Task`
8. **`virtual OnExit{StateName}Async(CancellationToken ct)`** — one per state member, returns `Task`
9. **`sealed override OnEntryAsync(TState, CancellationToken)`** — switch dispatching to typed virtuals
10. **`sealed override OnExitAsync(TState, CancellationToken)`** — switch dispatching to typed virtuals

**Transition-specific hooks (virtual, async, overridable):**
11. **`virtual OnTransition{FromState}To{ToState}Async(CancellationToken ct)`** — one per defined transition (e.g., `OnTransitionClosedToOpenAsync()`)
12. **`sealed override OnTransitionAsync(TState, TEvent, TState, CancellationToken)`** — switch dispatching to typed transition virtuals

**Guards (async):**
13. **`sealed override EvaluateGuardAsync(TState, TEvent, TState, CancellationToken)`** — dispatches to user's partial async guard methods (only if any transition has `Guard` set)

**Graph + visualization:**
14. **`static Graph`** property — `StateGraph<TState, TEvent>` with `.ToMermaid()` / `.ToDot()`
15. **`const string MermaidGraph`** — compile-time Mermaid string constant (zero allocation)
16. **`const string DotGraph`** — compile-time DOT string constant
17. **`static IReadOnlyList<DoorEvent> PermittedEventsFrom(DoorState state)`** — static graph introspection (no guards, no instance needed)

**Utility members:**
18. **`override string ToString()`** — `"DoorStateMachine { State = Closed }"`
19. **`ResetAsync(CancellationToken ct = default)`** — return to initial state (fires exit on current + entry on initial)
20. **`DoorStateMachine Clone()`** — copy at same current state

### 6.3 Compile-time diagnostics

| ID | Severity | Condition |
|----|----------|-----------|
| FSM001 | Error | `[StateMachine]` on non-partial class |
| FSM002 | Error | `InitialState` not a valid enum member |
| FSM003 | Error | State/event type is not an enum |
| FSM004 | Warning | Unreachable state (no incoming transitions, not initial) |
| FSM005 | Warning | Dead-end state (no outgoing, not `[TerminalState]`) |
| FSM006 | Error | Duplicate `(from, event)` without guards |
| FSM007 | Error | Guard method not found on class |
| FSM008 | Warning | Event member has no transitions |
| FSM009 | Error | `InitialState` not set |
| FSM010 | Error | No `DefineTransitions()` partial method found |

### 6.4 SourceGenerator.Lib emission

```csharp
// Roslyn-free models:
StateMachineEmitModel    — Namespace, ClassName, StateEnumFull, EventEnumFull, InitialState, StateMembers, EventMembers, Transitions
TransitionEmitModel      — FromMember, EventMember, ToMember, GuardMethod?

// Emitter:
StateMachineEmitter.Emit(StateMachineEmitModel) → string  // full generated file
```

---

## 7. Cross-Cutting Features

### 7.1 Listeners (fully async)
```csharp
public interface IStateMachineListener<TState, TEvent>
{
    // Before/after transition
    Task OnTransitioningAsync(TState from, TEvent @event, TState to, CancellationToken ct);
    Task OnTransitionedAsync(TState from, TEvent @event, TState to, CancellationToken ct);
    // State lifecycle
    Task OnStateEnteringAsync(TState state, TEvent? triggeringEvent, CancellationToken ct);
    Task OnStateEnteredAsync(TState state, TEvent? triggeringEvent, CancellationToken ct);
    Task OnStateExitingAsync(TState state, TEvent @event, CancellationToken ct);
    Task OnStateExitedAsync(TState state, TEvent @event, CancellationToken ct);
    // Failures
    Task OnTransitionDeniedAsync(TState from, TEvent @event, string reason, CancellationToken ct);
    Task OnErrorAsync(Exception exception, TState state, CancellationToken ct);
}
```
- DIMs (default `=> Task.CompletedTask`) on `net10.0`; `StateMachineListenerBase<>` abstract class on `netstandard2.0`
- Global listeners (on the machine) + per-state listeners (on the builder's `.When()`)

### 7.2 Guards (fully async)
- `IGuard<TState, TEvent>` — `Task<bool> EvaluateAsync(from, event, to, ct)`
- Lambda overloads: `Func<TState, TEvent, TState, CancellationToken, Task<bool>>`
- Return `Allow()` / `Deny(reason)` convenience
- Short-circuit: first denial stops evaluation

### 7.3 Graph introspection + visualization
```csharp
// On IStateMachineDefinition:
GetPermittedEvents(TState from)   → IReadOnlyList<TEvent>
GetReachableStates(TState from)   → IReadOnlySet<TState>
CanFire(TState from, TEvent evt)  → bool

// StateGraph<TState, TEvent>:
AllStates, AllEvents, UnreachableStates, DeadEndStates
ToDot(DotOptions?)       → string
ToMermaid(MermaidOptions?) → string
```

### 7.4 History tracking
Optional, enabled on machine creation:
```csharp
definition.CreateMachine(trackHistory: true, maxRecords: 100);
machine.History → IReadOnlyList<TransitionRecord<TState, TEvent>>
```

### 7.5 Re-entrant & internal transitions
- Re-entrant: `TransitionTo` same state — triggers exit + entry
- Internal: `.InternalTransition()` — executes action but NO exit/entry

### 7.6 Model-based test path generation (in Testing project)
```csharp
// StateMachinePathGenerator enumerates all paths from initial to terminal states
var paths = StateMachinePathGenerator.AllPaths(definition);
// Each path is a sequence of (state, event) pairs

// Use with xUnit [Theory]:
public static IEnumerable<object[]> AllPaths =>
    StateMachinePathGenerator.AllPaths(DoorStateMachine.Graph)
        .Select(path => new object[] { path });

[Theory]
[MemberData(nameof(AllPaths))]
public void AllPathsReachTerminalState(StateMachinePath<DoorState, DoorEvent> path)
{
    var machine = new DoorStateMachine();
    foreach (var step in path.Steps)
    {
        var result = await machine.FireAsync(step.Event);
        Assert.True(result.IsSuccess);
    }
    Assert.Contains(machine.CurrentState, path.TerminalStates);
}
```

Features:
- BFS/DFS traversal with cycle detection (configurable max depth)
- `AllPaths(definition)` — all paths from initial to any terminal state
- `AllPaths(definition, targetState)` — all paths to a specific state
- `ShortestPath(definition, from, to)` — shortest path between two states
- `RandomWalk(definition, maxSteps)` — random exploration for fuzz testing

---

## 8. File Layout

```
FiniteStateMachine/
  FrenchExDev.Net.FiniteStateMachine.slnx
  doc/PLAN.md
  src/
    FrenchExDev.Net.FiniteStateMachine/
      Core/
        IStateMachineDefinition.cs
        IStateMachine.cs
        IGuard.cs, ITransitionAction.cs, IStateAction.cs
        IStateMachineListener.cs, StateMachineListenerBase.cs
        StateMachineBase.cs (abstract base — Fire lifecycle, listeners, thread safety)
        StateMachineEngine.cs
        Transition.cs (success payload: From, To, Event, IsReentrant)
        TransitionDefinition.cs, TransitionRecord.cs
        HistoryListener.cs (opt-in history tracking as a listener, ring buffer)
        ConcurrencyMode.cs
        HierarchyDefinition.cs (parent/child relationships, history states)
        ICompositeStateMachine.cs, IRegion.cs (parallel regions)
        IInvokedService.cs (long-running services tied to state lifecycle)
        DeferredEventQueue.cs (event buffering for deferred events)
        TimerTransition.cs (timeout-based auto-transitions)
      Dynamic/
        DynamicStateMachineBuilder.cs
        DynamicStateMachineDefinition.cs
        DynamicStateMachineSerializer.cs
        DynamicWhenBuilder.cs, DynamicOnBuilder.cs
      Typed/
        TypedStateMachineBuilder.cs
        TypedStateMachineDefinition.cs
        TransitionTable.cs
        TypedWhenBuilder.cs, TypedOnBuilder.cs
      Rich/
        IState.cs, IEvent.cs  (core marker interfaces)
        RichStateMachineBuilder.cs
        RichStateMachineDefinition.cs
        RichWhenBuilder.cs, RichOnBuilder.cs
      Graph/
        StateGraph.cs
        MermaidExporter.cs, DotExporter.cs
        MermaidOptions.cs, DotOptions.cs
    FrenchExDev.Net.FiniteStateMachine.Attributes/
      StateMachineAttribute.cs
      TransitionAttribute.cs
      TerminalStateAttribute.cs
    FrenchExDev.Net.FiniteStateMachine.SourceGenerator/
      FiniteStateMachineGenerator.cs
      Diagnostics.cs
    FrenchExDev.Net.FiniteStateMachine.SourceGenerator.Lib/
      StateMachineEmitModel.cs
      TransitionEmitModel.cs
      StateMachineEmitter.cs
      RichEmitModel.cs
      RichEmitter.cs (visitor, Match, state/event records, serializer)
      MermaidEmitter.cs, DotEmitter.cs
    FrenchExDev.Net.FiniteStateMachine.Design/
      RichStateMachineGenerator.cs (IIncrementalGenerator, reads [RichStateMachine] attributes)
      FsmYamlGenerator.cs (IIncrementalGenerator, reads .fsm.yaml AdditionalFiles)
      FsmYamlParser.cs (YAML → RichEmitModel)
      FsmYamlSchema.cs (deserialization model for the YAML)
      DesignDiagnostics.cs
      fsm.schema.json (JSON Schema for VS Code YAML IntelliSense)
    FrenchExDev.Net.FiniteStateMachine.Testing/
      StateMachineAssert.cs
      StateMachinePathGenerator.cs
    FrenchExDev.Net.FiniteStateMachine.Tests/
      Dynamic/ (DynamicBuilderTests, DynamicFireTests, DynamicGuardTests, DynamicListenerTests)
      Typed/ (TypedBuilderTests, TypedFireTests, TypedGuardTests)
      Rich/ (RichBuilderTests, RichFireTests, RichEventPayloadTests)
      SourceGenerator/ (GenerationTests, DiagnosticTests)
      Engine/ (ConcurrencyTests, HistoryTests, IntrospectionTests)
      Graph/ (MermaidTests, DotTests)
  test/
    FrenchExDev.Net.FiniteStateMachine.Tests/
```

---

## 9. Implementation Phases

### Phase 0: Project scaffolding
1. Write this plan to `FiniteStateMachine/doc/PLAN.md` (the canonical location per project convention)
2. Create `FrenchExDev.Net.FiniteStateMachine.slnx` solution file
3. Create all `.csproj` files with correct TFMs, dependencies, and project references
4. Create empty directory structure (`src/`, `test/`, project folders)

### Phase 1: Core + Typed (enum-based, no SG)
1. Core interfaces (`IStateMachineDefinition`, `IStateMachine`, `IGuard`, `ITransitionAction`, `IStateAction`)
2. `Transition<TState>` (success payload), `TransitionRecord`, `TransitionDefinition`, `ConcurrencyMode`
3. `Fire()` returns `Result<Transition<TState>>` using `FrenchExDev.Net.Result` (composes with Map/Bind/Recover)
4. `StateMachineBase<TState, TEvent>` abstract class (Fire lifecycle, listeners, thread safety)
5. `TransitionTable` (array-indexed for enum performance)
6. `TypedStateMachineBuilder` with `When/On/TransitionTo` fluent API
7. `TypedStateMachineDefinition`
8. Listeners (`IStateMachineListener`, `StateMachineListenerBase`, `IAsyncStateMachineListener`)
9. `HistoryListener<TState, TEvent>` — opt-in history tracking as a listener (ring buffer, configurable max)
10. Guards (interface + lambda wrappers) + transition actions + state entry/exit actions
11. Thread safety (`ConcurrencyMode.None/Lock/Semaphore`)
12. Internal transitions (`.InternalTransition()` — action but no exit/entry)
13. Tests for Typed tier

### Phase 2: Source Generator (enum-based)
1. Attributes project (`StateMachineAttribute`, `TransitionAttribute`, `TerminalStateAttribute`)
2. `StateMachineEmitModel` + `TransitionEmitModel` in SG.Lib
3. `StateMachineEmitter.Emit()` in SG.Lib — generates all 21 member categories:
   - Transition table + constructor(s)
   - `Fire{Event}Async(ct)` + `CanFire{Event}Async(ct)` + `PermittedEventsAsync(ct)`
   - `OnEntry{State}Async(ct)` / `OnExit{State}Async(ct)` virtuals + dispatchers
   - `OnTransition{From}To{To}Async(ct)` virtuals + dispatcher
   - Guard dispatch
   - `static Graph`, `const MermaidGraph`, `const DotGraph`, `static PermittedEventsFrom()`
   - `ToString()`, `Reset()`, `Clone()`
4. `FiniteStateMachineGenerator` incremental generator (ForAttributeWithMetadataName)
5. Compile-time diagnostics (FSM001–FSM010)
6. SG tests (generation output verification + diagnostic verification)

### Phase 3: Dynamic (string-based)
1. `DynamicStateMachineBuilder` + fluent API
2. `DynamicStateMachineDefinition`
3. Validation at `.Build()` (graph validation via `Result<T>`)
4. JSON serialization (`ToJson()` / `FromJson()`) for persistence/config-driven workflows
5. Tests

### Phase 4: Rich (interface-based)
1. `IState`, `IEvent` core marker interfaces
2. `RichStateMachineBuilder<TState, TEvent>` where `TState : IState` and `TEvent : IEvent`
3. Type-discriminated `When<T>/On<T>` matching via `is` checks
4. Computed target states (`TransitionTo(Func<TConcreteEvent, TState, TState>)`)
5. `RichStateMachineDefinition`
6. Tests (users define `IOrderState`/`IOrderEvent` interfaces + record implementations)

### Phase 5: Graph + Visualization
1. `StateGraph` (introspection: reachable, dead-ends, unreachable)
2. `MermaidExporter` + `DotExporter` with options
3. `MermaidEmitter` + `DotEmitter` in SG.Lib (compile-time string generation)
4. Wire into enum SG (`static Graph`, `const MermaidGraph`, `const DotGraph`)
5. Tests

### Phase 6: Rich Design SG (dual input)
1. `RichEmitModel` in SG.Lib (state/event/transition definitions — shared by both input modes)
2. `RichEmitter` in SG.Lib — generates:
   - Visitor interfaces (`IXxxStateVisitor<TResult>`, `IXxxStateVisitor`)
   - `Accept()` methods on each state/event record
   - Functional `Match<TResult>()` extension methods (lambda-based exhaustive matching)
   - Pre-wired `XxxStateMachineFactory` + definition
   - `XxxStateSerializer` (JSON polymorphic serialization)
   - Compile-time `MermaidGraph` / `DotGraph` constants
   - For YAML input only: also generates state/event interfaces + records from scratch
3. **C# attribute input** (`RichStateMachineGenerator`):
   - Reads `[RichStateMachine]` on interfaces, `[State]`/`[Event]`/`[Transition]` on records
   - Extracts `RichEmitModel` from Roslyn symbols
   - Generates onto existing `partial` types (Accept, Name) + new types (visitor, Match, factory)
4. **YAML input** (`FsmYamlGenerator`):
   - Reads `.fsm.yaml` `AdditionalFiles`
   - `FsmYamlParser` → `RichEmitModel`
   - Generates ALL types (interfaces, records, visitor, Match, factory, serializer)
5. `fsm.schema.json` — JSON Schema for VS Code YAML IntelliSense
6. Design-specific diagnostics (missing initial state, undefined refs, invalid YAML, etc.)
7. Tests (C# attribute input + YAML input → verify identical output structure)

### Phase 7: Hierarchical States
1. `HierarchyDefinition` (parent/child, initial sub-state, history mode)
2. Engine support: LCA-based exit/entry chain, parent transition fallback
3. `IsInState(current, query)` — true if current is query or nested within query
4. History states (shallow: remember direct child; deep: remember leaf)
5. Builder API: `.WithParentState().WithSubState()` (all tiers)
6. YAML support: `substates:` + `initial:` within a state
7. Visualization: Mermaid `state Parent { ... }` nested syntax
8. Tests

### Phase 8: Parallel Regions
1. `IRegion<TState, TEvent>`, `ICompositeStateMachine<TState, TEvent>`
2. `CompositeStateMachineEngine` — manages multiple region machines
3. `FireAll(event)` — broadcast to all regions
4. `AllRegionsTerminal` — completion detection
5. Builder API: `.WithRegion("name", region => ...)` (all tiers)
6. YAML support: `regions:` top-level key
7. Visualization: Mermaid parallel notation
8. Tests

### Phase 9: Invoked Services + Timers + Deferred Events
1. `IInvokedService<TState, TEvent>` — start on enter, cancel on exit, auto-fire done/error events
2. `TimerTransition` — `Task.Delay`-based, cancel on exit, auto-fire timeout event
3. `DeferredEventQueue` — FIFO queue, replay on state change, cycle protection
4. Builder API: `.Invoke()`, `.After(TimeSpan)`, `.Defer(event)` (all tiers)
5. YAML support: `invoke:`, `after:`, `deferred:` in state definitions
6. Tests

### Phase 10: Testing helpers + polish
1. `StateMachineAssert` — transition assertions, path verification
2. `StateMachinePathGenerator` — BFS/DFS path enumeration for model-based testing
   - `AllPaths(definition)` → all paths initial→terminal
   - `ShortestPath(definition, from, to)`
   - `RandomWalk(definition, maxSteps)` for fuzz testing
   - xUnit `[Theory]` + `[MemberData]` integration
3. Full coverage pass

### Phase 11: Quality Gates
1. Add `quality-gate.yml` at `FiniteStateMachine/`:
   ```yaml
   solution: FrenchExDev.Net.FiniteStateMachine.slnx

   coverage:
     - "**/coverage.cobertura.xml"

   mutations:
     - "**/mutation-report.json"

   output: .quality-gate/

   gates:
     max-cyclomatic-complexity: 15
     max-cognitive-complexity: 20
     max-class-coupling: 55
     max-inheritance-depth: 5
     min-maintainability-index: 55
     max-lcom: 15
     max-distance-from-main-sequence: 1.0
     max-duplication-percent: 5
     min-test-quality-score: 0.95
   ```
2. Add `coverage.runsettings` at `FiniteStateMachine/`:
   ```xml
   <Include>
     [FrenchExDev.Net.FiniteStateMachine]*
   </Include>
   <ExcludeByAttribute>
     ExcludeFromCodeCoverageAttribute,CompilerGeneratedAttribute
   </ExcludeByAttribute>
   ```
   Excludes SG-generated code from coverage (CompilerGeneratedAttribute).
3. Run quality gate: `dotnet quality-gate test --config FiniteStateMachine/quality-gate.yml --settings FiniteStateMachine/coverage.runsettings`
4. Target: 100% line/branch coverage on core library, all gates green
5. Fix any violations (cyclomatic complexity in engine, coupling in builders, etc.)

---

## 10. Verification

**Build + tests:**
- `dotnet build FiniteStateMachine/FrenchExDev.Net.FiniteStateMachine.slnx`
- `dotnet test FiniteStateMachine/FrenchExDev.Net.FiniteStateMachine.slnx`

**Per-tier verification:**
- Dynamic: build + fire + guards + JSON round-trip (`ToJson()` → `FromJson()` → fire again)
- Typed: build + fire + guards + listeners + history
- Rich: build with interfaces + fire with record payloads + computed target states
- Each tier tested with a Door FSM (simple) and Order FSM (complex) scenario

**Enum SG verification:**
- `EmitCompilerGeneratedFiles` to inspect generated code
- Diagnostics: intentionally trigger each FSM001–FSM010
- `Fire{Event}Async(ct)` returns `Task<Result<Transition<TState>>>`
- Transition-specific hooks (`OnTransitionClosedToOpenAsync`)
- Compile-time `const MermaidGraph` / `const DotGraph` match runtime output
- `ToString()`, `Reset()`, `Clone()`

**Rich Design SG verification (C# attributes input):**
- `[RichStateMachine]` on interface + `[State]`/`[Event]` on records → generates visitor, Match, factory
- Visitor: compile error when a `Visit` method is missing (exhaustive check)
- `Match<TResult>()` compiles and returns correctly for each state
- `OrderStateSerializer` JSON round-trip (polymorphic)
- Generated factory creates a working machine

**Rich Design SG verification (`.fsm.yaml` input):**
- Parse a non-trivial YAML with hierarchy, regions, timers, deferred events
- JSON Schema provides VS Code IntelliSense for YAML structure
- Generated types match equivalent C# attribute output

**Advanced features:**
- Hierarchical: fire event on sub-state, verify parent transition applies; verify LCA exit/entry chain
- Parallel regions: fire in multiple regions independently; verify `AllRegionsTerminal`
- Invoked services: enter state → service starts; exit state → service cancelled; service done → auto-transition
- Timers: enter state → timer starts; exit before timeout → timer cancelled; timeout → auto-transition
- Deferred events: fire deferred event → queued; transition → replayed; verify FIFO order
- Model-based testing: `AllPaths` covers all reachable terminal states through Order FSM

---

## 11. Rich Design SG (`FrenchExDev.Net.FiniteStateMachine.Design`)

A separate Roslyn component project with **two input modes** for the Rich tier:

**Primary: C# attribute-decorated types** (type-safe, full IDE support)
- Users write real `partial` interfaces + records with `[RichStateMachine]`, `[State]`, `[Event]`, `[Transition]` attributes
- Full IntelliSense, refactoring, go-to-definition, Find All Implementations
- SG generates boilerplate onto the partial types (visitor, Match, Accept, factory, serializer)

**Secondary: `.fsm.yaml` files** (config-driven, external scenarios)
- Read as `AdditionalFiles`, generates entire type hierarchy from scratch
- Ships a **JSON Schema** (`fsm.schema.json`) for VS Code IntelliSense on the YAML structure
- Useful for: loading FSM definitions from databases, APIs, or non-C# tooling
- Limitation: C# type references in data/payload are strings (no IntelliSense for those)

Both modes produce a `RichEmitModel` in SG.Lib → shared `RichEmitter` generates the same output.

### 11.1 C# attribute input (primary)

```csharp
using FrenchExDev.Net.FiniteStateMachine.Attributes;

// State interface — decorated with [RichStateMachine]
[RichStateMachine(InitialState = typeof(CreatedState))]
public partial interface IOrderState : IState { }

// Event interface
[RichStateMachineEvents(StateMachine = typeof(IOrderState))]
public partial interface IOrderEvent : IEvent { }

// States — real records, full type safety, IDE autocomplete
[State]
public partial record CreatedState() : IOrderState;

[State]
public partial record SubmittedState(DateTime SubmittedAt) : IOrderState;

[State]
public partial record ApprovedState(string ApprovedBy) : IOrderState;

[State]
public partial record ShippedState(string TrackingNumber) : IOrderState;

[State(Terminal = true)]
public partial record DeliveredState(DateTime DeliveredAt) : IOrderState;

[State(Terminal = true)]
public partial record CancelledState(string Reason) : IOrderState;

// Events — real records with typed payloads + [Transition] attributes
[Event]
[Transition(typeof(CreatedState), typeof(SubmittedState))]
public partial record SubmitEvent(List<OrderItem> Items) : IOrderEvent;

[Event]
[Transition(typeof(SubmittedState), typeof(ApprovedState))]
public partial record ApproveEvent(string ApprovedBy) : IOrderEvent;

[Event]
[Transition(typeof(ApprovedState), typeof(ShippedState))]
public partial record ShipEvent(string TrackingNumber) : IOrderEvent;

[Event]
[Transition(typeof(ShippedState), typeof(DeliveredState))]
public partial record DeliverEvent() : IOrderEvent;

[Event]
[Transition(typeof(CreatedState), typeof(CancelledState))]
[Transition(typeof(SubmittedState), typeof(CancelledState))]
[Transition(typeof(ApprovedState), typeof(CancelledState))]
public partial record CancelEvent(string Reason) : IOrderEvent;
```

**What the SG generates onto the partial types:**

On `IOrderState`:
- `TResult Accept<TResult>(IOrderStateVisitor<TResult> visitor)`
- `void Accept(IOrderStateVisitor visitor)`

On each state record (e.g., `CreatedState`):
- `public string Name => "Created";` (from type name)
- `public TResult Accept<TResult>(...) => visitor.Visit(this);`
- `public void Accept(...) => visitor.Visit(this);`

As new types:
- `IOrderStateVisitor<out TResult>` — one `Visit(T)` per state
- `IOrderStateVisitor` — void variant
- `IOrderEventVisitor<out TResult>` + `IOrderEventVisitor`
- `OrderStateExtensions.Match<TResult>(...)` — exhaustive lambda matching
- `OrderEventExtensions.Match<TResult>(...)`
- `OrderStateMachineFactory.CreateDefinition()` + `.Create()`
- `OrderStateMachineGraph` — `const MermaidGraph`, `const DotGraph`
- `OrderStateSerializer.Serialize(IOrderState)` / `.Deserialize(string)`

**Attributes (in `FrenchExDev.Net.FiniteStateMachine.Attributes`):**

```csharp
[RichStateMachine(InitialState = typeof(...))]   // on interface : IState
[RichStateMachineEvents(StateMachine = typeof(...))]  // on interface : IEvent
[State]                          // on record : IXxxState
[State(Terminal = true)]         // marks as terminal/final state
[Event]                          // on record : IXxxEvent
[Transition(typeof(From), typeof(To))]  // on event record, AllowMultiple = true
```

### 11.2 YAML input (secondary)

```yaml
name: Order
namespace: MyApp.Orders

states:
  Created: {}
  Submitted:
    data: { SubmittedAt: DateTime }
  Approved:
    data: { ApprovedBy: string }
  Shipped:
    data: { TrackingNumber: string }
  Delivered:
    data: { DeliveredAt: DateTime }
    terminal: true
  Cancelled:
    data: { Reason: string }
    terminal: true

  # Hierarchical states — substates nested inside parent
  Processing:
    substates:
      Validating: {}
      Executing:
        data: { Progress: int }
    initial: Validating

  # State with timeout
  WaitingForApproval:
    after:
      duration: "00:30:00"
      transitionTo: Cancelled

  # State with invoked service
  Polling:
    invoke:
      id: pollService
      onDone: Completed
      onError: Failed

events:
  Submit:
    payload: { Items: "List<OrderItem>" }
  Approve:
    payload: { ApprovedBy: string }
  Ship:
    payload: { TrackingNumber: string }
  Deliver: {}
  Cancel:
    payload: { Reason: string }
  Timeout: {}           # auto-generated for timer transitions
  ServiceDone: {}       # auto-generated for invoke onDone
  ServiceError:
    payload: { Error: Exception }

transitions:
  - { from: Created, on: Submit, to: Submitted }
  - { from: Submitted, on: Approve, to: Approved }
  - { from: Submitted, on: Cancel, to: Cancelled }
  - { from: Approved, on: Ship, to: Shipped }
  - { from: Shipped, on: Deliver, to: Delivered }
  - { from: [Created, Submitted, Approved], on: Cancel, to: Cancelled }
  # Hierarchical: events on parent apply to all substates
  - { from: Processing, on: Cancel, to: Cancelled }
  - { from: Processing.Validating, on: Validated, to: Processing.Executing }

initial: Created

# Parallel regions (orthogonal state machines running concurrently)
regions:
  payment:
    initial: Pending
    states:
      Pending: {}
      Paid: { terminal: true }
      Refunded: { terminal: true }
    events:
      Pay: {}
      Refund: {}
    transitions:
      - { from: Pending, on: Pay, to: Paid }
      - { from: Paid, on: Refund, to: Refunded }
  fulfillment:
    initial: Unfulfilled
    states:
      Unfulfilled: {}
      Shipped: { data: { TrackingNumber: string } }
      Delivered: { terminal: true }
    events:
      ShipFulfillment: { payload: { TrackingNumber: string } }
      DeliverFulfillment: {}
    transitions:
      - { from: Unfulfilled, on: ShipFulfillment, to: Shipped }
      - { from: Shipped, on: DeliverFulfillment, to: Delivered }

# Deferred events — queued when received in states that don't handle them
deferred:
  - { event: Approve, in: [Created] }  # queued if received while in Created, replayed when leaving
```

### 11.3 YAML generates ALL types (interface, records, visitor, Match)

Unlike C# attribute input where users write the types and the SG adds boilerplate, YAML input generates **everything from scratch** — the full type hierarchy, visitor, Match, factory, serializer. The output is identical to what C# attribute input produces, but users don't write any C# types themselves.

### 11.4 JSON Schema for VS Code IntelliSense

The Design project ships `fsm.schema.json` as a NuGet content file. Users reference it in `.vscode/settings.json`:
```json
{
  "yaml.schemas": {
    "./node_modules/.../fsm.schema.json": "*.fsm.yaml"
  }
}
```
Or via a YAML comment: `# yaml-language-server: $schema=./fsm.schema.json`

The schema provides:
- Property name autocomplete (states, events, transitions, initial, terminal, etc.)
- Required field validation
- Structure validation (correct nesting of substates, regions, etc.)
- Enum values for known keywords
- Does NOT provide C# type name autocomplete (those remain strings)

### 11.5 Usage example

```csharp
// C# attribute input (type-safe):
var machine = OrderStateMachineFactory.Create();

// Or YAML input (in .csproj: <AdditionalFiles Include="Order.fsm.yaml" />)
// Same factory is generated from the YAML:

// Usage — same regardless of input mode:
var machine = OrderStateMachineFactory.Create();
await machine.FireAsync(new SubmitEvent(items));

// Exhaustive matching:
var label = machine.CurrentState.Match(
    created:   s => "New order",
    submitted: s => $"Submitted at {s.SubmittedAt}",
    approved:  s => $"Approved by {s.ApprovedBy}",
    shipped:   s => $"Tracking: {s.TrackingNumber}",
    delivered: s => $"Delivered at {s.DeliveredAt}",
    cancelled: s => $"Cancelled: {s.Reason}"
);

// Visitor pattern — compile error if you forget a state:
class OrderStateRenderer : IOrderStateVisitor<string>
{
    public string Visit(CreatedState state) => "🆕 New";
    public string Visit(SubmittedState state) => $"📤 Submitted {state.SubmittedAt}";
    public string Visit(ApprovedState state) => $"✅ Approved by {state.ApprovedBy}";
    public string Visit(ShippedState state) => $"📦 {state.TrackingNumber}";
    public string Visit(DeliveredState state) => $"🏠 {state.DeliveredAt}";
    public string Visit(CancelledState state) => $"❌ {state.Reason}";
}
```

---

## 12. Hierarchical States (Statecharts)

Supported in all tiers (core engine feature).

### 12.1 Concept
A parent state contains substates. When in a substate, the machine is also "in" the parent state. Transitions on the parent apply to all substates (unless overridden).

### 12.2 Engine behavior
- **Exit**: when leaving a substate, exit actions fire bottom-up from the current substate to the LCA (Least Common Ancestor)
- **Enter**: when entering a substate, entry actions fire top-down from the LCA to the target substate
- **Parent transitions**: if no transition matches `(substate, event)`, the engine checks the parent state, then grandparent, etc.
- **History states**: when re-entering a parent, optionally resume at the last active substate (shallow history) or deep substate (deep history)

### 12.3 API (Typed tier)
```csharp
builder
    .WithParentState(ProcessingState.Processing)
        .WithSubState(ProcessingState.Validating, isInitial: true)
        .WithSubState(ProcessingState.Executing)
    .When(ProcessingState.Validating)
        .On(ProcessingEvent.Validated).TransitionTo(ProcessingState.Executing)
    .When(ProcessingState.Processing) // applies to ALL substates
        .On(ProcessingEvent.Cancel).TransitionTo(ProcessingState.Cancelled)
```

### 12.4 Core types
```csharp
// In IStateMachineDefinition:
TState? GetParentState(TState state);
IReadOnlyList<TState> GetSubStates(TState state);
TState? GetInitialSubState(TState parentState);
bool IsInState(TState current, TState query); // true if current == query or current is a substate of query
```

---

## 13. Parallel Regions (Orthogonal States)

### 13.1 Concept
Multiple independent FSMs running concurrently within a parent state. Each region has its own current state, transitions, and lifecycle. The composite machine is "done" when all regions reach terminal states.

### 13.2 Core types
```csharp
public interface IRegion<TState, TEvent>
{
    string Name { get; }
    IStateMachine<TState, TEvent> Machine { get; }
}

public interface ICompositeStateMachine<TState, TEvent>
{
    IReadOnlyList<IRegion<TState, TEvent>> Regions { get; }
    bool AllRegionsTerminal { get; }
    void FireAll(TEvent @event); // broadcast to all regions
}
```

### 13.3 API (Dynamic tier)
```csharp
var definition = new DynamicStateMachineBuilder()
    .InitialState("Active")
    .WithRegion("payment", region => region
        .InitialState("Pending")
        .FinalState("Paid")
        .When("Pending").On("Pay").TransitionTo("Paid"))
    .WithRegion("shipping", region => region
        .InitialState("NotShipped")
        .FinalState("Delivered")
        .When("NotShipped").On("Ship").TransitionTo("Shipped")
        .When("Shipped").On("Deliver").TransitionTo("Delivered"))
    .Build();
```

---

## 14. Invoked Services / Activities

Long-running async operations tied to a state's lifecycle.

### 14.1 Concept
When entering a state, a service is started. When exiting that state, the service is cancelled. The service can complete (triggering an `onDone` transition) or fail (triggering an `onError` transition).

### 14.2 Core types
```csharp
public interface IInvokedService<TState, TEvent>
{
    string Id { get; }
    Task ExecuteAsync(CancellationToken ct);
    TEvent DoneEvent { get; }
    TEvent ErrorEvent { get; }
}
```

### 14.3 API
```csharp
builder
    .When(OrderState.Processing)
        .Invoke(async ct =>
        {
            await PollExternalApiAsync(ct);
        },
        onDone: OrderEvent.ProcessingComplete,
        onError: OrderEvent.ProcessingFailed)
```

### 14.4 Engine behavior
- On state entry: start the service task with a `CancellationTokenSource`
- On state exit: cancel the token source
- On service completion: auto-fire the `DoneEvent`
- On service exception: auto-fire the `ErrorEvent`

---

## 15. Deferred Events

### 15.1 Concept
Events that arrive in a state that doesn't handle them are queued (deferred) instead of rejected. When the machine transitions to a state that handles the event, deferred events are replayed in FIFO order.

### 15.2 API
```csharp
builder
    .When(OrderState.Created)
        .Defer(OrderEvent.Approve) // queue Approve if received in Created
    .When(OrderState.Submitted)
        .On(OrderEvent.Approve).TransitionTo(OrderState.Approved) // replayed here
```

### 15.3 Engine behavior
- `Fire(event)` in a state that defers it → event goes to internal queue, `TransitionResult.IsDeferred = true`
- On state transition → check deferred queue → replay matching events in order
- Cycle protection: max replay depth to prevent infinite loops

---

## 16. Timers / Timeout Transitions

### 16.1 Concept
A state can define an automatic transition after a duration. If the machine leaves the state before the timer fires, the timer is cancelled.

### 16.2 API
```csharp
builder
    .When(OrderState.WaitingForApproval)
        .After(TimeSpan.FromMinutes(30))
        .TransitionTo(OrderState.TimedOut)
```

### 16.3 Engine behavior
- On state entry: if the state has a timer, start a `Task.Delay` with a `CancellationTokenSource`
- On state exit: cancel the timer
- On timer fire: auto-fire an internal timeout event → transition
- Thread-safe: timer fires through the same `Fire` path (respects `ConcurrencyMode`)
