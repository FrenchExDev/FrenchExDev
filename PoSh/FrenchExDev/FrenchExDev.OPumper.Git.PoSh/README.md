# FrenchExDev.OPumper.Git.PoSh

Independent **PowerShell 7.4+ Core** provider for OutputPumps. It contributes 4 pumps for Git. DevPoSh is optional and is not a runtime dependency.

Project directory: `FrenchExDev.OPumper.Git.PoSh`. Module entry point: `Git-OPumper.psd1`. The provider ID and pump IDs are unchanged.

Git status analyzes NUL records progressively, retaining at most `MaxItems` paths and counting the complete stream. A limited preview reports `Success` with `ItemLimit` and an exact total, even when the native output exceeds the buffered capture limit. Use `Pump-Me git status -Debug` to inspect the selected source, capture mode, limits and completion counters without exposing native output.

## Activate

Clone this directory as its own repository, or keep it next to the OutputPumps engine. Add its parent to `$HOME/.pumps-config.yaml`:

```yaml
version: 2
roots:
  - path: C:/path/to/providers
providers:
  git:
    enabled: true
    options: {}
pumps: {}
```

Import the engine, then prepare the session:

```powershell
Import-Module 'C:/path/to/FrenchExDev.OutputPumps.PoSh/OutputPump.PoSh.psd1' -DisableNameChecking
Setup-OutputPumps | Out-Null
```

To enable startup, create `$PROFILE` if necessary, edit it with `code $PROFILE`, and paste the same block. The [engine README](../FrenchExDev.OutputPumps.PoSh/README.md) contains the complete profile and dependency setup.

Ordinary module import defines commands without registration. `Setup-OutputPumps` invokes the declared `src/Setup-OutputPumps.ps1`, reusing this module if DevPoSh already loaded it. Registration does not execute native commands. YAML can disable this provider or individual pump identifiers without unloading the module.

## Commands

| Pump ID | Route | Specialized command example |
|---|---|---|
| `git.status` | `git status` | `Get-PumpGitStatus -WorkingDirectory $repo -MaxItems 10` |
| `git.diff` | `git diff` | `Get-PumpGitDiff -WorkingDirectory $repo -Path 'src/'` |
| `git.changes` | `git diff --patch` | `Get-PumpGitChanges -WorkingDirectory $repo -Path 'src/app.cs'` |
| `git.log` | `git log` | `Get-PumpGitLog -WorkingDirectory $repo -MaxItems 5` |

Read [index.md](index.md), then [the software sheet](software/git.md) for official CLI documentation, verified versions, supported formats and arguments. Common limits are 20 items, 8 KiB compact JSON and 600 seconds. Inspect `Outcome`, `Error`, `RetryAdvice` and `Truncation`, then narrow incomplete requests.

Results keep the engine's seven-field envelope. Domain-specific fields belong in `.Data` with a business `PSTypeName`. The provider projects useful information and never exposes native capture buffers.

## Contribution contract

The manifest declares `PrivateData.OutputPumps.Version`, `ProviderId` and `RegistrationScript`. Its setup script accepts the engine's context, enters the provider module scope and creates callbacks with `.GetNewClosure()`. Captured command references retain access to private parser functions. Provider options are data under `$Context.Options`.

The provider requires `OutputPump.PoSh`, which must be imported first or installed on `PSModulePath`. It does not require `FrenchExDev.DevPoSh.PoSh`. DevPoSh may compile it as a normal module; that artifact retains its matching setup script, identity, catalogue and resources.

## Tests and maintenance

Tests live in `test/Unit` with one file per public command. The shared engine runner selects this provider:

```powershell
./FrenchExDev.OutputPumps.PoSh/test/Invoke-Tests.ps1 -Suite Unit -Module FrenchExDev.OPumper.Git.PoSh
./FrenchExDev.OutputPumps.PoSh/test/Invoke-Tests.ps1 -Suite Integration
```

Development uses **Pester 6.2.0** with experimental parallelism disabled. Unit tests mock process execution; integration fixtures and cross-provider scenarios are maintained in the engine harness. Update catalogue, sheet, index and tests together. `Register-OutputPump -Descriptor ... -LibraryRoot ...` validates documentary registration; live registration uses a separate parameter set and writes no files.
