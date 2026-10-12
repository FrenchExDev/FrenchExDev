# Plan: IFC61499 -- Industrial-Grade IEC 61499 Platform

## Context

Build an **industrial-grade** IEC 61499 platform in C#/.NET with:
1. **Source-generated, AOT-compiled** function block code -- no runtime reflection
2. **A full IDE** built on VSCode + extensions covering the complete lifecycle: **Design → Deploy → Secure → Run → Monitor**
3. Inspired by [EcoStruxure Automation Expert](https://www.se.com/us/en/product-range/23643079-ecostruxure-automation-expert/) (Schneider Electric's IEC 61499 IDE) but using open tools: VSCode, C#, FrenchExDev ecosystem

The existing `IEC61499/` project is **reference code only** -- it shows the understanding of function block architecture (enum-generic `IBasicFunctionBlock<6 params>`, ECC, ports, builders, BinaryOperator example). IFC61499 is a clean-room implementation.

## What EcoStruxure Automation Expert does (benchmark)

| Lifecycle phase | EcoStruxure features |
|-----------------|---------------------|
| **Design** | FB type editor (Basic, Composite, SIFB), FB Network editor (visual wiring), Application designer, Topology Manager (devices/resources), Library management, Collaborative engineering (SVN), Reverse engineering (P&ID → logic), Asset-centric approach |
| **Deploy** | Multi-target (physical PLCs, VMs, containers), Soft dPAC (containerized runtime), Online changes (hot deploy), Bulk engineering, Kubernetes/Docker/Portainer/OpenShift |
| **Secure** | OPC-UA auto-generated secure comms, Certificate management, Cybersecurity-by-design |
| **Run** | Distributed runtime (EcoRT), Event-driven execution (IEC 61499), Real-time capable, Multi-vendor hardware |
| **Monitor** | Online debugging (watch/force variables), System diagnostics, Performance monitoring, Historical data, Digital twin / simulation (EcoStruxure Machine Expert Twin) |

## IFC61499 VSCode extensions -- the full IDE

### Complete extension matrix

| # | Extension name | Lifecycle | What it does | VSCode API | Tech stack |
|---|---------------|-----------|-------------|------------|------------|
| 1 | **IFC61499 FB Type Designer** | Design | Visual editor for Basic/Composite/SIFB function blocks. Define event/data ports, WITH qualifiers, ECC state machine. Bidirectional: diagram ↔ C# attributes. | Custom Editor (webview) | React + TypeScript + SVG canvas |
| 2 | **IFC61499 ECC Editor** | Design | State machine diagram editor for ECCs. Add/remove states, transitions, guards, actions. Live Mermaid preview. Generates `[EccState]`/`[EccTransition]` attributes. | Custom Editor (webview) | React + TypeScript + state-machine-cat |
| 3 | **IFC61499 Application Network Editor** | Design | Visual canvas for FB network wiring. Drag FB instances, draw event/data connections, validate fan-in/fan-out rules live. Generates `[EventWire]`/`[DataWire]` attributes. | Custom Editor (webview) | React + TypeScript + react-flow/xyflow |
| 4 | **IFC61499 Topology Manager** | Design + Deploy | Device/Resource topology editor. Visual tree of System → Devices → Resources. Map applications to resources. Configure communication endpoints. | TreeView + webview panel | React + TypeScript + D3.js |
| 5 | **IFC61499 Library Manager** | Design | Browse, create, version, share reusable FB type libraries. NuGet-backed package management for FB libraries. Import/export IEC 61499-2 XML. | TreeView + QuickPick | TypeScript + NuGet CLI |
| 6 | **IFC61499 Language Server** | Design | LSP for C# files with `[FunctionBlock]` attributes. Diagnostics: unreachable ECC states, unconnected ports, fan-in violations, type mismatches. Code actions: "Add WITH", "Generate transition". Hover: port types, ECC diagram. | Language Server Protocol | C# (OmniSharp-based) |
| 7 | **IFC61499 Deployment Manager** | Deploy | One-click deploy to targets: Docker containers, VMs (Packer/Vagrant), bare-metal, Kubernetes. Hot deploy (online changes). Bulk deployment. Rollback. Uses FrenchExDev DockerCompose/Packer/Vos. | TreeView + terminal + webview | TypeScript + Docker API + SSH |
| 8 | **IFC61499 Security Manager** | Secure | OPC-UA certificate management. Auto-generate TLS certificates for device communication. Audit trail of deployments. Secure boot verification. Secret management (connection strings, tokens). | TreeView + webview | TypeScript + OpenSSL + mkcert |
| 9 | **IFC61499 Runtime Monitor** | Run + Monitor | Live dashboard: FB instance states, event flow visualization, data port values. Watch/force variables. Event trace timeline. Performance metrics (event latency, queue depth). | Webview panel | React + TypeScript + WebSocket |
| 10 | **IFC61499 Debugger** | Run + Monitor | Step-through debugging of FB execution. Breakpoints on ECC transitions and algorithm entry. Event flow stepping (step to next FB in chain). Integrates with C# debugger. | Debug Adapter Protocol | C# DAP extension |
| 11 | **IFC61499 Diagnostics** | Monitor | System health dashboard. Device connectivity status. Resource utilization (CPU, memory per resource). Communication diagnostics (OPC-UA, MQTT). Alarm management. | Webview panel | React + TypeScript + Grafana embeds |
| 12 | **IFC61499 Simulation** | Design + Monitor | Digital twin: simulate FB network without physical devices. Inject test events, observe outputs. Record/replay scenarios. Compare simulated vs actual behavior. | Webview panel + terminal | C# simulation runtime + React UI |

### Mapping: EcoStruxure → IFC61499 VSCode extensions

| EcoStruxure feature | IFC61499 extension(s) |
|--------------------|-----------------------|
| FB type editor | #1 FB Type Designer + #2 ECC Editor |
| FB Network editor | #3 Application Network Editor |
| Topology Manager | #4 Topology Manager |
| Library management | #5 Library Manager |
| Collaborative engineering (SVN) | Git (built-in VSCode) + #5 Library Manager (NuGet) |
| Reverse engineering (P&ID → logic) | Future: #5 Library Manager import plugin |
| Multi-target deployment | #7 Deployment Manager |
| Soft dPAC (container runtime) | #7 Deployment Manager (Docker/K8s targets) |
| Online changes | #7 Deployment Manager (hot deploy) |
| OPC-UA secure comms | #8 Security Manager |
| Certificate management | #8 Security Manager |
| Distributed runtime (EcoRT) | IFC61499.Runtime C# project + #7 deploy |
| Online debugging | #10 Debugger |
| System diagnostics | #11 Diagnostics |
| Performance monitoring | #9 Runtime Monitor |
| Digital twin / simulation | #12 Simulation |

## SG architecture -- industrial grade

### Developer writes (input):

```csharp
// Function block definition -- declarative, minimal
[FunctionBlock("BinaryOperator")]
public partial class BinaryOperatorBlock<TType>
    where TType : struct, INumber<TType>
{
    // Subject holds domain state
    public record BinaryOperation(TType Left, TType Right, TType Result);

    // Ports -- attributes only, no builder boilerplate
    [EventInput(With = [nameof(Left), nameof(Right)])]
    public static readonly EventPort Add, Subtract, Multiply, Divide;

    [EventOutput(With = [nameof(Result)])]
    public static readonly EventPort Computed;

    [DataInput] public static readonly DataPort<TType> Left;
    [DataInput] public static readonly DataPort<TType> Right;
    [DataOutput] public static readonly DataPort<TType> Result;

    // ECC -- real state machine
    [EccState(Initial = true)] private static partial void Idle();
    [EccState] private static partial void Computing();

    [EccTransition(From = nameof(Idle), To = nameof(Computing), On = nameof(Add))]
    [EccTransition(From = nameof(Idle), To = nameof(Computing), On = nameof(Subtract))]
    [EccTransition(From = nameof(Idle), To = nameof(Computing), On = nameof(Multiply))]
    [EccTransition(From = nameof(Idle), To = nameof(Computing), On = nameof(Divide))]
    [EccTransition(From = nameof(Computing), To = nameof(Idle), On = "1")]
    private static partial void DefineTransitions();

    // Algorithms -- the only hand-written logic
    [Algorithm(On = nameof(Add), Emits = nameof(Computed))]
    private void AddAlg() => Result.Set(Left.Get() + Right.Get());

    [Algorithm(On = nameof(Divide), Emits = nameof(Computed), Guard = nameof(NotZero))]
    private void DivideAlg() => Result.Set(Left.Get() / Right.Get());

    private bool NotZero() => !Right.Get().Equals(default);
}
```

### SG generates (output) -- AOT-friendly, zero reflection:

**`BinaryOperatorBlock.Enums.g.cs`**

```csharp
public partial class BinaryOperatorBlock<TType>
{
    public enum EventIn { Add, Subtract, Multiply, Divide }
    public enum EventOut { Computed }
    public enum DataIn { Left, Right }
    public enum DataOut { Result }
    public enum Algorithm { Add, Subtract, Multiply, Divide }
    public enum EccState { Idle, Computing }
}
```

**`BinaryOperatorBlock.Ecc.g.cs`** -- typed FSM via FiniteStateMachine

```csharp
public partial class BinaryOperatorBlock<TType>
{
    private readonly StateMachineEngine<EccState, EventIn> _ecc;

    private StateMachineEngine<EccState, EventIn> CreateEcc()
    {
        var def = new TypedStateMachineBuilder<EccState, EventIn>()
            .InitialState(EccState.Idle)
            .When(EccState.Idle)
                .On(EventIn.Add).TransitionTo(EccState.Computing)
                .On(EventIn.Subtract).TransitionTo(EccState.Computing)
                .On(EventIn.Multiply).TransitionTo(EccState.Computing)
                .On(EventIn.Divide)
                    .WithGuard(async (_, _, _, _) => NotZero())
                    .TransitionTo(EccState.Computing)
            .When(EccState.Computing)
                .OnEntry(async (state, evt, ct) => DispatchAlgorithm(evt, ct))
            .Build().Value;
        return def.CreateMachine();
    }

    private void DispatchAlgorithm(EventIn evt, CancellationToken ct)
    {
        switch (evt)
        {
            case EventIn.Add: AddAlg(); break;
            case EventIn.Subtract: SubtractAlg(); break;
            case EventIn.Multiply: MultiplyAlg(); break;
            case EventIn.Divide: DivideAlg(); break;
        }
        _outputEvents.Enqueue(EventOut.Computed);
    }

    public async Task<Result<Transition<EccState>>> FireAddAsync(CancellationToken ct = default)
        => await _ecc.FireAsync(EventIn.Add, ct);
    // ... FireSubtract, FireMultiply, FireDivide, CanFire* ...

    public EccState CurrentState => _ecc.CurrentState;
}
```

**`BinaryOperatorBlock.Factory.g.cs`** -- eliminates the 50-line manual Factory()

```csharp
public partial class BinaryOperatorBlock<TType>
{
    private static readonly FunctionBlockId _id = FunctionBlockId.Create<BinaryOperatorBlock<TType>>("BinaryOperator");

    public static BinaryOperatorBlock<TType> Create(BinaryOperation? subject = null)
    {
        subject ??= new BinaryOperation(default, default, default);
        var instance = new BinaryOperatorBlock<TType>(subject);
        instance._ecc = instance.CreateEcc();
        return instance;
    }
}
```

**`BinaryOperatorBlock.Graph.g.cs`** -- compile-time visualization

```csharp
public partial class BinaryOperatorBlock<TType>
{
    public static class Graph
    {
        public const string EccMermaid = """
            stateDiagram-v2
                [*] --> Idle
                Idle --> Computing : Add
                Idle --> Computing : Subtract
                Idle --> Computing : Multiply
                Idle --> Computing : Divide [NotZero]
                Computing --> Idle : 1
            """;

        public const string InterfaceMermaid = """
            graph LR
                subgraph BinaryOperator
                    subgraph "Event In"
                        Add; Subtract; Multiply; Divide
                    end
                    subgraph "Data In"
                        Left; Right
                    end
                    subgraph "Event Out"
                        Computed
                    end
                    subgraph "Data Out"
                        Result
                    end
                end
                Add -.WITH.-> Left & Right
                Computed -.WITH.-> Result
            """;
    }
}
```

### Infrastructure .Design (input):

```csharp
[Application("TemperatureControl")]
public partial class TempControlApp
{
    [Instance] public SetResetBlock sr1;
    [Instance] public BinaryOperatorBlock<double> calc1;

    [EventWire(nameof(sr1), "EO", nameof(calc1), "Add")]
    [DataWire(nameof(sr1), "Q", nameof(calc1), "Left")]
    private static partial void DefineWiring();
}

[Device("EdgeController_01", Runtime = RuntimeTarget.Docker)]
public partial class EdgeController
{
    [Resource("Control")] public ControlResource control;
    [Resource("Monitoring")] public MonitoringResource monitoring;

    [Map(typeof(TempControlApp), Resource = nameof(control))]
    private static partial void DefineMappings();

    [OpcUaServer(Port = 4840, SecurityPolicy = SecurityPolicy.Basic256Sha256)]
    private static partial void DefineCommunication();
}
```

### How deployment actually works (industrial-grade)

**Principle**: Industrial systems never stop. There is NO "deploy and restart". Instead:

**Phase A -- Build time (.Design → Docker image)**:

1. `.Design` CLI (or VSCode extension) **scaffolds a new solution**:
   - Creates `slnx`, a new lib project (`MyPlant.FunctionBlocks.csproj`)
   - Adds `[FunctionBlock]` definitions as source files
   - Adds `[Application]` network definitions
   - References `IFC61499.Attributes` + `IFC61499.SourceGenerator` NuGet packages

2. **SG runs at compile time** → all enums, ECCs, factories, wiring are generated (AOT, zero reflection)

3. **`dotnet publish`** → self-contained binary

4. **Docker image** built from the published binary (multi-stage Dockerfile, alpine-based)

5. Image pushed to registry (local or remote)

**Phase B -- Runtime (always-running orchestrator service)**:

An **IFC61499 Agent** service runs permanently on each device/edge node. It:

1. Exposes an API (gRPC or REST) that receives **deployment payloads**:
   - Docker Compose variable values (image tag, resource limits, port mappings, env vars)
   - Application configuration (which FBs to activate, initial data values)
   - Network topology (which resources connect to which)

2. On receiving a payload, the Agent:
   - Writes `docker-compose.yml` (via DockerCompose.Bundle SG'd models)
   - Writes `.env` file with variable values
   - Writes any additional config files (OPC-UA certs, FB init params)
   - Runs `docker compose up -d --pull` (pull new image, recreate only changed containers)

3. The Agent **monitors** the running containers:
   - Health checks
   - Restart policies
   - Log aggregation
   - Feeds status back to the VSCode Diagnostics/Monitor extensions

4. For **updates** (new FB version, config change):
   - New payload arrives → new compose file → `docker compose up -d --pull`
   - Only affected containers are recreated (rolling update)
   - Zero downtime for unaffected FBs

```
   VSCode IDE                    Registry              Edge Device
   +------------------+         +---------+         +-------------------+
   | .Design project  |         | Docker  |         | IFC61499 Agent    |
   | [FunctionBlock]  |  build  | Registry|  pull   | (always running)  |
   | [Application]    | ------> | myplant |-------->|                   |
   | [Device]         |  push   | :v1.2.3 |         | Receives payload  |
   +------------------+         +---------+         | Writes compose.yml|
          |                                         | Writes .env       |
          | deploy payload (gRPC/REST)              | Runs docker       |
          +---------------------------------------->|   compose up -d   |
                                                    |   --pull          |
                                                    |                   |
                                                    | Monitors health   |
                                                    | Reports status    |
                                                    +-------------------+
```

### Infrastructure SG generates:

- `TempControlApp.Network.g.cs` -- validated wiring, connection graph
- `TempControlApp.Mermaid.g.cs` -- network diagram as const string
- `EdgeController.ComposeModel.g.cs` -- typed DockerCompose.Bundle model for the device's compose file
- `EdgeController.Agent.g.cs` -- Agent API endpoint definitions (what payloads this device accepts)
- `EdgeController.OpcUa.g.cs` -- OPC-UA server config with certificate setup

## Project structure

```
IFC61499/
+-- FrenchExDev.Net.IFC61499.slnx
+-- README.md
+-- doc/
|   +-- ARCHITECTURE.md
|   +-- HOW-TO.md
|   +-- PHILOSOPHY.md
|   +-- PLAN.md                                      This file
|   +-- spec/                                         IEC reference PDFs
+-- src/
|   +-- FrenchExDev.Net.IFC61499/                     Core domain model
|   +-- FrenchExDev.Net.IFC61499.Attributes/          [FunctionBlock], [EventInput], [EccState], etc.
|   +-- FrenchExDev.Net.IFC61499.SourceGenerator/     FB source generator (Roslyn incremental, AOT)
|   +-- FrenchExDev.Net.IFC61499.SourceGenerator.Lib/ Reusable emitters (no Roslyn, netstandard2.0)
|   +-- FrenchExDev.Net.IFC61499.Infra.Attributes/    [Application], [Device], [Resource], [Map]
|   +-- FrenchExDev.Net.IFC61499.Infra.SourceGenerator/ Infrastructure SG
|   +-- FrenchExDev.Net.IFC61499.Runtime/              Event-driven execution engine
|   +-- FrenchExDev.Net.IFC61499.Agent/                Always-running orchestrator (gRPC/REST)
|   +-- FrenchExDev.Net.IFC61499.OpcUa/                OPC-UA communication layer
|   +-- FrenchExDev.Net.IFC61499.Testing/              Test helpers for FB verification
+-- test/
|   +-- FrenchExDev.Net.IFC61499.Tests/
+-- vscode/
    +-- ifc61499-fb-designer/                          Extension #1
    +-- ifc61499-ecc-editor/                           Extension #2
    +-- ifc61499-network-editor/                       Extension #3
    +-- ifc61499-topology-manager/                     Extension #4
    +-- ifc61499-library-manager/                      Extension #5
    +-- ifc61499-language-server/                       Extension #6
    +-- ifc61499-deployment-manager/                   Extension #7
    +-- ifc61499-security-manager/                     Extension #8
    +-- ifc61499-runtime-monitor/                      Extension #9
    +-- ifc61499-debugger/                             Extension #10
    +-- ifc61499-diagnostics/                          Extension #11
    +-- ifc61499-simulation/                           Extension #12
```

## Dependencies

```
FrenchExDev.Net.IFC61499
+-- FrenchExDev.Net.FiniteStateMachine         Typed FSM for ECC
+-- FrenchExDev.Net.Result                     Validation

FrenchExDev.Net.IFC61499.SourceGenerator.Lib
+-- FrenchExDev.Net.Builder.SourceGenerator.Lib         BuilderEmitter
+-- FrenchExDev.Net.FiniteStateMachine.SourceGenerator.Lib  StateMachineEmitter

FrenchExDev.Net.IFC61499.Infra.SourceGenerator
+-- FrenchExDev.Net.DockerCompose.Bundle       Container deployment
+-- FrenchExDev.Net.Packer.Bundle              VM image building
+-- FrenchExDev.Net.Vos                        VM orchestration

FrenchExDev.Net.IFC61499.Agent
+-- FrenchExDev.Net.DockerCompose.Bundle       Compose file generation
+-- Grpc.AspNetCore or ASP.NET Core            API layer

FrenchExDev.Net.IFC61499.OpcUa
+-- OPC Foundation UA .NET Standard SDK
```

## Key design decisions

- **VSCode is the IDE** -- "ceinture et bretelles": C# type system + SG compile-time checks + VSCode IntelliSense + Mermaid visualization = industrial-grade safety without proprietary tooling
- **SG over reflection** -- all FB code generated at compile time, AOT-compatible, zero runtime reflection
- **Typed FSM (enum-based)** -- industry uses strongly typed state machines; O(1) transition lookup via TransitionTable
- **Always-running Agent** -- industrial systems never stop; the Agent receives payloads and orchestrates containers without downtime
- **Docker Compose as deployment unit** -- not raw Docker; compose files with variable substitution for config changes
- **FrenchExDev infra reuse** -- DockerCompose.Bundle, Packer.Bundle, Vos for deployment targets
- **OPC-UA as communication backbone** -- industry standard, auto-generated secure comms

## IEC 61499 concept coverage

| IEC 61499 Concept | Status | Implementation |
|-------------------|--------|----------------|
| Basic Function Block | Phase 1 | SG from `[FunctionBlock]` attributes |
| Composite Function Block | Phase 1 | SG from `[Application]` + wiring attributes |
| Service Interface FB | Phase 1 | SG from `[ServiceInterfaceBlock]` |
| ECC (state machine) | Phase 1 | Real Typed FSM via FiniteStateMachine SG |
| Event ports + WITH | Phase 1 | Declarative `[EventInput(With=...)]` |
| Data ports | Phase 1 | Declarative `[DataInput]`/`[DataOutput]` |
| FB Network | Phase 1 | `[EventWire]`/`[DataWire]` → generated network |
| Device / Resource | Phase 2 | `[Device]`/`[Resource]` → generated deployment |
| Application mapping | Phase 2 | `[Map]` → generated runtime bootstrap |
| Deployment (containers) | Phase 2 | IFC61499 Agent + DockerCompose.Bundle |
| OPC-UA communication | Phase 2 | Auto-generated secure comms |
| Adapter (socket/plug) | Phase 3 | Bidirectional grouped connections |
| IDE (visual editors) | Phase 3+ | VSCode extensions #1-#12 |
| Simulation / Digital twin | Phase 4 | Extension #12 |
