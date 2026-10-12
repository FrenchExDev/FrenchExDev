# IEC-61499 — How To

Recipes for building function blocks, composites, and deployment topologies. Each one is paste-ready.

## Declaring a Basic Function Block

```csharp
using FrenchExDev.Net.IEC61499;

[FunctionBlock("BinaryOperator")]
public partial class BinaryOperatorBlock<T> where T : struct, INumber<T>
{
    // ── Event interface ────────────────────────────────────────────
    [EventInput(With = [nameof(Left), nameof(Right)])]
    public static readonly EventPort Add, Subtract, Multiply, Divide;

    [EventOutput(With = [nameof(Result)])]
    public static readonly EventPort Computed;

    // ── Data interface ─────────────────────────────────────────────
    [DataInput]  public static readonly DataPort<T> Left;
    [DataInput]  public static readonly DataPort<T> Right;
    [DataOutput] public static readonly DataPort<T> Result;

    // ── ECC ────────────────────────────────────────────────────────
    [EccState(Initial = true)] private static partial void Idle();
    [EccState]                 private static partial void Computing();

    // ── Algorithms ─────────────────────────────────────────────────
    [Algorithm(On = nameof(Add),      Emits = nameof(Computed))]
    private void AddAlg() => Result.Set(Left.Get() + Right.Get());

    [Algorithm(On = nameof(Subtract), Emits = nameof(Computed))]
    private void SubAlg() => Result.Set(Left.Get() - Right.Get());

    [Algorithm(On = nameof(Multiply), Emits = nameof(Computed))]
    private void MulAlg() => Result.Set(Left.Get() * Right.Get());

    [Algorithm(On = nameof(Divide),   Emits = nameof(Computed))]
    private void DivAlg() => Result.Set(Left.Get() / Right.Get());
}
```

The class **must** be `partial`. Ports are declared as `static readonly` so the source generator can discover them at compile time without instantiating the class.

## Reading and Writing Ports Inside an Algorithm

```csharp
[Algorithm(On = nameof(Add), Emits = nameof(Computed))]
private void AddAlg()
{
    var a = Left.Get();              // sample input
    var b = Right.Get();             // sample input
    var c = a + b;
    Result.Set(c);                   // drive output
}
```

`Get()` is the only way to read a data input. `Set(value)` is the only way to drive a data output. The runtime captures the inputs at event time and propagates the outputs when the event emits.

## Using Multiple ECC States

```csharp
[FunctionBlock("Heater")]
public partial class HeaterBlock
{
    [EventInput(With = [nameof(SetPoint)])]  public static readonly EventPort Start;
    [EventInput]                             public static readonly EventPort Stop;
    [EventOutput(With = [nameof(Temp)])]     public static readonly EventPort Heating, Idle;

    [DataInput]  public static readonly DataPort<double> SetPoint;
    [DataOutput] public static readonly DataPort<double> Temp;

    [EccState(Initial = true)] private static partial void Stopped();
    [EccState]                 private static partial void Running();

    [Algorithm(On = nameof(Start), From = nameof(Stopped), To = nameof(Running), Emits = nameof(Heating))]
    private void StartAlg() => Temp.Set(SetPoint.Get());

    [Algorithm(On = nameof(Stop), From = nameof(Running), To = nameof(Stopped), Emits = nameof(Idle))]
    private void StopAlg() => Temp.Set(0);
}
```

Use `From` and `To` on the algorithm when the inferred transition isn't enough — for example, when the same input event has different effects depending on the current state.

## Composite Function Block

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

    [EventConnection(From = nameof(Start),    To = "Step1.Trigger")]
    [EventConnection(From = "Step1.Done",     To = "Step2.Trigger")]
    [EventConnection(From = "Step2.Done",     To = nameof(Done))]
    [DataConnection (From = nameof(In),       To = "Step1.Input")]
    [DataConnection (From = "Step1.Result",   To = "Step2.Input")]
    [DataConnection (From = "Step2.Result",   To = nameof(Out))]
    private static partial void Wire();
}
```

Composites have no `[Algorithm]` methods — their behavior is the wiring. The generator emits the runtime that creates `Step1` and `Step2` and forwards events according to the connection attributes.

## Declaring Infrastructure

```csharp
// Logical grouping
[Application("OvenControl")]
public static partial class OvenApp { }

