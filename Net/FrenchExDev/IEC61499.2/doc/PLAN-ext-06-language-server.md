# Extension #6: IEC61499.2 Language Server

## Purpose

Language Server Protocol implementation for IEC 61499 C# files. Provides diagnostics (unreachable ECC states, unconnected ports, fan-in violations), code actions, hover info, and go-to-definition for FB connections.

## Lifecycle phase

Design

## Architecture

```
vscode/iec61499-language-server/
+-- package.json
+-- client/                           VSCode extension (TypeScript)
|   +-- src/
|       +-- extension.ts              Start LSP client
+-- server/                           Language server (C#)
    +-- FrenchExDev.Net.IEC61499.2.LanguageServer.csproj
    +-- Program.cs                    LSP host (Microsoft.Extensions.LanguageServer)
    +-- Analyzers/
    |   +-- EccReachabilityAnalyzer.cs      Detect unreachable ECC states
    |   +-- PortConnectionAnalyzer.cs       Detect unconnected ports
    |   +-- FanInViolationAnalyzer.cs        Detect data fan-in violations
    |   +-- TypeMismatchAnalyzer.cs          Detect data wire type mismatches
    |   +-- WithQualifierAnalyzer.cs         Detect missing WITH associations
    |   +-- TransitionCompletenessAnalyzer.cs  Warn on states with no outgoing transitions
    +-- CodeActions/
    |   +-- AddWithQualifierAction.cs        Quick fix: add missing WITH
    |   +-- GenerateTransitionAction.cs      Generate ECC transition stub
    |   +-- GenerateAlgorithmAction.cs       Generate algorithm method stub
    |   +-- ConnectPortAction.cs             Wire unconnected port
    +-- Hover/
    |   +-- FbHoverProvider.cs               Show FB interface summary on hover
    |   +-- PortHoverProvider.cs             Show port type, WITH associations
    |   +-- EccHoverProvider.cs              Show ECC Mermaid diagram on hover
    +-- Completion/
    |   +-- PortNameCompletionProvider.cs     Autocomplete port names in [EventWire]
    |   +-- FbTypeCompletionProvider.cs       Autocomplete FB type names in [Instance]
    +-- Navigation/
        +-- GoToConnectionDefinition.cs      Navigate from wire → source/target FB
        +-- GoToAlgorithmDefinition.cs       Navigate from ECC action → algorithm method
```

## package.json

```json
{
  "contributes": {
    "languages": [{
      "id": "iec61499-cs",
      "aliases": ["IEC 61499 C#"],
      "extensions": [".fb.cs", ".app.cs", ".device.cs"]
    }]
  },
  "activationEvents": [
    "workspaceContains:**/*.fb.cs",
    "workspaceContains:**/[FunctionBlock]*"
  ]
}
```

## Diagnostics

| Code | Severity | Description | Quick fix |
|------|----------|-------------|-----------|
| IEC001 | Error | Data fan-in: port `{name}` has multiple sources | Remove extra wire |
| IEC002 | Error | Type mismatch: `{sourceType}` → `{targetType}` | -- |
| IEC003 | Warning | Unreachable ECC state: `{name}` | Add transition to state |
| IEC004 | Warning | Dead-end ECC state: `{name}` has no outgoing transitions | Add transition from state |
| IEC005 | Warning | Event port `{name}` has no WITH association | Add WITH qualifier |
| IEC006 | Info | Unconnected event input `{name}` | Wire port |
| IEC007 | Error | Algorithm `{name}` referenced in [EccAction] but method not found | Generate stub |
| IEC008 | Error | Guard `{name}` referenced in [EccTransition] but method not found | Generate stub |
| IEC009 | Warning | ECC has no initial state | Mark a state as `Initial = true` |

## Hover info

On `[FunctionBlock]` class:
```
BinaryOperatorBlock<TType>
━━━━━━━━━━━━━━━━━━━━━━━━━
Event In:  Add, Subtract, Multiply, Divide
Event Out: Computed
Data In:   Left (TType), Right (TType)
Data Out:  Result (TType)
ECC:       2 states, 5 transitions
```

On `[EccState]`:
```
stateDiagram-v2
  [*] --> Idle
  Idle --> Computing : Add | Subtract | Multiply | Divide
  Computing --> Idle : 1
```
(Rendered as Mermaid in hover markdown)

## Server implementation

The language server is written in **C#** (not TypeScript) because:
- It needs to understand C# syntax (Roslyn APIs)
- It shares models with the SG (reuse `IEC61499.2.SourceGenerator.Lib` POCOs)
- It can run as a self-contained binary (`dotnet publish`)

Uses `Microsoft.Extensions.LanguageServer` (or `OmniSharp.Extensions.LanguageServer`) for LSP protocol handling.

## Communication

```
VSCode                          Language Server (C# process)
+------------------+           +---------------------------+
| LSP Client       |  stdio    | LSP Host                  |
| (TypeScript)     |<--------->| Roslyn workspace           |
| extension.ts     |  JSON-RPC | Analyzes [FunctionBlock]   |
+------------------+           | classes on file change     |
                               +---------------------------+
```

## Dependencies

**Client (TypeScript):**
- `vscode-languageclient`

**Server (C#):**
- `Microsoft.CodeAnalysis.CSharp` (Roslyn)
- `OmniSharp.Extensions.LanguageServer` or `Microsoft.Extensions.LanguageServer`
- `FrenchExDev.Net.IEC61499.2.SourceGenerator.Lib` (shared models)

## Testing

- Unit: each analyzer with test C# snippets
- Unit: code action generation
- Integration: open workspace with FB files → verify diagnostics appear → apply quick fix → verify fix
