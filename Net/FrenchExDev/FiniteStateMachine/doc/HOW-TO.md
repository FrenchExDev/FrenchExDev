# How-To Guide

## Choose a Tier

| Use case | Tier | Why |
|----------|------|-----|
| Config-driven workflows, rule engines, user-defined FSMs | **Dynamic** | States/events are strings — load from JSON, database, or user input |
| Protocol implementations, well-defined state machines | **Typed** | Enum-based — compile-time checking, O(1) lookup, optional source generation |
| Rich domain models, event sourcing, CQRS | **Rich** | Interface-based — states carry data, events carry payloads, computed targets |

## Dynamic Tier

### Build and run

```csharp
using FrenchExDev.Net.FiniteStateMachine.Dynamic;

var result = new DynamicStateMachineBuilder()
    .InitialState("Idle")
    .FinalState("Done")
    .When("Idle")
        .On("Start").TransitionTo("Running")
    .When("Running")
        .On("Complete").TransitionTo("Done")
    .Build();

var machine = result.Value!.CreateMachine();
await machine.FireAsync("Start");
// machine.CurrentState == "Running"
```

### Add guards

```csharp
.When("Idle")
    .On("Start")
        .Guard(() => hasPermission)
        .TransitionTo("Running")
```

### Serialize and deserialize

```csharp
using FrenchExDev.Net.FiniteStateMachine.Dynamic;

// Serialize
var json = DynamicStateMachineSerializer.ToJson(definition);

// Deserialize
var restored = DynamicStateMachineSerializer.FromJson(json);

// Note: guards and actions are code — re-attach after deserialization.
```

## Typed Tier

### Fluent builder

```csharp
using FrenchExDev.Net.FiniteStateMachine.Typed;

public enum TrafficLight { Red, Yellow, Green }
public enum TrafficEvent { Timer }

var definition = new TypedStateMachineBuilder<TrafficLight, TrafficEvent>()
    .InitialState(TrafficLight.Red)
    .When(TrafficLight.Red)
        .On(TrafficEvent.Timer).TransitionTo(TrafficLight.Green)
    .When(TrafficLight.Green)
        .On(TrafficEvent.Timer).TransitionTo(TrafficLight.Yellow)
    .When(TrafficLight.Yellow)
        .On(TrafficEvent.Timer).TransitionTo(TrafficLight.Red)
    .Build();

var machine = definition.Value!.CreateMachine();
```

### Source-generated state machine

```csharp
using FrenchExDev.Net.FiniteStateMachine.Attributes;

[StateMachine(typeof(TrafficLight), typeof(TrafficEvent), InitialState = nameof(TrafficLight.Red))]
public partial class TrafficLightMachine
{
    [Transition(TrafficLight.Red,    TrafficEvent.Timer, TrafficLight.Green)]
    [Transition(TrafficLight.Green,  TrafficEvent.Timer, TrafficLight.Yellow)]
    [Transition(TrafficLight.Yellow, TrafficEvent.Timer, TrafficLight.Red)]
    private static partial void DefineTransitions();
}

// Usage:
var machine = new TrafficLightMachine();
await machine.FireTimerAsync();
Assert.Equal(TrafficLight.Green, machine.CurrentState);
```

### Add a guard to a source-generated transition

```csharp
[StateMachine(typeof(DoorState), typeof(DoorEvent), InitialState = nameof(DoorState.Closed))]
public partial class DoorStateMachine
{
    [Transition(DoorState.Closed, DoorEvent.Lock, DoorState.Locked, Guard = nameof(IsKeyInserted))]
    [Transition(DoorState.Closed, DoorEvent.Open, DoorState.Open)]
    private static partial void DefineTransitions();

    private Task<bool> IsKeyInserted(DoorState from, DoorEvent evt, DoorState to, CancellationToken ct)
    {
        return Task.FromResult(_keyInserted);
    }
}
```

### Add entry/exit actions (fluent builder)

```csharp
var definition = new TypedStateMachineBuilder<State, Event>()
    .InitialState(State.Idle)
    .When(State.Running)
        .OnEntry(() => Console.WriteLine("Entered Running"))
        .OnExit(() => Console.WriteLine("Left Running"))
        .On(Event.Stop).TransitionTo(State.Idle)
    .Build();
```

### Add transition actions (fluent builder)

```csharp
.When(State.Idle)
    .On(Event.Start)
        .Then(() => logger.Log("Starting..."))
        .TransitionTo(State.Running)
```

## Rich Tier

### Define states and events

