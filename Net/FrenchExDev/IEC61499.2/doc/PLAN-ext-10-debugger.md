# Extension #10: IEC61499.2 Debugger

## Purpose

Step-through debugging of FB execution. Breakpoints on ECC transitions and algorithm entry. Event flow stepping (step to next FB in chain). Integrates with the C# debugger.

## Lifecycle phase

Run + Monitor

## Architecture

```
vscode/iec61499-debugger/
+-- package.json
+-- src/
|   +-- extension.ts
|   +-- Iec61499DebugAdapterFactory.ts   Creates debug session
|   +-- Iec61499DebugSession.ts          Debug Adapter Protocol implementation
|   +-- EccBreakpointManager.ts          Map ECC state/transition to C# source lines
|   +-- EventFlowStepper.ts             Step through event propagation chain
```

## package.json

```json
{
  "contributes": {
    "debuggers": [{
      "type": "iec61499",
      "label": "IEC61499 FB Debugger",
      "languages": ["csharp"],
      "configurationAttributes": {
        "launch": {
          "properties": {
            "program": { "type": "string", "description": "Path to compiled FB runtime" },
            "device": { "type": "string", "description": "Target device address" },
            "resource": { "type": "string", "description": "Resource to debug" }
          }
        }
      }
    }],
    "breakpoints": [{ "language": "csharp" }],
    "commands": [
      { "command": "iec61499.debug.stepEvent", "title": "Step to Next Event" },
      { "command": "iec61499.debug.stepFb", "title": "Step to Next FB in Chain" },
      { "command": "iec61499.debug.breakOnTransition", "title": "Break on ECC Transition" }
    ]
  }
}
```

## Debug modes

### 1. Local debug (development)

Standard C# debugging with IEC 61499 overlays:
- Breakpoints on `[Algorithm]` methods
- ECC state shown in debug sidebar
- Data port values shown as watch variables

### 2. Remote debug (production)

Connect to running Agent via debug protocol:
- Attach to containerized runtime
- Set breakpoints on ECC transitions (Agent injects transition listeners)
- Event flow stepping across FB chain

## ECC-aware breakpoints

Beyond standard line breakpoints, the extension supports:
- **Transition breakpoint**: break when ECC moves from state A to state B
- **State entry breakpoint**: break when entering a specific ECC state
- **Event breakpoint**: break when a specific event fires on any FB instance

These are implemented by:
1. Mapping ECC states/transitions to generated C# source line numbers
2. Setting conditional breakpoints on the `StateMachineEngine.FireAsync()` calls
3. Using the `IStateMachineListener` hooks for remote debugging

## launch.json example

```json
{
  "type": "iec61499",
  "request": "launch",
  "name": "Debug TemperatureControl",
  "program": "${workspaceFolder}/bin/Debug/net10.0/MyPlant.dll",
  "device": "localhost",
  "resource": "Control"
}
```

## Dependencies

- VSCode Debug Adapter Protocol
- `@vscode/debugadapter` -- DAP base classes
- Underlying C# debugger (coreclr) for local debugging

## Testing

- Unit: EccBreakpointManager maps states to source lines
- Unit: EventFlowStepper follows event chain correctly
- Integration: launch debug → set transition breakpoint → trigger event → verify break
