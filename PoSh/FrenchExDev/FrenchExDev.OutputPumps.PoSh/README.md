# OutputPumps

Turn noisy CLI output into small PowerShell objects that an agent can use directly.

```powershell
$result = Pump-Me git status -WorkingDirectory ./my-project
$result.Data.Branch
$result.Data.Items
```

OutputPumps captures a supported command, parses its useful fields, removes secrets and returns one bounded result. It does not replace your terminal commands. **PowerShell 7.4+ Core is required.**

## Quick start without DevPoSh

Import the engine from your checkout. Install `powershell-yaml` explicitly if it is unavailable; startup never installs dependencies.

```powershell
Install-Module powershell-yaml -RequiredVersion 0.4.12 -Scope CurrentUser
Import-Module 'C:\path\FrenchExDev.OutputPumps.PoSh\OutputPump.PoSh.psd1' -DisableNameChecking
```

Create `$HOME/.pumps-config.yaml`. Roots are directories to search for provider modules. Relative paths are resolved from this YAML file.

```yaml
version: 2
roots:
  - path: C:/code/FrenchExDev/FrenchExDev_i2/PoSh/FrenchExDev
    enabled: true
providers:
  git:
    enabled: true
    options: {}
  pnpm:
    enabled: true
    options: {}
pumps:
  git.status: true
```

Providers and pumps are enabled unless explicitly disabled. An empty configuration with `roots: []`, `providers: {}` and `pumps: {}` is valid. Missing or invalid configuration produces a failure and prevents native passthrough.

Create and edit your profile:

```powershell
New-Item -ItemType Directory -Path (Split-Path $PROFILE) -Force | Out-Null
if (!(Test-Path -LiteralPath $PROFILE)) {
    New-Item -ItemType File -Path $PROFILE | Out-Null
}
code $PROFILE
```

Paste this block, adjusting the engine path:

```powershell
Import-Module 'C:\path\FrenchExDev.OutputPumps.PoSh\OutputPump.PoSh.psd1' `
    -DisableNameChecking
Setup-OutputPumps | Out-Null
```

Open a new shell, or run the block in your current session. Inspect activation when troubleshooting:

```powershell
Setup-OutputPumps | ConvertTo-OutputPumpJson
Get-OutputPump | ConvertTo-OutputPumpJson
Pump-Me git status -WorkingDirectory . | ConvertTo-OutputPumpJson
```

Cloning `FrenchExDev.OPumper.Git.PoSh` into a configured root makes its contribution discoverable at the next `Setup-OutputPumps`, including startup in a new shell. Providers are trusted PowerShell code: configuration selects where that code is discovered.

## Independent providers

The engine owns configuration, the session registry, dispatch, bounded process capture and the result contract. CLI-specific code lives in independent sibling modules:

Project directories follow `FrenchExDev.OPumper.<Name>.PoSh`. Module entry points keep their existing names, such as `Git-OPumper.psd1`, and registry IDs remain unchanged (`git`, `git.status`, etc.).

| Provider | Pumps |
|---|---|
| [FrenchExDev.OPumper.Git.PoSh](../FrenchExDev.OPumper.Git.PoSh/README.md) | Status, diff statistics, targeted changes, limited history |
| [FrenchExDev.OPumper.Dotnet.PoSh](../FrenchExDev.OPumper.Dotnet.PoSh/README.md) | Build diagnostics and test reports |
| [FrenchExDev.OPumper.Pnpm.PoSh](../FrenchExDev.OPumper.Pnpm.PoSh/README.md) | Bounded dependency inventory |
| [FrenchExDev.OPumper.Podman.PoSh](../FrenchExDev.OPumper.Podman.PoSh/README.md) | Containers, images, targeted logs |
| [FrenchExDev.OPumper.Ripgrep.PoSh](../FrenchExDev.OPumper.Ripgrep.PoSh/README.md) | Structured search and file discovery |
| [FrenchExDev.OPumper.PowerShell.PoSh](../FrenchExDev.OPumper.PowerShell.PoSh/README.md) | Directory inventory, file windows, filtered logs |

These six modules retain all fifteen original pumps. Providers depend on the OutputPumps API and **do not require DevPoSh**. Ordinary provider imports define commands without registering routes. Registration is explicit through `Setup-OutputPumps`.

The [index](index.md) is the agent's documentation entry point. Read it, then the selected software sheet. Open implementation files only after selecting a pump.

## The result contract

Every recognized invocation returns one `PSCustomObject` with seven properties:

| Property | Meaning |
|---|---|
| `Summary` | One-line explanation |
| `Outcome` | `Success`, `Failed`, `Timeout` or `Unsupported` |
| `Data` | Provider-specific business object with a business `PSTypeName` |
| `ExitCode` | Native exit code, when available |
| `Error` | Sanitized diagnostic information |
| `RetryAdvice` | `SafeToRetry`, `NarrowRequest`, `CorrectRequest` or `Unknown` |
| `Truncation` | Explicit omissions, returned count, known total and next-request advice |

Defaults are **20 items, 8 KiB of compact UTF-8 JSON and 600 seconds**. Use `MaxItems`, `MaxOutputBytes` and `TimeoutSeconds` to reduce limits. Concurrent stdout/stderr capture and internal buffers are bounded. No retry runs automatically.

`Truncation.Applied` signals omitted information; it does not stop the process. A preview can be truncated while a certified aggregate remains exact. Ripgrep exposes `Complete`, `MatchLines` or `FileCount` for this distinction. When analysis is incomplete, exact totals are null and observed counts are labelled separately. Narrow the actual path, filter or time window to obtain omitted details. A lower `MaxItems` alone does not reduce native output. Do not retrieve a raw log to fill omissions.

`Data` is projected before final redaction and bounding:

```powershell
Pump-Me git status -Filter { param($data) $data.Branch -eq 'main' } `
    -Map { param($data) [pscustomobject]@{ Branch=$data.Branch; Files=$data.Items } }
```