// Physical host
[Device("EdgeNode-01", Address = "192.168.1.50")]
public static partial class EdgeNode01 { }

// Execution context inside the device
[Resource("MainResource", Device = nameof(EdgeNode01))]
public static partial class MainResource { }

// Map function block instances to resources
[Map(Instance = "heater1",   Type = typeof(HeaterBlock),         Resource = nameof(MainResource))]
[Map(Instance = "thermostat", Type = typeof(ThermostatBlock),     Resource = nameof(MainResource))]
public static partial class OvenAppDeployment { }
```

The infrastructure SG reads these and emits the deployment plan, agent registration, and routing tables.

## Wiring Cross-Resource Communication

```csharp
[Map(Instance = "sensor",     Type = typeof(TempSensor),  Resource = nameof(EdgeMain))]
[Map(Instance = "controller", Type = typeof(PidLoop),     Resource = nameof(CloudControl))]

[CrossResourceConnection(
    From = "sensor.Reading",
    To   = "controller.Measurement",
    Transport = Transport.OpcUa)]
public static partial class CrossSiteDeployment { }
```

The SG generates OPC-UA bindings on both ends. The function block code does not change.

## Testing a Function Block in Isolation

```csharp
[Fact]
public async Task Heater_starts_and_drives_temperature()
{
    var heater = HeaterBlock.CreateForTest();
    heater.SetPoint.Set(75.0);

    var result = await heater.FireAsync(HeaterBlock.Start);

    Assert.True(result.IsSuccess);
    Assert.Equal(75.0, heater.Temp.Get());
}
```

`CreateForTest()` returns a fully-wired instance disconnected from any runtime. Fire events directly, assert on output ports.

## Asserting on the ECC

The ECC is a typed FSM under the covers. Every assertion the FSM library supports works:

```csharp
await StateMachineAssert.PathReachesAsync(
    heater.Ecc,
    new[] { HeaterBlock.Start, HeaterBlock.Stop },
    nameof(HeaterBlock.Stopped));
```

For full ECC coverage, enumerate all reachable paths via `StateMachinePathGenerator`.

## Visualization

The generator emits Mermaid for both the ECC and the function block interface. Wire the build to copy them into your docs:

```csharp
var mermaid = MermaidExporter.Export(heater.Ecc.Definition);
File.WriteAllText("docs/heater.mmd", mermaid);
```

For composite blocks, the generator also emits a network diagram showing children and connections.

## Hot-Reload

Deploy a new version of the assembly to the agent's drop folder. The agent unloads the previous version, loads the new one into a fresh assembly load context, migrates state from the old instances to the new ones (where compatible), and resumes processing. No process restart.

## Anti-Patterns

| Don't | Why |
|---|---|
| Make ports non-`static readonly` | The SG discovers them statically; instance fields are invisible at compile time |
| Make the function block class non-`partial` | The generated runtime is a partial; the build will fail |
| Read a `DataInput` outside an algorithm | Inputs are sampled at event time; reads outside that window are racy |
| Drive a `DataOutput` from outside an algorithm | Outputs propagate when an event emits; arbitrary writes break determinism |
| Hand-write a state machine instead of using `[EccState]`/`[Algorithm]` | The SG already builds one; duplication will drift |
| Mix function block code with infrastructure attributes | Keep behavior portable across deployments |
| Block on `await` inside an algorithm | Algorithms run in the runtime's event loop; blocking starves the queue |
| Spawn threads from inside a function block | The runtime owns scheduling; manual threads break determinism |
| Talk directly to OPC-UA from algorithm code | Use the OPC-UA adapter and event ports |
