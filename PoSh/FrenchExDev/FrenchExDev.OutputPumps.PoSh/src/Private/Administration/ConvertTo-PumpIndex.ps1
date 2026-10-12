<#
.SYNOPSIS
Render the compact catalogue entry point.
.DESCRIPTION
Define ConvertTo-PumpIndex for the OutputPumps runtime. Summarize available software and the bounded-output workflow without requiring source inspection.
#>


function ConvertTo-PumpIndex {
    <#
    .SYNOPSIS
    Render the compact catalogue entry point.
    .DESCRIPTION
    Summarize available software and the bounded-output workflow without requiring source inspection.
    #>
    
    param([object[]]$Descriptors)
    $lines=@(
        '# OutputPumps — index','',
        'PowerShell **7.4+ Core only**. Read this index, then the selected software sheet (index + 1). CLI implementations live in independent `FrenchExDev.OPumper.*.PoSh` modules. Open code only after selecting a pump.','',
        'Before a noisy command: verified documentation → index → software sheet → pump → generated caller → compact JSON only. Verify coverage before `Pump-Me`: unknown routes use raw native passthrough. Unsupported options on a recognized route return `Unsupported` without execution.','',
        '| Software | Useful for | Software sheet | Example |','|---|---|---|---|'
    )
    foreach($descriptor in $Descriptors | Sort-Object Id){
        $lines+="| $($descriptor.Name) | $($descriptor.Purpose) | [$($descriptor.Id)](software/$($descriptor.Id).md) | ``$($descriptor.Commands[0].Example)`` |"
    }
    $lines+=@(
        '', '## Activation and agent callers','',
        'Import `OutputPump.PoSh.psd1` and run `Setup-OutputPumps`. User configuration is `$HOME/.pumps-config.yaml` (version 2: roots, providers, pumps). Providers already loaded by DevPoSh are reused; DevPoSh is optional. Missing or invalid configuration prevents passthrough.','',
        '```powershell',
        "Import-Module (Join-Path `$library 'OutputPump.PoSh.psd1') -DisableNameChecking",
        'Setup-OutputPumps | Out-Null',
        '$call = New-OutputPumpScript -SoftwareId git -Function Get-PumpGitStatus -Parameters @{ WorkingDirectory=$repo; MaxItems=10 }',
        '& $call.Data.Items[0].ScriptPath','```','',
        'The temporary caller emits compact JSON and deletes itself. It reuses the session registry or initializes the selected provider in a fresh process. In-memory closures do not cross processes. Never display internal captures, stdout, stderr or raw logs.','',
        'For multiple calls, use `New-OutputPumpScript -TaskScriptBlock { ... } -ArgumentList @(...)`. Wrap locally consumed calls in `Invoke-OutputPumpStep` inside `Invoke-OutputPumpTask`; emit one final projection. The task retains failures and truncation under one JSON budget. Per-call serialization does not impose a cumulative budget.','',
        '## Contract','',
        '`Summary`, `Outcome`, `Data`, `ExitCode`, `Error`, `RetryAdvice`, `Truncation`. Business properties belong in `Data`. Defaults: 20 items, 8 KiB compact UTF-8 JSON, 600 seconds. Success, failure, timeout and unsupported functionality are distinct.','',
        '`Truncation.Applied` requires a narrower path, filter or window. Null `TotalCount` means unknown. No retry is automatic. Filter and Map run after parsing and before final redaction and bounding.','',
        'Native calls wait for completion and stream EOF. Preview truncation never stops a process. `Invoke-OutputPumpNative -State ... -OnRecord ... -Parse ...` reduces records progressively with bounded buffering. Ripgrep counters remain exact when `Data.Complete` is true; oversized records or failed analysis signal unknown totals.','',
        '## Contributions and maintenance','',
        'A provider declares `PrivateData.OutputPumps` in its manifest and a `src/Setup-OutputPumps.ps1` contribution script. Register reusable native routes or explicit transformations with closures. Live registration writes no files; `-Replace` adds an override, and unregistering restores its predecessor.','',
        '`Register-OutputPump -Descriptor $descriptor -LibraryRoot $providerRoot` validates documentation, exports, parameters and examples, updating catalogue, sheet and index under a lock. Identical registration does not rewrite files. Task-specific scripts stay outside catalogues.','',
        'Validation: `Test-OutputPumpCatalogue`. Tests: `test/Invoke-Tests.ps1 -Suite Unit` or `-Suite Integration`. `New-OutputPumpCompilation` writes a JSON loading inventory under `$HOME/.pwsh-pumps/registry.json`; it does not compile provider code.','',
        'See [README](README.md) for profiles, configuration, authoring and migration.'
    )
    $lines -join "`n"
}
