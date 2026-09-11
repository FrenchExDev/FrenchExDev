# Extension #4: IEC61499.2 Topology Manager

## Purpose

Device/Resource topology editor. Visual tree of System → Devices → Resources. Map applications to resources. Configure communication endpoints. Generates `[Device]`, `[Resource]`, `[Map]`, `[OpcUaServer]` attributes.

## Lifecycle phase

Design + Deploy

## Architecture

```
vscode/iec61499-topology-manager/
+-- package.json
+-- src/
|   +-- extension.ts
|   +-- TopologyTreeProvider.ts       TreeDataProvider (sidebar tree)
|   +-- TopologyEditorProvider.ts     Webview panel (visual topology)
|   +-- webview/
|   |   +-- App.tsx
|   |   +-- components/
|   |   |   +-- TopologyDiagram.tsx   Visual device/resource layout (D3.js)
|   |   |   +-- DeviceNode.tsx        Device box with resource slots
|   |   |   +-- ResourceSlot.tsx      Resource inside device, shows mapped apps
|   |   |   +-- ApplicationBadge.tsx  Draggable app badge for mapping
|   |   |   +-- CommunicationLink.tsx Network connection between devices
|   |   |   +-- PropertiesPanel.tsx   Device/resource/mapping properties
|   |   +-- model/
|   |       +-- TopologyModel.ts      Devices, resources, mappings, comms
|   |       +-- CSharpParser.ts       Parse [Device]/[Resource] classes
|   |       +-- CSharpEmitter.ts      Model → C# attributes
```

## package.json

```json
{
  "contributes": {
    "viewsContainers": {
      "activitybar": [{
        "id": "iec61499-topology",
        "title": "IEC61499 Topology",
        "icon": "resources/topology.svg"
      }]
    },
    "views": {
      "iec61499-topology": [{
        "id": "iec61499.topologyTree",
        "name": "System Topology"
      }]
    },
    "commands": [
      { "command": "iec61499.topology.addDevice", "title": "Add Device" },
      { "command": "iec61499.topology.addResource", "title": "Add Resource" },
      { "command": "iec61499.topology.mapApplication", "title": "Map Application to Resource" },
      { "command": "iec61499.topology.openDiagram", "title": "Open Topology Diagram" }
    ]
  }
}
```

## TopologyModel

```typescript
interface TopologyModel {
  system: string;
  devices: DeviceModel[];
  communications: CommunicationModel[];
}

interface DeviceModel {
  name: string;
  runtime: "docker" | "vm" | "baremetal" | "kubernetes";
  address: string;                    // IP/hostname
  resources: ResourceModel[];
  opcUa?: OpcUaConfig;
}

interface ResourceModel {
  name: string;
  mappedApplications: string[];       // application class names
}

interface CommunicationModel {
  from: string;                       // device name
  to: string;                         // device name
  protocol: "opcua" | "mqtt" | "grpc";
  port: number;
}

interface OpcUaConfig {
  port: number;
  securityPolicy: string;
}
```

## Visual design

Sidebar tree:
```
▼ System: MyPlant
  ▼ Device: EdgeController_01 [Docker]
    ▼ Resource: Control
      ● TemperatureControlApp (mapped)
    ▼ Resource: Monitoring
      (empty -- drag app here)
  ▼ Device: PLC_02 [Bare-metal]
    ▼ Resource: Main
      ● PressureControlApp (mapped)
  ▼ Communications
    EdgeController_01 ↔ PLC_02 [OPC-UA :4840]
```

Webview diagram: devices as large boxes containing resource slots, with network lines between devices.

## Integration

- Reads `[Application]` classes from workspace to populate the "available apps" palette
- Generates `[Device]` and `[Resource]` C# classes with appropriate attributes
- Feeds device addresses to Extension #7 (Deployment Manager) for deploy targets

## Dependencies

- `d3` -- topology diagram rendering
- `react`, `react-dom`

## Testing

- Unit: TopologyModel CRUD operations
- Unit: CSharpParser/Emitter roundtrip
- Integration: add device → add resource → map app → verify C# generated
