# Extension #12: IEC61499.2 Simulation

## Purpose

Digital twin: simulate FB network without physical devices. Inject test events, observe outputs. Record/replay scenarios. Compare simulated vs actual behavior.

## Lifecycle phase

Design + Monitor

## Architecture

```
vscode/iec61499-simulation/
+-- package.json
+-- src/
|   +-- extension.ts
|   +-- SimulationPanel.ts             Webview panel
|   +-- SimulationRunner.ts            Manages C# simulation process
|   +-- ScenarioManager.ts             Save/load/replay test scenarios
|   +-- webview/
|       +-- App.tsx
|       +-- components/
|       |   +-- SimulationCanvas.tsx    Network view with live state overlay
|       |   +-- EventInjector.tsx       Inject events into FB inputs
|       |   +-- DataInjector.tsx        Set data port values
|       |   +-- OutputObserver.tsx      Watch output events and data
|       |   +-- ScenarioTimeline.tsx    Record/replay timeline
|       |   +-- ComparisonView.tsx      Side-by-side: simulated vs actual
|       |   +-- StepControls.tsx        Play / Pause / Step / Reset
```

## package.json

```json
{
  "contributes": {
    "commands": [
      { "command": "iec61499.simulation.start", "title": "Start Simulation" },
      { "command": "iec61499.simulation.stop", "title": "Stop Simulation" },
      { "command": "iec61499.simulation.step", "title": "Step Simulation" },
      { "command": "iec61499.simulation.injectEvent", "title": "Inject Event" },
      { "command": "iec61499.simulation.record", "title": "Start Recording Scenario" },
      { "command": "iec61499.simulation.replay", "title": "Replay Scenario" },
      { "command": "iec61499.simulation.compare", "title": "Compare Simulated vs Actual" }
    ]
  }
}
```

## Simulation runtime

The simulation runs the actual compiled C# FB code locally (not on a device):

1. Extension runs `dotnet run --project SimulationHost` in a child process
2. SimulationHost loads the FB network, creates all instances, wires connections
3. Communicates with the extension via stdin/stdout JSON protocol
4. No Docker, no Agent -- pure in-process execution

```
Extension (TypeScript)           SimulationHost (C# process)
+----------------------+        +---------------------------+
| SimulationRunner     |  stdin | Loads FB network          |
|                      |------->| Creates FB instances      |
| Sends: injectEvent   |        | Wires connections         |
| Sends: setData       |        | Processes events          |
| Sends: step          |  stdout| Reports state changes     |
|                      |<-------| Reports output events     |
+----------------------+        +---------------------------+
```

## Protocol messages

**Extension → SimulationHost:**
```json
{ "type": "inject", "instance": "sr1", "event": "S" }
{ "type": "setData", "instance": "calc1", "port": "Left", "value": "42.0" }
{ "type": "step" }
{ "type": "run", "maxEvents": 100, "timeoutMs": 5000 }
{ "type": "reset" }
```

**SimulationHost → Extension:**
```json
{ "type": "stateChange", "instance": "sr1", "eccState": "Set", "timestamp": "..." }
{ "type": "outputEvent", "instance": "sr1", "event": "EO", "timestamp": "..." }
{ "type": "dataChange", "instance": "sr1", "port": "Q", "value": "true" }
{ "type": "done", "eventsProcessed": 5 }
```

## Scenarios

A scenario is a recorded sequence of injected events and expected outputs:

```json
{
  "name": "Set-Reset cycle",
  "steps": [
    { "inject": { "instance": "sr1", "event": "S" } },
    { "expect": { "instance": "sr1", "eccState": "Set" } },
    { "expect": { "instance": "sr1", "port": "Q", "value": "true" } },
    { "inject": { "instance": "sr1", "event": "R1" } },
    { "expect": { "instance": "sr1", "eccState": "Reset" } },
    { "expect": { "instance": "sr1", "port": "Q", "value": "false" } }
  ]
}
```

Scenarios can be:
- Recorded interactively (user injects events, extension records everything)
- Written manually as JSON
- Replayed for regression testing
- Used as integration tests (`dotnet test` runs scenarios headless)

## Comparison view

Side-by-side comparison of simulated vs actual runtime behavior:
- Left panel: simulation state (from SimulationHost)
- Right panel: actual state (from Agent via Extension #9)
- Differences highlighted in red
- Useful for validating that the simulation matches production

## Dependencies

- Child process to `dotnet run`
- `react`, `react-dom`
- Reuses network visualization from Extension #3

## Testing

- Unit: Protocol message serialization/deserialization
- Unit: ScenarioManager save/load/replay
- Integration: start simulation → inject events → verify expected state changes
- Integration: record scenario → replay → verify deterministic output
