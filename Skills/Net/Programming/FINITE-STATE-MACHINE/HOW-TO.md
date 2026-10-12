# FINITE-STATE-MACHINE — How To

Recipes for the common cases. Each one is paste-ready.

## Choosing a Tier

| Use case | Tier |
|---|---|
| The machine is loaded from JSON / database / user input | **Dynamic** |
| The machine is fixed at compile time and states have no payload | **Typed** |
| States and events carry data (timestamps, payloads, IDs) | **Rich** |

When in doubt, start Typed and migrate to Rich if states grow into structures.

## Dynamic — Build a Machine at Runtime

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
var result = await machine.FireAsync("Submit");
```

`def` is `Result<StateMachineDefinition>`. Failure means the builder rejected the model (e.g. duplicate transitions, unreachable states). Always check.

## Typed — Hand-Written

```csharp
public enum DoorState { Closed, Open, Locked }
public enum DoorEvent { Open, Close, Lock, Unlock }

var def = new TypedStateMachineBuilder<DoorState, DoorEvent>()
    .InitialState(DoorState.Closed)
    .FinalState(DoorState.Locked)
    .When(DoorState.Closed)
        .On(DoorEvent.Open).TransitionTo(DoorState.Open)
        .On(DoorEvent.Lock).TransitionTo(DoorState.Locked)
    .When(DoorState.Open)
        .On(DoorEvent.Close).TransitionTo(DoorState.Closed)
    .When(DoorState.Locked)
        .On(DoorEvent.Unlock).TransitionTo(DoorState.Closed)
    .Build();

var machine = def.Value!.CreateMachine();
await machine.FireAsync(DoorEvent.Open);
```

## Typed — Source-Generated

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

var door = new DoorStateMachine();
await door.FireOpenAsync();
await door.FireLockAsync();
var permitted = await door.PermittedEventsAsync();
```

The generator emits one `FireXxxAsync()` per enum member of `TEvent`, plus `PermittedEventsAsync()` returning the set the current state accepts.

## Rich — States as Records

```csharp
public interface IOrderState : IState { }
public interface IOrderEvent : IEvent { }

public record CreatedState() : IOrderState { public string Name => "Created"; }
public record SubmittedState(DateTime At) : IOrderState { public string Name => "Submitted"; }
public record DeliveredState(DateTime At) : IOrderState { public string Name => "Delivered"; }

public record SubmitEvent(List<string> Items) : IOrderEvent { public string Name => "Submit"; }

var def = new RichStateMachineBuilder<IOrderState, IOrderEvent>()
    .InitialState(new CreatedState())
    .FinalState<DeliveredState>()
    .When<CreatedState>()
        .On<SubmitEvent>()
            .TransitionTo((evt, _) => new SubmittedState(DateTime.UtcNow))
    .Build();

var machine = def.Value!;
await machine.FireAsync(new SubmitEvent(new() { "item1" }));
```

The transition lambda receives the event and the current state and returns the next state — a pure function from `(event, state) → state`.

## Adding a Guard

```csharp
.When(DoorState.Closed)
    .On(DoorEvent.Open)
        .Guard(async (state, evt, ctx, ct) => await IsKeyPresentAsync())
        .TransitionTo(DoorState.Open)
```

Guards are `async`. Multiple guards on the same transition are AND-combined. A failing guard denies the transition; the engine returns `Result.Failure` and fires `OnDenied`.

## Adding Entry/Exit/Transition Actions

```csharp
.When(DoorState.Open)
    .OnEntry(async (state, ctx, ct) => await NotifyOpenedAsync())
    .OnExit(async (state, ctx, ct) => await LogClosingAsync())
    .On(DoorEvent.Close)
        .Action(async (from, to, evt, ctx, ct) => await AuditTransitionAsync(from, to))
        .TransitionTo(DoorState.Closed)
```

All actions are async. Action exceptions become `Result.Failure(ex)` — they do not propagate.

