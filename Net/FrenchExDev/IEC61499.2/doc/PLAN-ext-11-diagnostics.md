# Extension #11: IEC61499.2 Diagnostics

## Purpose

System health dashboard. Device connectivity status. Resource utilization (CPU, memory per resource). Communication diagnostics (OPC-UA, MQTT). Alarm management.

## Lifecycle phase

Monitor

## Architecture

```
vscode/iec61499-diagnostics/
+-- package.json
+-- src/
|   +-- extension.ts
|   +-- DiagnosticsPanel.ts            Webview panel
|   +-- HealthPoller.ts                Polls Agent health endpoints
|   +-- AlarmManager.ts                Collects and filters alarms
|   +-- webview/
|       +-- App.tsx
|       +-- components/
|       |   +-- SystemOverview.tsx      All devices at a glance (green/yellow/red)
|       |   +-- DeviceDetail.tsx        CPU, memory, disk, network per device
|       |   +-- ContainerHealth.tsx     Per-container status, restart count, logs
|       |   +-- OpcUaDiagnostics.tsx    Connection status, session count, errors
|       |   +-- AlarmTable.tsx          Active/acknowledged/historical alarms
|       |   +-- AlarmRuleEditor.tsx     Define alarm thresholds and notifications
```

## package.json

```json
{
  "contributes": {
    "commands": [
      { "command": "iec61499.diagnostics.open", "title": "Open System Diagnostics" },
      { "command": "iec61499.diagnostics.checkAll", "title": "Check All Devices" },
      { "command": "iec61499.diagnostics.viewLogs", "title": "View Container Logs" }
    ]
  }
}
```

## Agent health API

The Agent exposes health endpoints:

```
GET /health              → { status: "healthy", uptime: "14d 3h", version: "1.0.0" }
GET /health/containers   → [{ name, status, restartCount, cpuPercent, memoryMb }]
GET /health/opcua        → { connected: true, sessions: 3, lastError: null }
GET /health/events       → { queueDepth: 0, processedTotal: 142857, errorsTotal: 0 }
GET /alarms              → [{ id, severity, source, message, timestamp, acknowledged }]
POST /alarms/{id}/ack    → acknowledge alarm
```

## Alarm rules

```typescript
interface AlarmRule {
  name: string;
  condition: "cpu > 80%" | "memory > 90%" | "restartCount > 3" | "queueDepth > 100" | "custom";
  severity: "info" | "warning" | "critical";
  notification: "statusbar" | "popup" | "sound";
}
```

Default rules:
- CPU > 80% → warning
- Memory > 90% → critical
- Container restart count > 3 in 1h → critical
- Event queue depth > 100 → warning
- OPC-UA disconnected → critical

## Visual design

```
+------------------------------------------------------------------+
| System Diagnostics                                                |
+------------------------------------------------------------------+
| Devices                                                           |
| [●] EdgeController_01  CPU: 23%  MEM: 45%  ▲ 14d 3h   3 containers |
| [●] PLC_02             CPU: 12%  MEM: 30%  ▲ 28d 1h   2 containers |
| [○] TestBench_03       OFFLINE since 2026-03-22 16:00             |
+------------------------------------------------------------------+
| Active Alarms                                                     |
| ⚠ WARNING  PLC_02/sensor1  Queue depth 87 (threshold: 100)  14:29|
| ✗ CRITICAL TestBench_03    Device offline                    16:00|
+------------------------------------------------------------------+
| Container Logs (EdgeController_01)                                |
| [14:30:01] INFO  ResourceRuntime started on Control               |
| [14:30:01] INFO  3 FB instances loaded                            |
| [14:30:02] INFO  OPC-UA server listening on :4840                 |
+------------------------------------------------------------------+
```

## Dependencies

- `node-fetch` or `@grpc/grpc-js` -- Agent health API
- `recharts` -- CPU/memory charts
- `react`, `react-dom`

## Testing

- Unit: HealthPoller handles offline/online transitions
- Unit: AlarmManager evaluates rules correctly
- Integration: connect to Agent → verify health data displayed → trigger alarm → verify notification