`Filter` receives the provider's business object and must return one Boolean. Its preview may already be limited. False preserves the execution envelope and empties the data. `Map` projects the retained data; it cannot recover omitted records. Compute exact aggregates in the parser before preview limits. Neither block changes the original technical outcome; exceptions become structured failures.

## Streaming analysis and completion

Native calls are synchronous: they return after process exit and stdout/stderr EOF, or a structured execution failure. There is no `Wait` switch. Preview or JSON limits never terminate a process. Expiration is a separate condition that terminates the process tree and reports `Timeout` without retrying.

The SDK offers two modes. Existing `-Parse { param($capture) ... }` calls keep bounded capture for formats requiring a complete document. Streaming calls add `-State` and `-OnRecord`; the final `-Parse` receives `(state, capture)` after draining both streams. Git status, ripgrep search and file discovery use streaming, and the provider generator creates a streaming starter. Git status retains at most `MaxItems` paths while counting all changes, including NUL-framed rename pairs.

```powershell
$state = @{ Lines=0L; Warnings=0L }
Invoke-OutputPumpNative -Executable my-cli -ArgumentList @('--info') `
    -State $state -OnRecord {
        param($record, $state)
        if ($record.Stream -eq 'stdout' -and !$record.Truncated) {
            $state.Lines++
            if ($record.Text -match '\bWARN\b') { $state.Warnings++ }
        }
    } -Parse {
        param($state, $capture)
        @{ LineCount=$state.Lines; Warnings=$state.Warnings }
    }