## Listeners

```csharp
public sealed class LoggingListener : IStateMachineListener
{
    public Task OnTransition(StateMachineContext ctx, Transition t)
    {
        _log.LogInformation("{From} -> {To} via {Event}", t.From, t.To, t.Event);
        return Task.CompletedTask;
    }
    // other hooks default to Task.CompletedTask
}

var machine = def.Value!.CreateMachine();
machine.AddListener(new LoggingListener());
machine.AddListener(new HistoryListener(capacity: 100));   // built-in
```

## Hierarchy

```csharp
.When(DoorState.Locked).IsChildOf(DoorState.Closed)
```

`Locked` inherits transitions from `Closed`. `OnExit(Closed)` fires when leaving any descendant of `Closed`. The LCA between source and target determines exactly which exits and entries fire.

## Parallel Regions

```csharp
var composite = new CompositeStateMachineBuilder()
    .AddRegion("payment", paymentDef)
    .AddRegion("fulfillment", fulfillmentDef)
    .Build();

await composite.FireAsync("PaymentReceived");   // dispatched to all regions
```

Each region tracks its own current state. The composite reports success if at least one region accepted the event.

## Deferred Events

```csharp
.When(DoorState.Locked)
    .Defer(DoorEvent.Open)         // queue Open until we leave Locked
```

When the machine leaves `Locked`, every queued `Open` event replays in order.

## Timer Transitions

```csharp
.When(DoorState.Open)
    .OnTimeout(TimeSpan.FromMinutes(5)).TransitionTo(DoorState.Closed)
```

The engine schedules an internal timer event. On timeout, the transition fires through the same lifecycle as a normal event.

## Visualization

```csharp
var mermaid = MermaidExporter.Export(definition);
File.WriteAllText("docs/door.mmd", mermaid);

var dot = DotExporter.Export(definition);
File.WriteAllText("docs/door.dot", dot);
```

Wire the exporter into your build to keep diagrams in sync with the model.

## Testing

```csharp
// Single transition
await StateMachineAssert.TransitionsToAsync(machine, DoorEvent.Open, DoorState.Open);

// Multi-step path
await StateMachineAssert.PathReachesAsync(machine,
    new[] { DoorEvent.Open, DoorEvent.Close, DoorEvent.Lock },
    DoorState.Locked);

// Denial
await StateMachineAssert.IsDeniedAsync(machine, DoorEvent.Lock);

// Model-based: enumerate every reachable path
var paths = StateMachinePathGenerator.AllPaths(def, maxDepth: 50);
foreach (var p in paths)
{
    var m = def.Value!.CreateMachine();
    foreach (var e in p.Events) await m.FireAsync(e);
    Assert.Equal(p.ExpectedEnd, m.CurrentState);
}
```

## Concurrency

Default is `ConcurrencyMode.Semaphore`. Switch only if profiling shows the lock is hot **and** callers serialize externally:

```csharp
var machine = def.Value!.CreateMachine(new StateMachineOptions
{
    ConcurrencyMode = ConcurrencyMode.None
});
```

## Anti-Patterns

| Don't | Why |
|---|---|
| Throw on a denied event | The engine returns `Result.Failure`. Use `Match` or `IsDenied` to handle it. |
| Block on `FireAsync().Result` | The engine is async-first; blocking causes deadlocks under sync contexts. |
| Hand-code the cross-product of two regions as flat states | Use a composite machine. The flat encoding explodes combinatorially. |
| Encode shared transitions on every leaf | Use hierarchy. Declare on a parent state, refine on leaves. |
| Read `CurrentState` from a listener and call back into `FireAsync` | Listeners run inside the lifecycle; recursive fires deadlock or skip the queue. Use deferred events. |
| Use Dynamic when Typed would do | Typed catches typos at compile time; Dynamic discovers them at runtime. |
| Use Rich when records carry no data | Rich is for stateful payloads. Typed is simpler for plain enum states. |
