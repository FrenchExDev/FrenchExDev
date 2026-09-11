# IEC-61499 — Architecture

The function-block platform splits along three axes: language (the function block model), runtime (the always-on engine), and infrastructure (deployment topology). Each axis has its own packages, and each package follows the same Roslyn-friendly four-project shape.

## Package Layout

```
IEC61499.2/
├── src/
│   ├── IEC61499.2                            Core domain models (net10.0)
│   ├── IEC61499.2.Attributes                 Function-block attributes (netstandard2.0;net10.0)
│   ├── IEC61499.2.SourceGenerator            Roslyn IIncrementalGenerator (netstandard2.0)
│   ├── IEC61499.2.SourceGenerator.Lib        Emitters, no Roslyn dependency (netstandard2.0)
│   ├── IEC61499.2.Infra.Attributes           [Application], [Device], [Resource], [Map] (netstandard2.0;net10.0)
│   ├── IEC61499.2.Infra.SourceGenerator      Deployment-topology SG (netstandard2.0)
│   ├── IEC61499.2.Runtime                    Event-driven execution engine (net10.0)
│   ├── IEC61499.2.Agent                      Always-running orchestrator: gRPC + REST (net10.0)
│   ├── IEC61499.2.OpcUa                      OPC-UA communication adapter (net10.0)
│   └── IEC61499.2.Testing                    Test helpers for FB validation (net10.0)
└── test/
    └── IEC61499.2.Tests                      xUnit tests
```

The same project shape repeats for the function-block axis and the infrastructure axis: attributes, generator, generator-lib. The runtime, agent, and OPC-UA adapter are runtime concerns and don't have generators.

## Function Block Vocabulary

### Class-level

| Attribute | Purpose |
|---|---|
| `[FunctionBlock("Name")]` | Marks a class as a function block type |

The class is `partial`. The generator emits the runtime, factory, and Mermaid diagram in another partial.

### Event interface

| Attribute | Purpose |
|---|---|
| `[EventInput(With = [...])]` | Declares an input event and the data inputs that travel with it |
| `[EventOutput(With = [...])]` | Declares an output event and the data outputs it produces |

Both decorate `static readonly EventPort` fields. The `With` array names the data ports that are sampled (input) or driven (output) when the event fires.

### Data interface

| Attribute | Purpose |
|---|---|
| `[DataInput]` | Declares a typed input slot |
| `[DataOutput]` | Declares a typed output slot |

Both decorate `static readonly DataPort<T>` fields. The type parameter is the runtime type of the data.

### Internal state machine (ECC)

| Attribute | Purpose |
|---|---|
| `[EccState(Initial = bool)]` | Declares a state in the Execution Control Chart |
| `[EccTransition(From, On, To, Guard?)]` | Declares a transition in the ECC (often inferred from `[Algorithm]`) |
| `[Algorithm(On, Emits, From?, To?)]` | Binds a method to an event, declaring which output event fires after |

`[EccState]` decorates a `static partial void StateName()` method. The body is empty — the method's only role is to give the state a typed name.

`[Algorithm]` is the workhorse. The `On` parameter names the input event that triggers the method, `Emits` names the output event that fires when the method returns. The generator infers a transition `(currentState, inputEvent) → nextState` and uses the algorithm method as the transition action.

## Generated Output (Per Function Block)

For one decorated function block, the generator emits:

1. **A typed FSM** built on `FrenchExDev.Net.FiniteStateMachine`. States are the `[EccState]` methods, transitions come from `[Algorithm]`/`[EccTransition]`, and algorithm methods are wired as transition actions.
2. **A factory method** `Create()` that instantiates the block and wires every port.
3. **Strongly-typed event enums** generated from the `[EventInput]`/`[EventOutput]` declarations.
4. **A Mermaid diagram** of the ECC, written next to the source.
5. **A runtime metadata block** that the agent reads to enumerate the type's interface.

## Composite Function Blocks

A composite block is declared with the same `[FunctionBlock]` attribute but contains **child block instances** and **connection attributes** instead of `[Algorithm]` methods:

