# IEC61499.2 -- Architecture

## 1. Overview

IEC61499.2 specifies an industrial-grade IEC 61499 distributed control platform. The system compiles declarative C# function block definitions into AOT-compatible runtime code via Roslyn incremental source generators, deploys as Docker containers orchestrated by an always-running agent, and provides a complete IDE through 12 coordinated VSCode extensions.

**Status: specification phase.** This document describes the planned architecture.

---

## 2. Platform Layers

```
Layer 5 — IDE
  12 VSCode extensions (Design + Deploy + Run + Monitor)
  Language Server (LSP, C#-based)

Layer 4 — Entry Points
  CLI (scaffolding, build, deploy)
  Agent (gRPC/REST, always-running on each device)

Layer 3 — Infrastructure Source Generation
  [Application], [Device], [Resource], [Map] attributes
  Infra SG → Docker Compose, deployment manifests, OPC-UA config

Layer 2 — Function Block Source Generation
  [FunctionBlock], [EventInput], [DataOutput], [EccState], [Algorithm]
  FB SG → enums, ECC state machines, factories, Mermaid diagrams

Layer 1 — Runtime
  Event-driven execution engine
  OPC-UA communication backbone

Layer 0 — Core Domain
  IEC 61499 type system (FB types, events, data ports, ECCs)
  FiniteStateMachine library (typed FSM for ECC)
  Result library (validation)
```

---

## 3. Source Generator Pipeline

### Function Block SG (Layer 2)

Input: class decorated with `[FunctionBlock]`, `[EventInput]`, `[DataInput]`, `[EccState]`, `[Algorithm]`, etc.

Output (per FB):
1. **`{Name}.Enums.g.cs`** -- `EventIn`, `EventOut`, `DataIn`, `DataOut`, `Algorithm` enums
2. **`{Name}.Ecc.g.cs`** -- Typed FSM using FiniteStateMachine library (enum states, O(1) transitions)
3. **`{Name}.Factory.g.cs`** -- Factory method for DI registration
4. **`{Name}.Graph.g.cs`** -- Mermaid diagram (compile-time visualization)

All generated code is AOT-compatible (no reflection).

### Infrastructure SG (Layer 3)

Input: classes decorated with `[Application]`, `[Device]`, `[Resource]`, `[Map]`, `[OpcUaServer]`

Output:
- Docker Compose service definitions (via DockerCompose.Bundle)
- OPC-UA endpoint configuration
- Deployment manifests
- Network topology validation

---

## 4. Deployment Model

### Build Time

```
1. Developer writes [FunctionBlock] classes
2. SG generates enums, ECCs, factories at compile
3. dotnet publish → self-contained AOT binary
4. Docker multi-stage build → alpine image
5. Push to container registry
```

### Runtime (Zero-Downtime)

```
1. Agent (always-running service) exposes gRPC/REST API
2. VSCode sends deployment payload:
   - Docker image tag
   - Docker Compose variables
   - Application config
   - OPC-UA certificates
3. Agent writes docker-compose.yml + .env + config
4. Agent runs: docker compose up -d --pull
5. Only changed containers restart
6. Unaffected FBs continue running (zero downtime)
```

### Agent Architecture

The IEC61499.2 Agent runs permanently on each device (physical or VM):

```
Agent
  |-- gRPC/REST API (deployment commands)
  |-- Docker Compose orchestration
  |-- OPC-UA server management
  |-- Health monitoring + heartbeat
  |-- Configuration persistence
  +-- Rollback support (previous compose state)
```

---

## 5. IEC 61499 Type System

### Function Block Types

| Type | Description | Generated |
|------|-------------|-----------|
| **Basic FB** | ECC + algorithms | Full: enums, FSM, algorithms |
| **Composite FB** | Network of sub-FBs | Wiring validation, forwarding |
| **SIFB** | Service Interface FB | External I/O bindings |

### Event/Data Architecture

```
EventInput  →  ECC (state machine)  →  Algorithm  →  EventOutput
                                           ↕
DataInput   ────────────────────────→  DataOutput
```

WITH qualifiers associate data ports with events: when an event fires, only the associated data ports are read/written.

### ECC (Execution Control Chart)

Each FB has a typed finite state machine:
- States are enum values (O(1) lookup)
- Transitions are triggered by input events + guard conditions
- Each state maps to an algorithm
- Generated via FiniteStateMachine library

---

## 6. Communication

### OPC-UA

Industry-standard secure communication between devices:
- Server per device (configurable port, security policy)
- FB data ports exposed as OPC-UA nodes
- Events mapped to OPC-UA events
- Certificate-based authentication

### Internal (within device)

- Direct method calls between FBs in same resource
- Event queue between resources on same device

---

## 7. VSCode IDE Architecture

12 extensions organized by lifecycle phase:

### Design Phase (Ext 1-6)
- **FB Type Designer** -- React + SVG visual editor for function blocks
- **ECC Editor** -- State machine diagrams with live Mermaid preview
- **Network Editor** -- FB wiring canvas (xyflow) with fan-in/fan-out validation
- **Topology Manager** -- Device/resource tree with D3.js diagram
- **Library Manager** -- NuGet-backed FB library, IEC 61499-2 XML import/export
- **Language Server** -- LSP: diagnostics, code actions, hover info, go-to-definition

### Deploy Phase (Ext 7-8)
- **Deployment Manager** -- One-click deploy to Docker/VM/K8s, hot deploy, rollback
- **Security Manager** -- OPC-UA cert management, secret storage, audit trail

### Run & Monitor Phase (Ext 9-12)
- **Runtime Monitor** -- Live WebSocket dashboard: FB states, event flow, data values
- **Debugger** -- Debug Adapter Protocol: step-through execution, ECC breakpoints
- **Diagnostics** -- System health, device connectivity, resource utilization, alarms
- **Simulation** -- Digital twin: simulate FB networks, record/replay test scenarios

See `PLAN-ext-01` through `PLAN-ext-12` for detailed specifications.

---

## 8. Dependency Graph (Planned)

```
Core FB SG:
  IEC61499.2.Attributes      → (no deps, netstandard2.0)
  IEC61499.2.SourceGenerator  → CodeAnalysis, FiniteStateMachine
  IEC61499.2.SourceGenerator.Lib → (no deps, netstandard2.0)

Infrastructure SG:
  IEC61499.2.Infra.Attributes      → IEC61499.2.Attributes
  IEC61499.2.Infra.SourceGenerator → CodeAnalysis, DockerCompose.Bundle

Runtime:
  IEC61499.2.Runtime → IEC61499.2, FiniteStateMachine
  IEC61499.2.Agent   → Runtime, ASP.NET Core, Docker
  IEC61499.2.OpcUa   → OPC Foundation UA .NET Standard SDK

VSCode Extensions:
  Each extension → TypeScript/React, communicates with Agent via REST/gRPC
```
