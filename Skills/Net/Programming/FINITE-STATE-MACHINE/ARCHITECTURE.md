# FINITE-STATE-MACHINE — Architecture

The library is one engine with three façades and two source generators. This file describes the moving parts.

## Package Layout

```
FiniteStateMachine/
├── src/
│   ├── FrenchExDev.Net.FiniteStateMachine                    Core engine + 3 façades (netstandard2.0;net10.0)
│   ├── FrenchExDev.Net.FiniteStateMachine.Attributes         Marker attributes (netstandard2.0;net10.0)
│   ├── FrenchExDev.Net.FiniteStateMachine.SourceGenerator    Typed-tier IIncrementalGenerator (netstandard2.0)
│   ├── FrenchExDev.Net.FiniteStateMachine.SourceGenerator.Lib  Shared emitters, no Roslyn (netstandard2.0)
│   ├── FrenchExDev.Net.FiniteStateMachine.Design             Rich-tier IIncrementalGenerator (netstandard2.0)
│   └── FrenchExDev.Net.FiniteStateMachine.Testing            Test utilities (StateMachineAssert, PathGenerator)
└── test/
    └── FrenchExDev.Net.FiniteStateMachine.Tests              ~92 xUnit tests
```

The split between `SourceGenerator` and `Design` is intentional: the Typed and Rich tiers have different code shapes, and merging them into one generator would couple two unrelated emitters.

## Three Tiers

| Tier | Builder | Identifier shape | Source generator |
|---|---|---|---|
| Dynamic | `DynamicStateMachineBuilder` | `string` | none |
| Typed | `TypedStateMachineBuilder<TState, TEvent>` | `enum` | `[StateMachine]` + `[Transition]` |
| Rich | `RichStateMachineBuilder<TState, TEvent>` | `IState` / `IEvent` types | `[RichStateMachine]` |

All three produce a state machine that implements the same engine interface. Lifecycle, listeners, hierarchy, regions, and visualisation work identically.

### Dynamic

```csharp
var def = new DynamicStateMachineBuilder()
    .InitialState("Created")
    .FinalState("Delivered")
    .When("Created")  .On("Submit").TransitionTo("Submitted")
    .When("Submitted").On("Approve").TransitionTo("Approved")
    .When("Approved") .On("Ship").TransitionTo("Shipped")
    .When("Shipped")  .On("Deliver").TransitionTo("Delivered")
    .Build();

var machine = def.Value!.CreateMachine();
await machine.FireAsync("Submit");
```

States and events are strings. Built at runtime — for JSON-loaded machines, end-user editors, rules engines.

### Typed

```csharp
public enum DoorState { Closed, Open, Locked }
public enum DoorEvent { Open, Close, Lock, Unlock }

var def = new TypedStateMachineBuilder<DoorState, DoorEvent>()
    .InitialState(DoorState.Closed)
    .FinalState(DoorState.Locked)
    .When(DoorState.Closed)
        .On(DoorEvent.Open).TransitionTo(DoorState.Open)
        .On(DoorEvent.Lock).TransitionTo(DoorState.Locked)
    .Build();
```

Compile-time-checked. The 80% case.

### Rich

```csharp
public interface IOrderState : IState { }
public interface IOrderEvent : IEvent { }
public record CreatedState() : IOrderState { public string Name => "Created"; }
public record SubmittedState(DateTime At) : IOrderState { public string Name => "Submitted"; }
public record SubmitEvent(List<string> Items) : IOrderEvent { public string Name => "Submit"; }

var def = new RichStateMachineBuilder<IOrderState, IOrderEvent>()
    .InitialState(new CreatedState())
    .When<CreatedState>()
        .On<SubmitEvent>()
            .TransitionTo((evt, _) => new SubmittedState(DateTime.UtcNow))
    .Build();
```

States and events carry data. Transitions can synthesise the next state from the event payload.

## Engine Lifecycle

For every `FireAsync(event)` call, the engine runs:

```
1. Acquire concurrency gate (None → no-op; Semaphore → SemaphoreSlim.WaitAsync)
2. listener.OnBeforeFire(event)
3. Resolve current state's effective transitions (walk hierarchy if needed)
4. Find a transition matching the event
5. If no transition → listener.OnDenied → release gate → return Result.Failure
6. listener.OnBeforeGuard(event, transition)
7. Evaluate guards (async, AND-combined)
8. listener.OnAfterGuard(event, transition, result)
9. If guard failed → listener.OnDenied → release gate → return Result.Failure
10. Compute LCA between source and target states
11. Walk from source up to LCA → fire OnExit on each
12. Fire transition action (if any)
13. Walk from LCA down to target → fire OnEntry on each
14. Update current state
15. listener.OnTransition(transition)
16. listener.OnAfterFire(transition)
17. Process deferred events queue
18. Release concurrency gate
19. Return Result.Success(transition)
```

The same lifecycle runs for all three tiers. The difference between tiers is only how transitions are described to the builder.

## Result-Returning Fire

Every `FireAsync` returns `Result<Transition<TState>>`:

