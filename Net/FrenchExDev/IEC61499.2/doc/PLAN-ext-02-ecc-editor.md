# Extension #2: IEC61499.2 ECC Editor

## Purpose

State machine diagram editor for Execution Control Charts. Visually add/remove states, transitions, guards, and actions. Live Mermaid preview. Generates `[EccState]`/`[EccTransition]`/`[Algorithm]` attributes in C#.

## Lifecycle phase

Design

## Architecture

```
vscode/iec61499-ecc-editor/
+-- package.json
+-- src/
|   +-- extension.ts
|   +-- EccEditorProvider.ts         CustomTextEditorProvider
|   +-- webview/
|   |   +-- App.tsx
|   |   +-- components/
|   |   |   +-- EccCanvas.tsx        State diagram SVG canvas
|   |   |   +-- StateNode.tsx        Draggable state circle/rect
|   |   |   +-- TransitionArrow.tsx  Directed edge with label
|   |   |   +-- ActionBadge.tsx      Algorithm + output event badge on state
|   |   |   +-- GuardLabel.tsx       [guard] condition on transition
|   |   |   +-- MermaidPreview.tsx   Live Mermaid rendering panel
|   |   |   +-- PropertiesPanel.tsx  Edit state/transition properties
|   |   |   +-- Toolbar.tsx          Add state, add transition, auto-layout
|   |   +-- model/
|   |   |   +-- EccModel.ts          States, transitions, actions, guards
|   |   |   +-- CSharpParser.ts      Parse [EccState]/[EccTransition] → model
|   |   |   +-- CSharpEmitter.ts     Model → C# attributes
|   |   |   +-- MermaidEmitter.ts    Model → Mermaid stateDiagram-v2
|   |   +-- layout/
|   |       +-- AutoLayout.ts        Dagre/elk-based automatic layout
```

## package.json contribution points

```json
{
  "contributes": {
    "customEditors": [{
      "viewType": "iec61499.eccEditor",
      "displayName": "IEC61499 ECC Editor",
      "selector": [{ "filenamePattern": "*.fb.cs" }],
      "priority": "option"
    }],
    "commands": [
      { "command": "iec61499.eccEditor.open", "title": "Open ECC Editor" },
      { "command": "iec61499.eccEditor.addState", "title": "Add ECC State" },
      { "command": "iec61499.eccEditor.addTransition", "title": "Add ECC Transition" },
      { "command": "iec61499.eccEditor.autoLayout", "title": "Auto-Layout ECC" },
      { "command": "iec61499.eccEditor.exportMermaid", "title": "Copy ECC as Mermaid" }
    ]
  }
}
```

## EccModel

```typescript
interface EccModel {
  states: EccStateModel[];
  transitions: EccTransitionModel[];
}

interface EccStateModel {
  name: string;
  isInitial: boolean;
  actions: EccActionModel[];
  position: { x: number; y: number };
}

interface EccActionModel {
  algorithm: string;       // method name
  outputEvent: string | null;  // event port name or null
}

interface EccTransitionModel {
  from: string;
  to: string;
  on: string;              // event name or "1" (unconditional)
  guard: string | null;    // guard method name or null
}
```

## Webview ↔ Extension messages

**Extension → Webview:**
- `{ type: "init", ecc: EccModel, ports: { eventInputs: string[], eventOutputs: string[] } }`
- `{ type: "update", ecc: EccModel }`

**Webview → Extension:**
- `{ type: "addState", name, isInitial }`
- `{ type: "removeState", name }`
- `{ type: "addTransition", from, to, on, guard? }`
- `{ type: "removeTransition", from, to, on }`
- `{ type: "addAction", state, algorithm, outputEvent? }`
- `{ type: "moveState", name, position }`

## Visual design

```
    +-------+                    +----------+
    | START |------- S --------->|   Set    |
    +-------+     (initial)      | [SetAlg] |
                                 | → EO     |
                                 +----------+
                                   |     ^
                                  R1     S
                                   v     |
                                 +----------+
                                 |  Reset   |
                                 | [RstAlg] |
                                 | → EO     |
                                 +----------+
```

- States as rounded rectangles with name, actions listed inside
- Initial state has a `[*] →` entry arrow
- Transitions as directed arrows with event label
- Guards shown as `[guardName]` on the transition label
- Actions shown as `algorithm → outputEvent` inside the state
- Unconditional transitions labeled `1`
- Split view: diagram on left, Mermaid preview on right

## Auto-layout

- Uses `elkjs` (Eclipse Layout Kernel in JS) for directed graph layout
- Layered layout (top-to-bottom or left-to-right, user toggle)
- Preserves manual position adjustments (stored in model)

## Integration with Extension #1 (FB Type Designer)

- When both extensions are open on the same `.fb.cs` file, they share the document
- Adding an event port in #1 automatically makes it available in the ECC transition dropdown
- Adding an algorithm in the ECC editor creates a stub `[Algorithm]` method in C#

## Dependencies

- `elkjs` -- automatic graph layout
- `react`, `react-dom`
- `mermaid` -- for live Mermaid rendering in the preview pane

## Testing

- Unit: EccModel manipulation (add/remove states, transitions)
- Unit: CSharpParser roundtrip
- Unit: MermaidEmitter output matches expected diagram
- Integration: open file → add state → verify C# `[EccState]` attribute added
