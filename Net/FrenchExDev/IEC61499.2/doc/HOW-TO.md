# IEC61499.2 -- Developer Guide (HOW-TO)

**Status: specification phase.** This document describes the planned developer workflow.

---

## Table of Contents

1. [Creating a Function Block](#1-creating-a-function-block)
2. [Defining an ECC](#2-defining-an-ecc)
3. [Writing Algorithms](#3-writing-algorithms)
4. [Wiring a Network](#4-wiring-a-network)
5. [Defining an Application](#5-defining-an-application)
6. [Defining a Device](#6-defining-a-device)
7. [Building and Publishing](#7-building-and-publishing)
8. [Deploying](#8-deploying)
9. [Monitoring](#9-monitoring)
10. [Testing](#10-testing)

---

## 1. Creating a Function Block

Decorate a partial class with `[FunctionBlock]`:

```csharp
[FunctionBlock("TemperatureSensor")]
public partial class TemperatureSensorBlock
{
    [EventInput]
    public static readonly EventPort Read;

    [EventOutput(With = [nameof(Temperature)])]
    public static readonly EventPort Updated;

    [DataOutput]
    public static readonly DataPort<double> Temperature;

    [EccState(Initial = true)] private static partial void Idle();
    [EccState] private static partial void Reading();

    [EccTransition(From = nameof(Idle), To = nameof(Reading), On = nameof(Read))]

    [Algorithm(On = nameof(Read), Emits = nameof(Updated))]
    private void ReadSensor()
    {
        Temperature.Set(GetSensorValue());
    }
}
```

The source generator produces:
- `TemperatureSensorBlock.Enums.g.cs` -- typed enums for events and data ports
- `TemperatureSensorBlock.Ecc.g.cs` -- finite state machine (Idle → Reading)
- `TemperatureSensorBlock.Factory.g.cs` -- DI-compatible factory
- `TemperatureSensorBlock.Graph.g.cs` -- Mermaid diagram

---

## 2. Defining an ECC

The ECC (Execution Control Chart) is a state machine. States and transitions are declared via attributes:

```csharp
[EccState(Initial = true)]
private static partial void Idle();

[EccState]
private static partial void Computing();

[EccState]
private static partial void Error();

// Transitions: From → To, triggered by event, optional guard
[EccTransition(From = nameof(Idle), To = nameof(Computing), On = nameof(Start))]
[EccTransition(From = nameof(Computing), To = nameof(Idle), On = nameof(Done))]
[EccTransition(From = nameof(Computing), To = nameof(Error), On = nameof(Fault))]
[EccTransition(From = nameof(Error), To = nameof(Idle), On = nameof(Reset))]
```

The SG generates a typed FSM using the FiniteStateMachine library with O(1) transition lookup.

---

## 3. Writing Algorithms

Algorithms are methods triggered by events:

```csharp
[Algorithm(On = nameof(Add), Emits = nameof(Computed))]
private void AddAlg()
{
    Result.Set(Left.Get() + Right.Get());
}

[Algorithm(On = nameof(Divide), Emits = nameof(Computed))]
private void DivideAlg()
{
    var right = Right.Get();
    if (right == default)
    {
        // Error handling via event emission
        return;
    }
    Result.Set(Left.Get() / right);
}
```

WITH qualifiers control which data ports are read/written when an event fires.

---

## 4. Wiring a Network

Composite FBs wire sub-FBs together:

```csharp
[CompositeFunctionBlock("TemperatureControl")]
public partial class TempControlBlock
{
    [Instance] public TemperatureSensorBlock sensor;
    [Instance] public BinaryOperatorBlock<double> comparator;
    [Instance] public SetResetBlock actuator;

    [EventWire(nameof(sensor), "Updated", nameof(comparator), "Add")]
    [DataWire(nameof(sensor), "Temperature", nameof(comparator), "Left")]
    private static partial void DefineWiring();
}
```

The SG validates:
- Port type compatibility
- No fan-in on data inputs
- All required connections present
- No orphaned ports

---

## 5. Defining an Application

Applications group FB instances and wiring:

```csharp
[Application("TemperatureControl")]
public partial class TempControlApp
{
    [Instance] public TempControlBlock controller;
    [Instance] public AlarmBlock alarm;

    [EventWire(nameof(controller), "HighTemp", nameof(alarm), "Trigger")]
    private static partial void DefineWiring();
}
```

---

## 6. Defining a Device

Devices map applications to resources:

```csharp
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

---

## 7. Building and Publishing

```bash
# Build with SG
dotnet build

# Publish AOT binary
dotnet publish -c Release

# Build Docker image
docker build -t myregistry/temp-control:1.0 .

# Push to registry
docker push myregistry/temp-control:1.0
```

---

## 8. Deploying

### Via CLI

```bash
iec61499 deploy --device EdgeController_01 \
  --image myregistry/temp-control:1.0 \
  --agent http://192.168.56.10:5000
```

### Via VSCode

Use the Deployment Manager extension (Ext #7): select device, select image, click Deploy. The extension communicates with the Agent via REST/gRPC.

### What happens

1. Agent receives deployment payload
2. Agent writes `docker-compose.yml` + `.env` + config files
3. Agent runs `docker compose up -d --pull`
4. Only changed containers restart (zero downtime)

---

## 9. Monitoring

### Via VSCode

Runtime Monitor (Ext #9) provides a live WebSocket dashboard:
- FB state visualization (current ECC state)
- Event flow tracing (event → algorithm → output)
- Data port values (real-time updates)
- Performance metrics (execution time, event frequency)

### Via Debugger

Debugger (Ext #10) supports:
- ECC breakpoints (break when entering a state)
- Event breakpoints (break when event fires)
- Step-through execution (event by event)
- Data port inspection

---

## 10. Testing

### Unit testing FBs

```csharp
[Fact]
public void AddAlgorithm_SetsResult()
{
    var fb = BinaryOperatorBlockFactory.Create<double>();
    fb.Left.Set(3.0);
    fb.Right.Set(4.0);
    fb.Fire(EventIn.Add);

    fb.Result.Get().ShouldBe(7.0);
}
```

### Simulation

Simulation extension (Ext #12) provides:
- Digital twin: simulate FB networks without hardware
- Record/replay: capture event sequences, replay for regression
- Fault injection: simulate sensor failures, network partitions