```csharp
var result = await machine.FireAsync(DoorEvent.Open);
return result.Match(
    onSuccess: t => $"Moved {t.From} → {t.To} on {t.Event}",
    onFailure: err => $"Refused: {err.Message}");
```

The engine **never throws on a denied event**. Throwing on a known business outcome is using exceptions as control flow. Listener exceptions and action exceptions are wrapped in `Result.Failure(ex)`, not propagated.

## Hierarchy

Parent-child relationships are declared in the builder:

```csharp
.When(DoorState.Locked).IsChildOf(DoorState.Closed)
```

Resolution rules:

- A child inherits its parent's transitions unless it overrides them.
- Firing an event walks from the leaf state up the parent chain looking for a matching transition.
- The LCA between source and target determines which states fire `OnExit` and `OnEntry`.
- Entering a parent for the first time fires its `OnEntry` once.

LCA resolution is implemented as a path-walk: build the chain from source to root, build the chain from target to root, find the deepest shared ancestor.

## Composite Machines and Regions

`ICompositeStateMachine` runs multiple regions concurrently:

```csharp
var composite = new CompositeStateMachineBuilder()
    .AddRegion("payment", paymentDef)
    .AddRegion("fulfillment", fulfillmentDef)
    .Build();

await composite.FireAsync("PaymentReceived");   // dispatched to all regions
```

Each region has its own current state, listeners, deferred queue. Events fire into all regions; each decides whether to react. The composite returns success if **at least one** region accepts.

## Listeners

`IStateMachineListener` exposes 8 hooks:

```csharp
public interface IStateMachineListener
{
    Task OnBeforeFire(StateMachineContext ctx, object @event);
    Task OnAfterFire(StateMachineContext ctx, object @event, Result<Transition> result);
    Task OnBeforeGuard(StateMachineContext ctx, object @event);
    Task OnAfterGuard(StateMachineContext ctx, object @event, bool passed);
    Task OnEntry(StateMachineContext ctx, object state);
    Task OnExit(StateMachineContext ctx, object state);
    Task OnTransition(StateMachineContext ctx, Transition transition);
    Task OnDenied(StateMachineContext ctx, object @event, string reason);
}
```

Listeners register at machine construction. Multiple listeners are invoked in registration order. Failures in listeners are caught and recorded; they do not abort the transition.

`HistoryListener` ships in the core: a ring-buffer transition recorder. Replace it or add others.

## Source Generators

### Typed-tier SG (`SourceGenerator`)

Pipeline:

1. Find classes with `[StateMachine(typeof(TState), typeof(TEvent), InitialState = ...)]`.
2. Read `[Transition(from, evt, to)]` attributes on the class.
3. Build a `TypedStateMachineModel`.
4. Call `TypedStateMachineEmitter.Emit(model)` from the `.Lib`.
5. Emit a partial that:
   - Instantiates a `TypedStateMachineBuilder<TState, TEvent>` in `DefineTransitions()`
   - Exposes `FireXxxAsync()` methods for every event in `TEvent`
   - Exposes `PermittedEventsAsync()` returning the set of events the current state accepts

### Rich-tier SG (`Design`)

Pipeline:

1. Find classes with `[RichStateMachine]`.
2. Walk transitions declared via attributes on the class.
3. Emit a partial that builds a `RichStateMachineBuilder<...>` and exposes typed fire methods.

Both generators use the shared `SourceGenerator.Lib` for string-based emission. The `.Lib` has no Roslyn dependency and is unit-testable.

## Path Generation and Testing

`StateMachinePathGenerator.AllPaths(definition, maxDepth)` performs DFS over the transition graph and returns every reachable event sequence. Used for model-based tests:

```csharp
var paths = StateMachinePathGenerator.AllPaths(def, maxDepth: 50);
foreach (var path in paths)
{
    var machine = def.Value!.CreateMachine();
    foreach (var evt in path.Events)
        await machine.FireAsync(evt);
    Assert.Equal(path.ExpectedEnd, machine.CurrentState);
}
```

`StateMachineAssert` provides:

- `TransitionsToAsync(machine, event, expectedState)`
- `PathReachesAsync(machine, events, expectedFinal)`
- `IsDeniedAsync(machine, event)`

## Visualization

```csharp
var mermaid = MermaidExporter.Export(definition);
var dot = DotExporter.Export(definition);
```

Both exporters walk the same `StateMachineDefinition` and produce a renderable diagram. The output is the documentation; regenerating it from the model means it never lies.

## Concurrency Modes

| Mode | Mechanism | Use |
|---|---|---|
| `ConcurrencyMode.None` | No locking | External serialization, hot path |
| `ConcurrencyMode.Semaphore` (default) | `SemaphoreSlim(1, 1)` | General-purpose, safe |

Set at machine construction. Default is `Semaphore`.

## Result Integration

The library depends on `FrenchExDev.Net.Result`. Every fire returns `Result<Transition<TState>>`. Errors flow as values, never as exceptions, integrating with the wider Result-pattern conventions used in the codebase.
