# IEC61499.2

Architectural specification for an industrial-grade IEC 61499 distributed control platform. Source-generated, AOT-compiled function blocks in C#/.NET with a complete VSCode IDE (12 extensions) covering design, deployment, security, runtime monitoring, debugging, and simulation.

**Status: design/specification phase** -- comprehensive planning documents, no source code yet. See the 13 documents in [doc/](doc/) for the full specification.

## Core Concept

Developers write minimal attribute-decorated C# classes:

```csharp
[FunctionBlock("BinaryOperator")]
public partial class BinaryOperatorBlock<TType> where TType : struct, INumber<TType>
{
    [EventInput(With = [nameof(Left), nameof(Right)])]
    public static readonly EventPort Add, Subtract, Multiply, Divide;

    [EventOutput(With = [nameof(Result)])]
    public static readonly EventPort Computed;

    [DataInput] public static readonly DataPort<TType> Left;
    [DataInput] public static readonly DataPort<TType> Right;
    [DataOutput] public static readonly DataPort<TType> Result;

    [EccState(Initial = true)] private static partial void Idle();
    [EccState] private static partial void Computing();

    [Algorithm(On = nameof(Add), Emits = nameof(Computed))]
    private void AddAlg() => Result.Set(Left.Get() + Right.Get());
}
```

Source generators produce at compile time:
- Strongly typed enums (events, data ports, algorithms)
- Typed ECC state machine (using FiniteStateMachine library)
- Factory methods
- Mermaid diagrams for visualization

## Planned Project Decomposition

| Project | Purpose |
|---------|---------|
| `IEC61499.2` | Core domain models |
| `IEC61499.2.Attributes` | `[FunctionBlock]`, `[EventInput]`, `[EccState]`, etc. |
| `IEC61499.2.SourceGenerator` | Roslyn incremental SG for FB code generation |
| `IEC61499.2.SourceGenerator.Lib` | Reusable emitters (no Roslyn dependency) |
| `IEC61499.2.Infra.Attributes` | `[Application]`, `[Device]`, `[Resource]`, `[Map]` |
| `IEC61499.2.Infra.SourceGenerator` | SG for deployment infrastructure |
| `IEC61499.2.Runtime` | Event-driven execution engine |
| `IEC61499.2.Agent` | Always-running orchestrator (gRPC/REST) |
| `IEC61499.2.OpcUa` | OPC-UA communication layer |
| `IEC61499.2.Testing` | Test helpers for FB validation |

## 12 VSCode Extensions

| # | Extension | Phase |
|---|-----------|-------|
| 1 | **FB Type Designer** | Design: visual function block editor |
| 2 | **ECC Editor** | Design: state machine diagram editor |
| 3 | **Network Editor** | Design: FB network wiring canvas |
| 4 | **Topology Manager** | Design: device/resource topology |
| 5 | **Library Manager** | Design: NuGet-backed FB library |
| 6 | **Language Server** | Design: LSP diagnostics, hover, go-to-definition |
| 7 | **Deployment Manager** | Deploy: Docker/VM/K8s with hot deploy |
| 8 | **Security Manager** | Deploy: OPC-UA certs, secrets, audit |
| 9 | **Runtime Monitor** | Run: live dashboard, event flow, metrics |
| 10 | **Debugger** | Run: step-through FB execution, breakpoints |
| 11 | **Diagnostics** | Monitor: health, connectivity, alarms |
| 12 | **Simulation** | Test: digital twin, record/replay |

## Related Projects

| Project | Relationship |
|---------|-------------|
| **IEC61499** | Reference/proof-of-concept (v1, working code) |
| **IFC61499** | Clean-room production implementation (planned) |
| **IEC61499.2** | Master specification (this project) |
| **FiniteStateMachine** | Typed FSM library used for ECC generation |

## Documentation

- [ARCHITECTURE.md](doc/ARCHITECTURE.md) -- platform layers, SG pipeline, deployment model, agent architecture
- [HOW-TO.md](doc/HOW-TO.md) -- planned developer workflow: create FB, wire network, deploy, monitor
- [PHILOSOPHY.md](doc/PHILOSOPHY.md) -- why source generation, why VSCode, why always-running agent
- [PLAN.md](doc/PLAN.md) -- main architectural plan
- [PLAN-ext-01 through 12](doc/) -- detailed specifications for each VSCode extension
