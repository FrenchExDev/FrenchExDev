# Extension #5: IEC61499.2 Library Manager

## Purpose

Browse, create, version, and share reusable FB type libraries. NuGet-backed package management for FB libraries. Import/export IEC 61499-2 XML (Phase 2).

## Lifecycle phase

Design

## Architecture

```
vscode/iec61499-library-manager/
+-- package.json
+-- src/
|   +-- extension.ts
|   +-- LibraryTreeProvider.ts        TreeDataProvider for sidebar
|   +-- NuGetClient.ts                Interact with NuGet feeds (local + remote)
|   +-- FbTypeScanner.ts              Scan workspace for [FunctionBlock] types
|   +-- LibraryPackager.ts            Pack FB types into NuGet package
|   +-- Xml/
|       +-- Iec61499XmlImporter.ts    Parse IEC 61499-2 XML → [FunctionBlock] C# (Phase 2)
|       +-- Iec61499XmlExporter.ts    [FunctionBlock] C# → IEC 61499-2 XML (Phase 2)
```

## package.json

```json
{
  "contributes": {
    "viewsContainers": {
      "activitybar": [{
        "id": "iec61499-libraries",
        "title": "IEC61499 Libraries",
        "icon": "resources/library.svg"
      }]
    },
    "views": {
      "iec61499-libraries": [
        { "id": "iec61499.localFbTypes", "name": "Local FB Types" },
        { "id": "iec61499.installedLibraries", "name": "Installed Libraries" },
        { "id": "iec61499.availableLibraries", "name": "Available Libraries" }
      ]
    },
    "commands": [
      { "command": "iec61499.library.createPackage", "title": "Pack FB Library" },
      { "command": "iec61499.library.publish", "title": "Publish FB Library" },
      { "command": "iec61499.library.install", "title": "Install FB Library" },
      { "command": "iec61499.library.importXml", "title": "Import IEC 61499 XML" },
      { "command": "iec61499.library.exportXml", "title": "Export as IEC 61499 XML" }
    ]
  }
}
```

## Library structure

An FB library is a NuGet package containing:
```
MyCompany.FunctionBlocks.1.0.0.nupkg
+-- lib/
|   +-- netstandard2.0/
|       +-- MyCompany.FunctionBlocks.dll    (compiled FB types + SG attributes)
+-- contentFiles/
|   +-- cs/
|       +-- any/
|           +-- FBTypes/
|               +-- TemperatureSensor.fb.cs   (source for reference)
+-- iec61499/
    +-- TemperatureSensor.xml                  (IEC 61499-2 XML, Phase 2)
```

## Tree view structure

```
▼ Local FB Types (this project)
  ● BinaryOperatorBlock<T>  [Basic]
  ● SetResetBlock           [Basic]
  ● TempControlApp          [Composite]
▼ Installed Libraries
  ▼ FrenchExDev.IEC61499.StandardLib 1.0.0
    ● E_CYCLE               [Basic]
    ● E_DELAY               [Basic]
    ● E_SR                  [Basic]
    ● F_ADD                 [Basic]
  ▼ MyCompany.SafetyBlocks 2.1.0
    ● SF_EmergencyStop      [Basic]
    ● SF_GuardMonitoring    [Basic]
▼ Available (NuGet feed)
  ○ MyCompany.MotorControl 1.0.0  [Install]
```

## NuGet integration

- Uses `dotnet nuget` CLI via child process
- Supports local feeds (file-based, like FrenchExDev's `__Local_Nuget_Registry__`)
- Supports remote feeds (nuget.org, private Artifactory/Nexus)
- Package creation: `dotnet pack` with custom `.nuspec` including `iec61499/` folder

## Dependencies

- `@vscode/webview-ui-toolkit`
- Child process calls to `dotnet nuget`, `dotnet pack`

## Testing

- Unit: FbTypeScanner finds all `[FunctionBlock]` types
- Unit: NuGetClient list/install/publish
- Integration: create library → pack → install in another project → verify types available
