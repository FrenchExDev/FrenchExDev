# Extension #1: IEC61499.2 FB Type Designer

## Purpose

Visual editor for designing Basic, Composite, and Service Interface function blocks. Bidirectional sync between a graphical FB diagram and C# `[FunctionBlock]` attribute source code.

## Lifecycle phase

Design

## Architecture

```
vscode/iec61499-fb-designer/
+-- package.json                     Extension manifest
+-- src/
|   +-- extension.ts                 Activation, register custom editor
|   +-- FbDesignerProvider.ts        CustomTextEditorProvider implementation
|   +-- FbDesignerDocument.ts        Document model (parse ↔ serialize C# attributes)
|   +-- webview/
|   |   +-- index.html               Webview shell
|   |   +-- App.tsx                   Root React component
|   |   +-- components/
|   |   |   +-- FbCanvas.tsx          Main SVG canvas (drag ports, resize block)
|   |   |   +-- EventPortRow.tsx      Event input/output port row
|   |   |   +-- DataPortRow.tsx       Data input/output port row
|   |   |   +-- WithLine.tsx          WITH qualifier connection line
|   |   |   +-- PortPropertiesPanel.tsx  Side panel: edit port name, type, WITH
|   |   |   +-- FbPropertiesPanel.tsx    Side panel: FB name, subject type
|   |   |   +-- Toolbar.tsx           Add port, delete, undo/redo, zoom
|   |   +-- model/
|   |   |   +-- FbModel.ts            In-memory FB model (ports, WITH, metadata)
|   |   |   +-- CSharpParser.ts       Parse C# source → FbModel
|   |   |   +-- CSharpEmitter.ts      FbModel → C# source (attributes + partial class)
|   |   +-- hooks/
|   |   |   +-- useVsCodeApi.ts        postMessage ↔ extension host
|   |   |   +-- useFbModel.ts          React state management for FbModel
|   |   +-- styles/
|   |       +-- fb-designer.css        Layout, SVG styling
+-- tsconfig.json
+-- webpack.config.js                Bundles webview separately from extension
```

## package.json contribution points

```json
{
  "contributes": {
    "customEditors": [{
      "viewType": "iec61499.fbDesigner",
      "displayName": "IEC61499 Function Block Designer",
      "selector": [{
        "filenamePattern": "*.fb.cs"
      }],
      "priority": "option"
    }],
    "commands": [
      { "command": "iec61499.fbDesigner.open", "title": "Open in FB Designer" },
      { "command": "iec61499.fbDesigner.addEventInput", "title": "Add Event Input Port" },
      { "command": "iec61499.fbDesigner.addEventOutput", "title": "Add Event Output Port" },
      { "command": "iec61499.fbDesigner.addDataInput", "title": "Add Data Input Port" },
      { "command": "iec61499.fbDesigner.addDataOutput", "title": "Add Data Output Port" },
      { "command": "iec61499.fbDesigner.exportPng", "title": "Export FB Diagram as PNG" }
    ],
    "menus": {
      "editor/title": [{
        "command": "iec61499.fbDesigner.open",
        "when": "resourceExtname == .cs && editorTextContains('[FunctionBlock'"
      }]
    }
  }
}
```

## Webview ↔ Extension communication

```
Extension Host (TypeScript)              Webview (React)
+----------------------------+          +---------------------------+
| FbDesignerProvider         |          | App.tsx                   |
|                            |  init    |                           |
| onDidChangeTextDocument() -+--------->| useVsCodeApi().setState() |
|   parse C# → FbModel JSON |          | FbCanvas renders SVG      |
|                            |          |                           |
|                            |  edit    |                           |
| applyEdit(document, edit) <+----------| user drags port, edits    |
|   FbModel JSON → C# source|          | name, adds WITH line      |
+----------------------------+          +---------------------------+
```

**Messages (extension → webview)**:
- `{ type: "init", model: FbModel }` -- initial FB model from parsed C#
- `{ type: "update", model: FbModel }` -- model changed externally (e.g., user edited C# in text editor)

**Messages (webview → extension)**:
- `{ type: "addPort", kind: "eventInput"|"eventOutput"|"dataInput"|"dataOutput", name: string }`
- `{ type: "removePort", kind, name }`
- `{ type: "renamePort", kind, oldName, newName }`
- `{ type: "setWith", eventPort, dataPort, connected: boolean }`
- `{ type: "setFbName", name }`
- `{ type: "setDataPortType", port, typeName }`

## FbModel (shared between webview and extension)

```typescript
interface FbModel {
  name: string;                    // [FunctionBlock("name")]
  subjectType: string;             // record/class name
  eventInputs: PortModel[];
  eventOutputs: PortModel[];
  dataInputs: DataPortModel[];
  dataOutputs: DataPortModel[];
  withQualifiers: WithModel[];     // event ↔ data associations
}

interface PortModel {
  name: string;
  position: { x: number; y: number }; // for layout persistence
}

interface DataPortModel extends PortModel {
  typeName: string;                // e.g., "int", "double", "TimeSpan"
}

interface WithModel {
  eventPort: string;
  dataPort: string;
}
```

## C# parsing and emission

**Parser** (`CSharpParser.ts`):
- Regex-based extraction from C# source (no full Roslyn in TypeScript)
- Finds `[FunctionBlock("name")]` → FB name
- Finds `[EventInput]` / `[EventOutput]` fields → event ports
- Finds `[DataInput]` / `[DataOutput]` fields → data ports with type
- Finds `[EventInput(With = [nameof(X), nameof(Y)])]` → WITH qualifiers
- Falls back gracefully on unparseable code (show raw text editor)

**Emitter** (`CSharpEmitter.ts`):
- Generates valid C# partial class with attributes
- Preserves user-written code (algorithms, guards, subject class) -- only modifies the attribute-decorated section
- Uses marker comments `// <fb-designer-start>` / `// <fb-designer-end>` to delimit managed region

## Visual design

The FB diagram follows the IEC 61499 visual convention:

```
+------------------------------------------+
|              BinaryOperator              |
+==========================================+
| Event In              | Event Out        |
|  ● Add                |        Computed ●|
|  ● Subtract           |                  |
|  ● Multiply           |                  |
|  ● Divide             |                  |
+--------------------+--+------------------+
| Data In            |WITH| Data Out       |
|  ● Left : TType    |~~~~|  Result : TType●|
|  ● Right : TType   |    |                |
+--------------------+----+----------------+
```

- Event ports on top half, data ports on bottom half
- Input on left, output on right
- WITH lines drawn as dashed connections between event and data ports
- Ports are draggable to reorder
- Double-click port to rename/change type
- Right-click context menu for add/remove

## Key decisions

- **Custom Text Editor** (not Custom Editor) -- allows bidirectional: user can switch between visual and text editor for the same `.fb.cs` file
- **Marker comments** for managed region -- prevents the emitter from destroying user-written algorithm code
- **Regex parsing** -- full Roslyn in TypeScript is too heavy; regex is sufficient for the attribute-based declarative style
- **SVG canvas** -- lightweight, no heavy library dependency; React + SVG is well-supported

## Dependencies

- `@vscode/webview-ui-toolkit` -- VSCode-styled UI components
- `react`, `react-dom` -- UI framework
- No additional heavy dependencies (no D3, no canvas library)

## Testing

- Unit tests: `CSharpParser.test.ts` -- parse various FB definitions
- Unit tests: `CSharpEmitter.test.ts` -- emit and re-parse roundtrip
- Unit tests: `FbModel` manipulation (add/remove ports, WITH)
- Integration: open `.fb.cs` file → verify webview renders → add port → verify C# updated
