# Extension #9: IEC61499.2 Runtime Monitor

## Purpose

Live dashboard showing FB instance states, event flow visualization, data port values. Watch/force variables. Event trace timeline. Performance metrics (event latency, queue depth).

## Lifecycle phase

Run + Monitor

## Architecture

```
vscode/iec61499-runtime-monitor/
+-- package.json
+-- src/
|   +-- extension.ts
|   +-- MonitorPanel.ts               Creates webview panel
|   +-- AgentConnection.ts            WebSocket/gRPC stream to Agent
|   +-- webview/
|   |   +-- App.tsx
|   |   +-- components/
|   |   |   +-- FbInstanceList.tsx     List of running FB instances + ECC state
|   |   |   +-- EventFlowGraph.tsx    Live animated event flow between FBs
|   |   |   +-- DataPortTable.tsx     Current data port values (watch)
|   |   |   +-- EventTimeline.tsx     Time-series of events (sparkline)
|   |   |   +-- PerformanceMetrics.tsx Event latency, queue depth, throughput
|   |   |   +-- ForceValueDialog.tsx  Force a data port value for debugging
|   |   |   +-- FilterBar.tsx         Filter by device, resource, FB type
|   |   +-- model/
|   |       +-- RuntimeState.ts       Live state model (updated via stream)
```

## package.json

```json
{
  "contributes": {
    "commands": [
      { "command": "iec61499.monitor.open", "title": "Open Runtime Monitor" },
      { "command": "iec61499.monitor.watchPort", "title": "Watch Data Port" },
      { "command": "iec61499.monitor.forceValue", "title": "Force Data Port Value" },
      { "command": "iec61499.monitor.clearForce", "title": "Clear Forced Values" }
    ]
  }
}
```

## Agent → Extension streaming protocol

The Agent exposes a streaming endpoint (WebSocket or gRPC server-streaming):

```typescript
interface RuntimeSnapshot {
  timestamp: string;
  device: string;
  resource: string;
  instances: FbInstanceState[];
  eventQueue: { depth: number; processedPerSecond: number };
}

interface FbInstanceState {
  name: string;
  typeName: string;
  eccState: string;                   // current ECC state enum name
  dataPorts: { name: string; value: string; direction: "in" | "out" }[];
  lastEvent: { name: string; timestamp: string } | null;
  forcedPorts: string[];              // ports currently forced
}
```

Stream delivers snapshots at configurable interval (default 1s).

## Visual design

```
+------------------------------------------------------------------+
| Runtime Monitor                        [EdgeController_01] [▼]   |
+------------------------------------------------------------------+
| FB Instances              | Data Ports (sr1)                     |
| ● sr1      [Set]  ●live  | Port    Dir  Value   Forced          |
| ● calc1    [Idle] ●live  | Q       out  true    -               |
| ● sensor1  [Read] ●live  | EO      out  (event) -               |
+---------------------------+--------------------------------------+
| Event Timeline                                                    |
| sr1.S ──→ sr1.EO ──→ calc1.Add ──→ calc1.Computed     [14:30:01]|
| sr1.R1 ──→ sr1.EO ──→ calc1.Add ──→ calc1.Computed    [14:30:02]|
+------------------------------------------------------------------+
| Performance        | Event queue: 0  | Throughput: 42 evt/s      |
|                    | Avg latency: 2ms | Max latency: 8ms         |
+------------------------------------------------------------------+
```

## Force values

For debugging, users can force a data port value:
1. Right-click on a data port → "Force Value"
2. Enter value → sent to Agent
3. Agent overrides the port value until cleared
4. Forced ports highlighted in orange in the UI

## Dependencies

- `ws` or `@grpc/grpc-js` -- streaming from Agent
- `recharts` or `lightweight-charts` -- sparkline/timeline charts
- `react`, `react-dom`

## Testing

- Unit: RuntimeState model updates from snapshots
- Unit: EventTimeline rendering with mock data
- Integration: connect to running Agent → verify live updates displayed