```csharp
[FunctionBlock("Pipeline")]
public partial class PipelineBlock
{
    [EventInput(With = [nameof(In)])]    public static readonly EventPort Start;
    [EventOutput(With = [nameof(Out)])]  public static readonly EventPort Done;
    [DataInput]  public static readonly DataPort<int> In;
    [DataOutput] public static readonly DataPort<int> Out;

    [Child("Step1")] public static readonly Step1Block Step1;
    [Child("Step2")] public static readonly Step2Block Step2;

    [EventConnection(From = nameof(Start), To = "Step1.Trigger")]
    [EventConnection(From = "Step1.Done", To = "Step2.Trigger")]
    [EventConnection(From = "Step2.Done", To = nameof(Done))]
    [DataConnection (From = nameof(In),  To = "Step1.Input")]
    [DataConnection (From = "Step1.Result", To = "Step2.Input")]
    [DataConnection (From = "Step2.Result", To = nameof(Out))]
    private static partial void Wire();
}
```

The generator emits a composite runtime that creates the children, wires their ports, and forwards events.

## Infrastructure Vocabulary

| Attribute | Purpose |
|---|---|
| `[Application("Name")]` | Logical grouping of function block instances |
| `[Device("Name", Address = "...")]` | Physical (or virtual) host |
| `[Resource("Name")]` | Execution context inside a device (process, container, scheduler slot) |
| `[Map(InstanceName, ResourceName)]` | Assigns a function block instance to a resource |

The infrastructure SG reads these and emits:

- A deployment plan (which instance runs where)
- Agent registration code for each resource
- gRPC stubs for cross-device event routing
- A Mermaid topology diagram

## Runtime Engine

The runtime is the in-process scheduler that owns all function block instances on a single resource. Its responsibilities:

- Maintain an event queue
- Dispatch events to the right block's ECC
- Sample data inputs at event time, drive data outputs at event emission
- Forward cross-device events through the OPC-UA adapter
- Expose monitoring data to the agent

The engine is single-threaded per resource by default — events are processed FIFO. This is the simplest model that gives deterministic behavior. Multi-threaded execution is opt-in and requires explicit synchronization on shared blocks.

## Always-Running Agent

The agent is a long-running process. Responsibilities:

- Load function block assemblies and start the runtime
- Expose gRPC + REST endpoints for management
- Accept new deployments and reconfigure live
- Hot-reload updated function block versions
- Stream metrics, logs, and current ECC state to monitoring tools

The agent never restarts the process to deploy new code. It reloads assemblies into the same runtime. This requires care with assembly load contexts, but it is the only way to maintain industrial uptime SLAs.

## OPC-UA Adapter

OPC-UA is the dominant communication standard in industrial automation. The adapter exposes function block ports as OPC-UA variables and methods, and consumes events from external OPC-UA servers as input events to local function blocks.

The adapter is **optional**. A pure-.NET deployment doesn't need it. A deployment that has to talk to existing PLCs or SCADA systems does.

## Testing Helpers

The `Testing` package provides:

- A test harness that creates function block instances in isolation
- Helpers to fire events and assert output ports
- Recording listeners that capture every state transition for assertion
- Path generation utilities (delegated to FSM library) for ECC coverage

```csharp
[Fact]
public async Task BinaryOperator_adds_correctly()
{
    var fb = BinaryOperatorBlock<int>.CreateForTest();
    fb.Left.Set(2);
    fb.Right.Set(3);
    await fb.FireAsync(BinaryOperatorBlock<int>.Add);
    Assert.Equal(5, fb.Result.Get());
}
```

## Reuse of FiniteStateMachine

Every ECC is a typed FSM under the covers. The IEC 61499 SG calls into the FSM library's emitter to produce the underlying state machine, then layers the function-block surface on top. There is **no separate FSM implementation**. Reuse is mandatory — duplicating the FSM would mean two implementations of the same primitive.

This means:

- Every ECC inherits the FSM library's listener model, history, denial semantics
- Every ECC can be exported to Mermaid using the FSM exporter
- Every ECC is testable with `StateMachineAssert`
- Every ECC supports hierarchy and parallel regions if the function block uses them

## Target Frameworks

| Project | TFM | Why |
|---|---|---|
| `Attributes`, `Infra.Attributes` | `netstandard2.0;net10.0` | Used by both runtime and SG inputs |
| `SourceGenerator`, `Infra.SourceGenerator` | `netstandard2.0` | Roslyn requirement |
| `SourceGenerator.Lib` | `netstandard2.0` | No Roslyn dep, unit-testable |
| `Runtime`, `Agent`, `OpcUa` | `net10.0` | Modern runtime features required |
| `Testing` | `net10.0` | Modern test APIs |
