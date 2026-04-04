# IEC61499.2 -- Philosophy

## Source generation over runtime reflection

Industrial control systems run for years without restart. Runtime reflection is slow, unpredictable, and incompatible with AOT compilation. Source generation moves all type discovery, validation, and code synthesis to compile time.

A `[FunctionBlock]` attribute at compile time produces enum types, state machine code, factory methods, and Mermaid diagrams. At runtime, there is only concrete code -- no attribute scanning, no dynamic dispatch, no reflection overhead.

This is the same principle as Builder, Injectable, and Entity.Dsl in the FrenchExDev ecosystem: declare intent via attributes, let the source generator handle the how.

---

## Typed state machines, not stringly-typed event tables

Traditional IEC 61499 implementations use string-based event routing: an event named "REQ" arrives, the runtime looks up a handler in a dictionary. Typos compile. Missing handlers are discovered at runtime. Dead transitions are invisible.

IEC61499.2 generates enum types for events and states. A transition from `State.Idle` to `State.Computing` on `EventIn.Add` is a compile-time constant. The FiniteStateMachine library provides O(1) lookup. Invalid transitions are compiler errors, not runtime exceptions.

The ECC is a typed finite state machine. Every state, every transition, every algorithm binding is verified at compile time.

---

## VSCode is the IDE, not a proprietary tool

Industrial automation has historically required proprietary IDEs (Schneider's EcoStruxure, Beckhoff's TwinCAT, Siemens' TIA Portal). These are expensive, closed-source, and platform-locked.

IEC61499.2 uses VSCode with 12 purpose-built extensions. The FB Type Designer, ECC Editor, and Network Editor provide visual editing comparable to proprietary tools. The Language Server provides diagnostics and navigation. The Deployment Manager handles one-click deploys.

The advantage is openness: any developer with VSCode can contribute. Extensions are TypeScript/React -- the most widely known frontend stack. The C# backend uses standard .NET tooling (dotnet build, dotnet publish, Docker).

---

## Always-running Agent, never restart

Industrial systems have one non-negotiable constraint: they must not stop. A factory production line, a building HVAC system, a water treatment plant -- downtime means material damage or safety risk.

The IEC61499.2 Agent is an always-running service on each device. Deployment is not "stop, replace, start." It is "pull new image, recreate only changed containers, leave everything else running." Docker Compose's declarative model makes this natural: `docker compose up -d` only touches containers whose definition changed.

This means a developer can update one function block on one device without affecting any other function block on any other device. Zero-downtime deployment is not a feature -- it is a requirement.

---

## Attributes are the single source of truth

A function block's interface, state machine, and wiring are all defined via C# attributes on a single partial class. There is no separate XML file, no graphical model stored in a proprietary format, no configuration that can drift from the code.

The VSCode extensions read and write these attributes. The ECC Editor visualizes `[EccState]` and `[EccTransition]` attributes. The Network Editor visualizes `[EventWire]` and `[DataWire]` attributes. The visual representation is always in sync because it is generated from the same source.

This eliminates the classic IEC 61499 problem of visual model and runtime behavior diverging. The code is the model. The model is the code.

---

## 12 extensions because separation of concerns applies to tools too

One monolithic extension would be simpler to ship but harder to maintain, harder to test, and harder for users who only need part of the functionality. A library developer needs the FB Type Designer and Language Server. A deployment engineer needs the Deployment Manager and Security Manager. A plant operator needs the Runtime Monitor and Diagnostics.

Each extension has a clear responsibility, a clear lifecycle phase, and a clear user persona. They communicate through the Language Server and Agent APIs, not through shared state. They can be installed independently.

This is the same SOLID principle applied to tooling: single responsibility, open for extension, dependent on abstractions (LSP, DAP, REST/gRPC).