```

Callbacks run on the caller's PowerShell thread. `OnRecord` updates state without emitting output. Keep state bounded with counters and a limited sample; do not append every record. The internal queue is bounded and waits for consumer capacity. Records default to 65,536 characters, configurable with `MaxRecordCharacters`; stdout uses `RecordDelimiter` (newline by default, NUL for file discovery), while stderr remains line-delimited. A giant record is drained with `Truncated=true` and no text. The command continues, but the final result reports `Failed / RecordLimit`, `AnalysisComplete=false` and an unknown total. A parser exception also drains the command before reporting failure. Raw records and captures are never agent-facing output.

After upgrading the capture runtime, open a new PowerShell process: an already loaded C# type cannot be replaced by reimporting the module. Rebuild Production artifacts explicitly before using the new API there.

## Debugging a pump

Run `Pump-Me git status -Debug` for opt-in diagnostics. Traces identify the selected route, engine/provider source, capture mode, limits, character counts and completion status. `Timing` entries report `action`, monotonic `elapsedMs`, `count` and `averageMs`. Actions cover registry lookup, routing, argument conversion, provider loading, invocation, filtering/mapping, bounding, capture initialization, native setup/start, each stream drain, final parsing, tasks and steps. Streaming callbacks and record waits use cumulative timers and counts, without a log per record. Totals include their nested actions; stdout/stderr drains overlap, so these durations must not be added together. Timings are also emitted when an action fails or times out.

Traces never print native arguments, environment entries, stdout, stderr, records or exception messages. Debug records stay on stream 5 and do not count as additional business envelopes or task errors. `-Debug:$false` suppresses them.

If `Outcome=Failed`, `Error.Code=CaptureLimit` and `ExitCode=0`, the native command succeeded but its buffered capture was incomplete. `Error.Diagnostics` gives observed and retained character counts without exposing output. Buffered stdout is limited to 4,194,304 characters and stderr to 262,144 characters. Lowering `MaxItems` does not reduce that native capture. Git status uses streaming to avoid this global capture limit; its bounded preview may still report `Success` with `Truncation.Reasons=ItemLimit` and an exact `TotalCount`.

With a configured profile that already provides `Pump-Me`, no additional import is required. Otherwise import `OutputPump.PoSh.psd1` and run `Setup-OutputPumps`. The historical `OutputPumps.psm1` entry point delegates to the same engine. After updating loaded provider functions, start a new PowerShell Core session so registry closures reference the updated commands.

## Child process environment

Native providers for Git, dotnet, pnpm, Podman and ripgrep accept `-Environment` through `Pump-Me`, their own cmdlets and `Invoke-OutputPumpNative`. Pass a dictionary of child-only overrides:

```powershell
Pump-Me git status -Environment @{
    LANG = 'C.UTF-8'
    GIT_TRACE = $null
} -Debug
```

The child inherits the parent environment. OutputPumps then sets `NO_COLOR=1`, `TERM=dumb` and `GIT_TERMINAL_PROMPT=0`, followed by your overrides. A string sets or replaces an entry; `$null` removes it, including a default; `''` keeps an empty value. The parent environment is unchanged. Invalid names or NUL values fail before process startup, and diagnostics omit the entries. A route that cannot apply overrides returns `Unsupported` rather than ignoring them.

Custom providers declare an optional `[Collections.IDictionary]$Environment` parameter and forward it to `Invoke-OutputPumpNative -Environment $Environment`. `New-OPumperModule` includes this forwarding in its starter. The C# `BoundedProcess.Run` and `StreamingProcess` constructor retain their original overloads; new overloads accept `IDictionary<string,string>` as the final argument.

## One budget for a whole task

Per-call limits do not bound a sequence of results. `ConvertTo-OutputPumpJson` still serializes each pipeline envelope separately. Use `Invoke-OutputPumpTask` to process intermediate objects locally and produce one final envelope, or `-AsJson` for one JSON document. **Wrap every locally consumed pump call in `Invoke-OutputPumpStep`** so its outcome is recorded even when assigned to a variable. Plain assigned calls cannot be inferred from the script's final output.

```powershell
$call = New-OutputPumpScript -TaskScriptBlock {
    param($repo)
    $search = Invoke-OutputPumpStep -Name search -ScriptBlock {
        Pump-Me rg -NativeArguments @('TODO', 'src') `
            -WorkingDirectory $repo -MaxItems 2
    }
    if ($search.Outcome -eq 'Success' -and $search.Data.Complete) {
        [pscustomobject]@{ MatchingLines=$search.Data.MatchLines }
    }
} -ArgumentList @($repo) -MaxOutputBytes 2048
& $call.Data.Items[0].ScriptPath
```

`Data.Items` holds the final projection. `Data.Status` counts all steps, failures, timeouts, unsupported results and truncated steps. `Data.Issues` retains a bounded set of diagnostics, prioritizing failures over successful previews; `OmittedIssues` signals dropped details. Technical counters and aggregate outcome survive final projection trimming. Intermediate envelopes emitted by the body are recorded rather than dumped; explicitly project the values you need. Diagnostic streams and script exceptions become task failures. Nested tasks have separate accounting; wrap a locally consumed nested task in a step to include its outcome in the parent.

The generated task caller initializes the registry when necessary and emits one bounded JSON result. Pass external values through `ArgumentList`; script text does not transport captured closures into another process. No retry or native relaunch is automatic.

## Dispatch and arguments

`pump` is an alias for `Pump-Me`. Registered routes match the executable and leading argument tokens. A recognized route with unsupported options returns `Unsupported` without executing the command.

After successful setup, a command without a registered route uses native passthrough. It preserves raw streams and `$LASTEXITCODE` without exiting the caller. **Agents must verify coverage first and must not use this fallback for noisy commands.** Uninitialized or failed setup never falls through.

Arguments are passed as argument arrays, without constructing a shell command. Use `NativeArguments` when a native option conflicts with a wrapper parameter:

```powershell
Pump-Me git -NativeArguments @('status', '--', 'src/path with spaces')
```

## Session registry and live closures

One registry is shared by the engine's commands in a PowerShell session. Registrations are not serialized into the inventory. Repeated unchanged setup reuses loaded modules and preserves live overrides. Failures roll back registrations staged by that setup.

Add a native route in memory:

```powershell
$label = 'local experiment'
$handler = {
    param($call)
    New-OutputPumpResult -Summary $label -Data @{ Value=42 }
}.GetNewClosure()

Register-OutputPump -Id example.inspect -Route @{
    Executable='example'; Tokens=@('inspect'); Options=@{}
    Positional=$null; Multiple=$false
} -Handler $handler

Pump-Me example inspect
Unregister-OutputPump -Id example.inspect
```

Collisions are refused by default. `-Replace` replaces an existing registration with the same identity and route. `Unregister-OutputPump` removes the top registration and restores its predecessor. `Get-OutputPump` exposes metadata, not callbacks or captured variables.

Explicit object transformations compose through the pipeline:

```powershell
Register-OutputPump -Id example.branch -TestInput {
    param($inputObject)
    $null -ne $inputObject.Data -and $null -ne $inputObject.Data.Branch
} -Transform {
    param($inputObject)
    New-OutputPumpResult -Summary 'Branch selected.' -Data @{
        Branch=$inputObject.Data.Branch
    }
}

Pump-Me git status | Pump-Me -PumpId example.branch
```

An unrecognized input passes through unchanged. A recognized input is transformed once. There is no automatic recognition chain. Live closures last only for the current process; move reusable behavior into a provider to restore it in future shells.

## Authoring a provider

Suppose your project's CLI emits pages of INFO events. Write a pump that captures those events and extracts useful counters, diagnostics or domain entities into `.Data`. The agent then generates a PowerShell script that works with that object instead of parsing terminal text.

```powershell
New-OPumperModule -Name MyCli -Executable my-cli -Path ./FrenchExDev.OPumper.MyCli.PoSh
```

The generator supports `-WhatIf` and refuses a nonempty destination. It creates a manifest, loader, English README, catalogue, fixture and tests:

```text
FrenchExDev.OPumper.MyCli.PoSh/
  MyCli-OPumper.psd1
  MyCli-OPumper.psm1
  catalogue.json
  src/
    Public/  Private/  Classes/  Completers/  Variables/
    Setup-OutputPumps.ps1
  test/
    Unit/  Integration/  Fixtures/
  resources/
```

The manifest declares the contribution contract:

```powershell
PrivateData = @{
    OutputPumps = @{
        Version=1
        ProviderId='mycli'
        RegistrationScript='src/Setup-OutputPumps.ps1'
    }
}
```

Another explicitly declared location below `src` is supported. Duplicate provider identities or ambiguous loaded instances fail before registration.

The contribution script accepts exactly one mandatory `PSCustomObject` context. Its fields are `ProviderId`, `ProviderRoot`, `ManifestPath`, `RegistrationPath`, `Module`, `Options` and `Register`. `Options` comes from YAML and contains data. The script reuses `Module`; when absent, it imports its own manifest without `Force`. Registration executes inside the provider module, and closures explicitly capture command references so private helper functions remain available. Registration must not run native commands.

Use the public authoring API:

- `Invoke-OutputPumpNative -Executable ... -ArgumentList ... -Parse { param($capture) ... }` captures bounded streams and parses them internally. Return useful data, never the capture object.
- `New-OutputPumpResult -Summary ... -Data ...` constructs a sanitized, bounded envelope.
- `Register-OutputPump -Id ... -Route ... -Handler ...` registers a native callback receiving an invocation context with resolved `Parameters`, directory and limits.
- `Register-OutputPump -Id ... -TestInput ... -Transform ...` registers an explicit object transformation.

The migrated providers also use `Get-OutputPumpRuntime`, a versioned compatibility adapter for their existing internal parsers. New providers should prefer the minimal authoring API above.

## Inventory and DevPoSh

```powershell
Get-ConfiguredOutputPumps -Path ./alternate.yaml
New-OutputPumpCompilation -ConfigurationPath ./alternate.yaml
Setup-OutputPumps -ConfigurationPath ./alternate.yaml
```

`Get-ConfiguredOutputPumps` validates YAML without executing contributions. `New-OutputPumpCompilation` produces `$HOME/.pwsh-pumps/registry.json`: a versioned loading inventory with identities, paths and metadata. It excludes dependency/cache/VCS folders and directory links. Updates are atomic under a lock; identical content is not rewritten. Additions, removals and moves are detected during setup.

**DevPoSh is optional.** Its profile loads modules normally, then calls `Setup-OutputPumps`. The setup reuses matching module instances by provider identity, GUID and origin. Disabling a provider in YAML prevents registration but does not unload a module DevPoSh already imported.

DevPoSh `Compile` and `CompileAll` compile code separately under `.posh.posh.compilations`. Provider artifacts retain their identity, matching registration script, catalogue and resources. Setup uses the embedded script of a loaded compiled provider. OutputPumps inventory compilation does not depend on DevPoSh and does not generate a second code artifact.

Copy the updated DevPoSh profile explicitly:

```powershell
Set-DevPoShPowershellProfile -Path $PROFILE.CurrentUserAllHosts
# Existing different file: review it, then use -Force to save a backup and replace it.
```

The maintained template is `FrenchExDev.DevPoSh.PoSh/resources/profile.ps1.tpl`. Updating it never modifies an existing user profile. Recopy it when you want its new behavior.

## Agent scripts and catalogue maintenance

```powershell
$call = New-OutputPumpScript -SoftwareId git -Function Get-PumpGitStatus `
    -Parameters @{ WorkingDirectory=$repo; MaxItems=10 }
& $call.Data.Items[0].ScriptPath
```

The generated caller emits compact JSON and deletes itself. In the current process it reuses the live registry. A fresh process initializes the required provider from configuration; in-memory-only closures cannot be transported to another process.

`Register-OutputPump -Descriptor $descriptor -LibraryRoot $providerRoot` retains the documentary registration parameter set. It validates exports, parameters and examples, and updates catalogue, software sheet and index under a lock. This is separate from live registration, which writes no files. Run `Test-OutputPumpCatalogue -LibraryRoot ...` to check coherence.

Run `./Initialize-Catalogue.ps1` to refresh the engine's aggregate catalogue and software sheets from the six independent provider catalogues. Those provider catalogues are the source of truth. Aggregate module paths point directly to sibling `FrenchExDev.OPumper.<Name>.PoSh/<Name>-OPumper.psm1` loaders. Validation requires a matching provider manifest and rejects linked providers or arbitrary parent paths.

## Tests

Development dependencies are explicit and locally cached: **Pester 6.2.0**, `powershell-yaml` 0.4.12. Experimental Pester parallelism is disabled.

```powershell
./src/Scripts/Install-TestDependencies.ps1
./test/Invoke-Tests.ps1 -Suite Unit
./test/Invoke-Tests.ps1 -Suite Unit -Module FrenchExDev.OPumper.Git.PoSh
./test/Invoke-Tests.ps1 -Suite Unit -Cmdlet Setup-OutputPumps
./test/Invoke-Tests.ps1 -Suite Unit -Coverage
./test/Invoke-Tests.ps1 -Suite Integration
```

Unit tests use mocks and fixtures without requiring native tools or writing the real user profile. Integration tests require Git, ripgrep, a .NET SDK and PowerShell, and exercise real processes, huge concurrent streams, timeouts, generated callers, profiles and compiled resources. Test summaries are bounded JSON. CI separates unit and integration runs and publishes XML results, integration JSON and PowerShell coverage.

## Migration

- Replace `$HOME/.pwsh-pumps/pumps.yaml` with `$HOME/.pumps-config.yaml`, change `version` to `2`, rename `sources` to `roots`, and express provider switches under `providers` and route switches under `pumps`.
- The old monolithic `$HOME/.pwsh-pumps/OutputPump.psm1` is no longer generated or loaded by setup. Import the engine and call setup; `registry.json` is metadata, not executable code.
- CLI implementations live in independent `FrenchExDev.OPumper.*.PoSh` directories. Import their `<Name>-OPumper.psd1` manifests directly; the six engine compatibility loaders have been removed. The old `OutputPumps.psd1` entry point delegates to the main engine.
- Provider directories formerly named `<Name>-OPumper` are now `FrenchExDev.OPumper.<Name>.PoSh`, for example `Git-OPumper` → `FrenchExDev.OPumper.Git.PoSh`. Update YAML roots and scripts that point directly to a renamed directory, then run `Setup-OutputPumps` to refresh the inventory. Roots pointing to the parent directory need no change. Rebuild DevPoSh compilations explicitly when using Production artifacts.
- Keep all providers you use in configured roots, then reopen your shell. Recopy a DevPoSh profile explicitly if you use its template.
- Profile-copy generation parameters were replaced by mandatory `Set-DevPoShPowershellProfile -Path <file>`. No implicit destination or automatic profile installation remains.