```csharp
using FrenchExDev.Net.FiniteStateMachine.Rich;

// State interface — "Find All Implementations" shows every state
public interface IDocState : IState { }
public interface IDocEvent : IEvent { }

// States carry data
public record DraftState(string Author) : IDocState
{
    public string Name => "Draft";
}

public record PublishedState(DateTime PublishedAt, string Url) : IDocState
{
    public string Name => "Published";
}

// Events carry payloads
public record PublishEvent(string Url) : IDocEvent
{
    public string Name => "Publish";
}
```

### Build with computed target states

The key Rich tier feature: the target state is computed from the event payload.

```csharp
var definition = new RichStateMachineBuilder<IDocState, IDocEvent>()
    .InitialState(new DraftState("Alice"))
    .FinalState<PublishedState>()
    .When<DraftState>()
        .On<PublishEvent>()
            .TransitionTo((evt, current) => new PublishedState(DateTime.UtcNow, evt.Url))
    .Build();

var machine = definition.Value!;
await machine.FireAsync(new PublishEvent("https://example.com/doc"));
// machine.CurrentState is PublishedState { Url = "https://example.com/doc" }
```

### Rich tier with source generation (visitor + Match)

```csharp
[RichStateMachine(InitialState = typeof(DraftState))]
public partial interface IDocState : IState { }

[RichStateMachineEvents(StateMachine = typeof(IDocState))]
public partial interface IDocEvent : IEvent { }

[State]
public partial record DraftState(string Author) : IDocState { public string Name => "Draft"; }

[State(Terminal = true)]
public partial record PublishedState(DateTime At) : IDocState { public string Name => "Published"; }

[Event]
[RichTransition(typeof(DraftState), typeof(PublishedState))]
public partial record PublishEvent(string Url) : IDocEvent { public string Name => "Publish"; }
```

Generated visitor usage:

```csharp
// Exhaustive match (compiler enforces all cases)
string label = state.Match(
    onDraft: d => $"Draft by {d.Author}",
    onPublished: p => $"Published at {p.At}"
);

// Visitor pattern
class DocStateVisitor : IDocStateVisitor<string>
{
    public string Visit(DraftState s) => "Draft";
    public string Visit(PublishedState s) => "Published";
}
```

## Guards

### Lambda guards (all tiers)

```csharp
// Typed tier — async guard
.When(State.Idle)
    .On(Event.Start)
        .Guard(async (from, evt, to, ct) => await CheckPermissionAsync(ct))
        .TransitionTo(State.Running)

// Typed tier — sync shorthand
.When(State.Idle)
    .On(Event.Start)
        .Guard(() => hasPermission)
        .TransitionTo(State.Running)

// Rich tier
.When<DraftState>()
    .On<PublishEvent>()
        .Guard((evt, current) => !string.IsNullOrEmpty(evt.Url))
        .TransitionTo((evt, _) => new PublishedState(DateTime.UtcNow, evt.Url))
```

### Multiple guards (AND logic)

All guards on a single transition must pass. If any guard rejects, the transition is denied.

```csharp
.When(State.Idle)
    .On(Event.Start)
        .Guard(() => hasPermission)
        .Guard(() => isReady)
        .TransitionTo(State.Running)
```

### Multiple transitions with guards (first-match)

When multiple transitions exist for the same `(state, event)`, each is tried in order. The first one whose guards all pass wins.

```csharp
.When(State.Review)
    .On(Event.Decide)
        .Guard(() => score >= 80)
        .TransitionTo(State.Approved)
    .On(Event.Decide)
        .Guard(() => score < 80)
        .TransitionTo(State.Rejected)
```

## Listeners

### Implement a listener

```csharp
// On net10.0 — override only the hooks you need (default interface methods)
class AuditListener : IStateMachineListener<State, Event>
{
    public Task OnTransitionedAsync(State from, Event evt, State to, CancellationToken ct)
    {
        Console.WriteLine($"{from} --{evt}--> {to}");
        return Task.CompletedTask;
    }
}

// On netstandard2.0 — use the base class
class AuditListener : StateMachineListenerBase<State, Event>
{
    public override Task OnTransitionedAsync(State from, Event evt, State to, CancellationToken ct)
    {
        Console.WriteLine($"{from} --{evt}--> {to}");
        return Task.CompletedTask;
    }
}
```

### Attach listeners

```csharp
var machine = definition.CreateMachine();
machine.AddListener(new AuditListener());
machine.RemoveListener(listener);
```

## Hierarchy

### Define parent-child states

```csharp
var hierarchy = new StateHierarchy<OrderState>();
hierarchy.AddChild(OrderState.Processing, OrderState.Validating, isInitial: true);
hierarchy.AddChild(OrderState.Processing, OrderState.Charging);
```

### Query hierarchy

