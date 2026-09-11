# IEC-61499 — Philosophy

The IEC 61499 standard defines a function-block model for distributed industrial control systems. This skill captures the patterns for expressing function blocks as decorated C# classes that compile down to event-driven, deterministic, AOT-friendly runtimes.

## Function Blocks Are the Unit of Composition

A function block is a black box with:

- **Event inputs** — discrete signals that trigger execution
- **Event outputs** — discrete signals that announce completion or state change
- **Data inputs** — typed values associated with events ("with" semantics)
- **Data outputs** — typed values produced by execution
- **Internal state** — captured by an Execution Control Chart (ECC) — a state machine
- **Algorithms** — the actual computation, bound to ECC transitions

The function block is the **only** unit of behavior in the system. Networks of function blocks are wired event-to-event and data-to-data. The runtime schedules them; the developer never writes a control loop, never spawns a thread, never polls. The model is fully event-driven.

This is the right granularity for industrial control: a function block is small enough to reason about, large enough to encapsulate a meaningful behavior, and composable enough to wire into a system without integration code.

## Why Express It in C#

The historical IEC 61499 tooling produces XML files (the standard's "Block Type Library Format") and ships with proprietary IDEs. This is the same trap as Ecore vs. C# for metamodels: a separate modeling environment, a separate file format, a separate tool chain.

The pattern this skill describes is to express function blocks as **decorated C# classes** and let a source generator emit the runtime artefacts:

```csharp
[FunctionBlock("BinaryOperator")]
public partial class BinaryOperatorBlock<T> where T : struct, INumber<T>
{
    [EventInput(With = [nameof(Left), nameof(Right)])]
    public static readonly EventPort Add, Subtract, Multiply, Divide;

    [EventOutput(With = [nameof(Result)])]
    public static readonly EventPort Computed;

    [DataInput] public static readonly DataPort<T> Left;
    [DataInput] public static readonly DataPort<T> Right;
    [DataOutput] public static readonly DataPort<T> Result;

    [EccState(Initial = true)] private static partial void Idle();
    [EccState] private static partial void Computing();

    [Algorithm(On = nameof(Add), Emits = nameof(Computed))]
    private void AddAlg() => Result.Set(Left.Get() + Right.Get());
}
```

The compiler is the modeling tool. The IDE is the navigation tool. The type system catches typos. There are no XML files.

## Five Sets of Attributes

The vocabulary splits into five distinct concerns:

| Attribute set | Purpose |
|---|---|
| `[FunctionBlock]` | Marks a class as a function block type |
| `[EventInput]`, `[EventOutput]` | Declare the event interface (the "what triggers" and "what's produced") |
| `[DataInput]`, `[DataOutput]` | Declare the data interface (typed values associated with events via `With`) |
| `[EccState]`, `[EccTransition]`, `[Algorithm]` | Declare the internal state machine and the code attached to transitions |
| `[Application]`, `[Device]`, `[Resource]`, `[Map]` | Infrastructure attributes — declare deployment topology |

The first four describe **what a function block is**. The fifth describes **where it runs**. Keeping them separate means the function block library is portable across deployment topologies; the same `BinaryOperatorBlock<int>` runs in a simulator, on a PLC, on a Linux device, or in a Kubernetes pod, depending on which `[Map]` you choose.

## ECC = State Machine for the Block's Internals

Every function block has an Execution Control Chart — a state machine that determines which algorithm fires in response to which event. This is exactly the FSM pattern; the IEC 61499 layer **builds on** the FiniteStateMachine library rather than reinventing one.

```csharp
[EccState(Initial = true)] private static partial void Idle();
[EccState] private static partial void Computing();

[Algorithm(On = nameof(Add), Emits = nameof(Computed))]
private void AddAlg() => Result.Set(Left.Get() + Right.Get());
```

The `[EccState]` attributes declare the states. The `[Algorithm]` attribute binds an event to a method, declares which output event fires after the method returns, and (implicitly) which transition is taken in the ECC. The source generator builds the underlying typed FSM and wires the algorithm methods as transition actions.

The developer never writes the FSM by hand. They write attributes; the generator turns them into a state machine.

## Composite Function Blocks

A function block can be **basic** (its body is an ECC + algorithms) or **composite** (its body is a network of other function blocks, wired event-to-event and data-to-data). Composite blocks are how you build hierarchies: small primitive blocks combine into mid-level blocks, which combine into application-level blocks.

The same attribute vocabulary covers both. A composite block uses connection attributes to declare which output of which child connects to which input of which other child. The source generator emits a **composite runtime** that schedules the children and forwards events.

Composition is the only way to scale industrial control models above a few dozen blocks. Without it, you have a flat soup of leaf functions and no abstraction.

## Infrastructure Is Separate from Behavior

`[Application]`, `[Device]`, `[Resource]`, `[Map]` describe deployment, not behavior:

- **Application** — a logical grouping of function block instances
- **Device** — a physical (or virtual) host that runs resources
- **Resource** — an execution context inside a device (think OS process, container, scheduler slot)
- **Map** — assigns function block instances to resources

The same application can be mapped onto a single laptop for simulation, onto a network of edge devices for deployment, or onto a Kubernetes cluster for testing — by changing only the `[Map]` declarations. The function block code does not change. This is the original IEC 61499 promise: portable function blocks, reconfigurable topology.

## Source Generation, Not Interpretation

Hand-written runtimes for function-block models are common. They are also slow, fragile, and untestable. Source generation produces:

- A strongly-typed runtime per function block (no reflection, no boxing)
- A typed FSM for each ECC (using the FiniteStateMachine library)
- Factory methods that instantiate blocks and wire ports
- Mermaid / DOT diagrams for visualization

The output is normal C# the AOT compiler can optimise. There is no interpreter loop. There is no runtime model walker. The model **is** the code.

## Always-Running Agent

The execution model assumes there is always an agent process on each device that:

- Loads function block assemblies
- Listens for new application deployments
- Schedules events into the runtime
- Exposes monitoring (metrics, logs, current state) over gRPC or REST
- Provides hot-reload when a new version of a function block ships

This is a deliberate departure from the "build, deploy, restart" cycle of typical .NET applications. Industrial control systems need long-running processes with hot-update; the agent is how you get there.

## Trade-offs Accepted

| Trade-off | Decision |
|---|---|
| C# instead of XML | Loses interop with legacy IEC 61499 tools, gains IDE / compiler / ecosystem |
| Source generation instead of interpretation | Slower compile, faster runtime, fully type-safe |
| Built on FiniteStateMachine instead of a custom FSM | Reuses tested infrastructure; ECCs are just typed FSMs |
| Always-running agent instead of restart-on-deploy | Required for industrial control; adds operational complexity |
| Infrastructure attributes co-located with code | Keeps the deployment model in source control; harder for non-developers to edit |
| `static readonly` ports instead of instance fields | Required for the SG to discover them at compile time without instantiating |

## What This Pattern Is Not

- **Not a PLC compiler.** It produces .NET runtime code, not PLC bytecode. Targeting PLC hardware requires a separate AOT pipeline.
- **Not a SCADA system.** It runs the control logic; visualization and operator interfaces are separate concerns.
- **Not real-time-deterministic on stock .NET.** Hard real-time needs a real-time OS and an AOT runtime tuned for low jitter.
- **Not a workflow engine.** Function blocks are reactive event handlers, not long-running business processes.

Use this pattern when you have a domain that fits the function-block paradigm — sensors and actuators wired into deterministic event-driven graphs. Use a different pattern for everything else.
