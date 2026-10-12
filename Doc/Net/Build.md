# Build .NET 10 and .NET 11

Run from `Net/FrenchExDev` with the .NET 11 RC SDK selected by `global.json`:

```powershell
.\Rebuild-All.ps1
```

The script requires `dotnet` and `rg`. It discovers every source `.csproj`, including projects missing from `FrenchExDev.Net.slnx`, creates a temporary solution, restores serially, and rebuilds every declared target framework. It stops with an error if restore or build fails. Use `-Configuration Release` for a release build.

Runtime libraries, applications and tests target `net10.0;net11.0`. Projects already exposing `netstandard2.0` retain that target. Roslyn generators, analyzers and their compilation libraries remain on `netstandard2.0` so compiler hosts can load them. The preview/RC version belongs to the SDK selection; the target framework is `net11.0`.

For a single solution:

```powershell
dotnet restore .\BinaryWrapper\FrenchExDev.Net.BinaryWrapper.slnx --disable-parallel
dotnet build .\BinaryWrapper\FrenchExDev.Net.BinaryWrapper.slnx --no-restore --no-incremental
dotnet test .\BinaryWrapper\FrenchExDev.Net.BinaryWrapper.slnx --no-build --no-restore
```

Multi-targeted executables need an explicit framework:

```powershell
dotnet run --project .\Docker\src\FrenchExDev.Net.Docker.Design --framework net10.0 -- --help
```

`Find-Missing.ps1` defaults to `net10.0` and accepts `-Framework net11.0`.

BinaryWrapper regenerates commands from existing `scrape/*.json` during compilation; no scraping is needed for a generator change. Explicitly emitted sources are stored in `obj/<Configuration>/<TargetFramework>/Generated`, isolating concurrent framework builds.

# BinaryWrapper command environment

Generated commands expose `Environment`, and generated builders expose `WithEnvironment(...)`:

```csharp
var command = await client.RunAsync(b => b.WithEnvironment(
    new Dictionary<string, string> { ["APP_MODE"] = "test" }));
var result = await executor.ExecuteAsync(binding.Identifier, command);
```

`CommandExecutor` copies binding defaults and then command variables into `ProcessSpec.EnvironmentVariables`. Command values win on duplicate names. Windows names compare without case; other platforms compare ordinally. The input dictionaries are unchanged, and the runner receives a read-only snapshot.

The same merge applies to raw execution, streaming, and result collection. The injected `IProcessRunner` receives the merged specification. `SystemProcessRunner` applies it to `ProcessStartInfo.Environment`, preserving inherited process variables that were not overridden. Environment entries are never appended to CLI arguments.
