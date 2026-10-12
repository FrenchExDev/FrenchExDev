# Extension #3: IEC61499.2 Application Network Editor

## Purpose

Visual canvas for designing FB networks (IEC 61499 Applications). Drag FB instances, draw event/data connections between ports, validate fan-in/fan-out rules live. Generates `[Application]`, `[Instance]`, `[EventWire]`, `[DataWire]` attributes.

## Lifecycle phase

Design

## Architecture

```
vscode/iec61499-network-editor/
+-- package.json
+-- src/
|   +-- extension.ts
|   +-- NetworkEditorProvider.ts
|   +-- webview/
|   |   +-- App.tsx
|   |   +-- components/
|   |   |   +-- NetworkCanvas.tsx      react-flow/xyflow canvas
|   |   |   +-- FbInstanceNode.tsx     Custom node: shows FB interface (ports)
|   |   |   +-- EventEdge.tsx          Red dashed edge (event connection)
|   |   |   +-- DataEdge.tsx           Blue solid edge (data connection)
|   |   |   +-- FbPalette.tsx          Side panel: available FB types (from project)
|   |   |   +-- ConnectionValidator.tsx Live validation overlay (red on violations)
|   |   |   +-- PropertiesPanel.tsx    Instance name, initial values
|   |   |   +-- MermaidPreview.tsx     Network diagram as Mermaid
|   |   +-- model/
|   |   |   +-- NetworkModel.ts        Instances, event wires, data wires
|   |   |   +-- CSharpParser.ts        Parse [Application] class → model
|   |   |   +-- CSharpEmitter.ts       Model → C# attributes
|   |   |   +-- ValidationEngine.ts    Fan-in/fan-out rules, type checking
|   |   +-- hooks/
|   |       +-- useFbTypeDiscovery.ts   Discover available [FunctionBlock] types in workspace
```

## package.json

```json
{
  "contributes": {
    "customEditors": [{
      "viewType": "iec61499.networkEditor",
      "displayName": "IEC61499 Application Network Editor",
      "selector": [{ "filenamePattern": "*.app.cs" }],
      "priority": "option"
    }],
    "commands": [
      { "command": "iec61499.networkEditor.open", "title": "Open Network Editor" },
      { "command": "iec61499.networkEditor.validate", "title": "Validate Network" },
      { "command": "iec61499.networkEditor.autoLayout", "title": "Auto-Layout Network" }
    ]
  }
}
```

## NetworkModel

```typescript
interface NetworkModel {
  name: string;                        // [Application("name")]
  instances: FbInstanceModel[];
  eventWires: WireModel[];
  dataWires: WireModel[];
}

interface FbInstanceModel {
  name: string;                        // field name
  typeName: string;                    // C# type (e.g., "BinaryOperatorBlock<double>")
  position: { x: number; y: number };
  // Resolved at edit time from workspace:
  resolvedPorts?: {
    eventInputs: string[];
    eventOutputs: string[];
    dataInputs: DataPortInfo[];
    dataOutputs: DataPortInfo[];
  };
}

interface WireModel {
  fromInstance: string;
  fromPort: string;
  toInstance: string;
  toPort: string;
}

interface DataPortInfo {
  name: string;
  typeName: string;
}
```

## Validation rules (enforced live)

| Rule | Severity | Description |
|------|----------|-------------|
| Data fan-in | Error | Each data input port can have at most one source |
| Event fan-in | OK | Event inputs can receive from multiple sources |
| Data fan-out | OK | Data outputs can feed multiple inputs |
| Type mismatch | Error | Data wire source and target must have compatible types |
| Unconnected mandatory port | Warning | Event input without source (may be intentional for external input) |
| Self-loop | Warning | FB instance wired to itself |

## FB type discovery

The extension scans the workspace for `[FunctionBlock]` classes and extracts their port definitions:
1. Glob `**/*.fb.cs` + `**/*.cs` containing `[FunctionBlock`
2. Regex-parse port declarations (`[EventInput]`, `[DataOutput]`, etc.)
3. Cache in memory, refresh on file change via `FileSystemWatcher`
4. Available types shown in the FbPalette sidebar

## Visual design

Uses `reactflow` (xyflow) for the node-graph canvas:

```
+---[SetResetBlock: sr1]---+          +---[BinaryOperator: calc1]---+
|  EI: S    |    EO: EO ---+--(event)--+-> EI: Add                  |
|  EI: R1   |              |          |    EI: Subtract              |
|-----------|--------------|          |    EI: Multiply              |
|           |   DO: Q -----+--(data)--+-> DI: Left                  |
|           |              |          |    DI: Right     DO: Result  |
+-----------+--------------+          +-----------------------------+
```

- Event edges: red dashed lines
- Data edges: blue solid lines
- Ports shown as small circles on the node borders
- Drag from output port to input port to create wire
- Red highlight on validation errors
- Minimap in bottom-right corner

## Dependencies

- `reactflow` / `@xyflow/react` -- node-graph canvas
- `elkjs` -- auto-layout
- `react`, `react-dom`

## Testing

- Unit: NetworkModel manipulation, validation rules
- Unit: CSharpParser/Emitter roundtrip
- Unit: Type compatibility checking
- Integration: open `.app.cs` → add instances → wire → validate → verify C#