```csharp
// Check if in a parent state (includes descendants)
bool inProcessing = hierarchy.IsInState(OrderState.Validating, OrderState.Processing); // true

// Get parent
if (hierarchy.TryGetParent(OrderState.Validating, out var parent))
    // parent == OrderState.Processing

// Get initial sub-state
if (hierarchy.TryGetInitialChild(OrderState.Processing, out var initial))
    // initial == OrderState.Validating

// Least Common Ancestor
if (hierarchy.TryGetLCA(stateA, stateB, out var lca))
    // lca is the shared ancestor
```

### Hierarchy in engine

When no transition is found at the current state, the engine automatically walks up the parent chain searching for a match. Entry/exit actions fire for all states traversed.

## Parallel Regions (Composite)

```csharp
var region1Machine = definition1.CreateMachine();
var region2Machine = definition2.CreateMachine();

var composite = new CompositeStateMachine<State, Event>(
    new[]
    {
        ("UI", region1Machine),
        ("Network", region2Machine)
    },
    isTerminal: s => s == State.Done
);

// Broadcast event to all regions
var results = await composite.FireAllAsync(Event.Start);

// Check if all regions are in terminal states
if (composite.AllRegionsTerminal) { /* all done */ }

// Access a specific region
var uiRegion = composite.GetRegion("UI");
var uiState = uiRegion!.Machine.CurrentState;
```

## Deferred Events

Queue events that can't be processed in the current state for later replay.

```csharp
var deferred = new DeferredEventQueue<State, Event>();
deferred.Defer(State.Waiting, Event.Process);  // Register pair

// In your event handler:
if (deferred.IsDeferred(machine.CurrentState, @event))
{
    deferred.Enqueue(@event);
    return;
}

// After a state change, replay queued events:
int replayed = await deferred.ReplayAsync(machine);
```

## Timer Transitions

Auto-fire an event after a timeout.

```csharp
var timer = new TimerTransition<State, Event>(
    machine,
    timeoutEvent: Event.Timeout,
    duration: TimeSpan.FromSeconds(30)
);

// Start on state entry
timer.Start();

// Cancel on state exit
timer.Cancel();
```

## Concurrency

```csharp
// Thread-safe machine (SemaphoreSlim-based)
var machine = definition.CreateMachine(ConcurrencyMode.Semaphore);

// Default — no synchronization (caller ensures single-threaded access)
var machine = definition.CreateMachine(ConcurrencyMode.None);
```

## Visualization

### Mermaid

```csharp
using FrenchExDev.Net.FiniteStateMachine.Graph;

var mermaid = MermaidExporter.Export(definition);
var mermaid = MermaidExporter.Export(definition, new MermaidOptions
{
    Direction = "TB",              // top-to-bottom (default: "LR")
    HighlightFinalStates = true    // default: true
});
```

### Graphviz DOT

```csharp
var dot = DotExporter.Export(definition);
```

### From StateGraph

```csharp
var graph = new StateGraph<State, Event>(definition);
var deadEnds = graph.DeadEndStates;     // states with no outgoing transitions
var mermaid = MermaidExporter.Export(graph);
```

## Testing

### Assert transitions

```csharp
using FrenchExDev.Net.FiniteStateMachine.Testing;

// Single transition
await StateMachineAssert.TransitionsToAsync(machine, Event.Start, State.Running);

// Full path reaches target
await StateMachineAssert.PathReachesAsync(machine,
    new[] { Event.Start, Event.Process, Event.Complete },
    State.Done);

// Event is denied
await StateMachineAssert.IsDeniedAsync(machine, Event.Start);
```

### Model-based testing (enumerate all paths)

```csharp
// Generate all paths from initial to terminal states
var paths = StateMachinePathGenerator.AllPaths(definition, maxDepth: 50);

// Use with xUnit [Theory] for exhaustive testing
public static IEnumerable<object[]> AllPaths =>
    StateMachinePathGenerator.AllPaths(definition)
        .Select(path => new object[] { path });

[Theory]
[MemberData(nameof(AllPaths))]
public async Task AllPathsReachTerminalState(StateMachinePath<State, Event> path)
{
    var machine = definition.CreateMachine();
    foreach (var step in path.Steps)
    {
        var result = await machine.FireAsync(step.Event);
        Assert.True(result.IsSuccess);
    }
}
```

## History Tracking

```csharp
var machine = definition.CreateMachine();
var history = new HistoryListener<State, Event>();
machine.AddListener(history);

await machine.FireAsync(Event.Start);
await machine.FireAsync(Event.Process);

// history records all transitions as TransitionRecord<TState, TEvent>
```

## Source Generator Diagnostics

The Typed SG emits compile-time diagnostics:

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
