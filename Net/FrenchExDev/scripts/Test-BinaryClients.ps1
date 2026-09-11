#requires -Version 7.0
<#
.SYNOPSIS
Offline regression tests: selection, batching, failures, cleanup, replay and real child-process IO.
#>
[CmdletBinding()]
param()
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'BinaryClients.psm1') -Force
$module = Get-Module BinaryClients
$root = Split-Path $PSScriptRoot -Parent
$workspace = [IO.Path]::GetFullPath((Join-Path $root '../..'))
$testRoot = Join-Path $workspace ('.fake/binary-clients-tests/powershell-' + [guid]::NewGuid().ToString('N'))
$null = New-Item -ItemType Directory -Path $testRoot -Force
$script:checks = 0
function Assert-True([bool] $Condition, [string] $Message) {
    if (-not $Condition) { throw $Message }
    $script:checks++
}
function Assert-Throws([scriptblock] $Action, [string] $Message) {
    $failed = $false
    try { & $Action | Out-Null } catch { $failed = $true }
    Assert-True $failed $Message
}
function Options([string] $Name) {
    @{
        Client = @('Docker','DockerCompose','Git'); ExcludeClient = @('Dotnet')
        ClientParallel = 2; Parallel = 4; ScrapeParallel = 12
        Framework = 'net10.0'; Configuration = 'Release'; Runtime = 'podman'
        MinVersion = @{ Docker = '29.8.0' }; AllVersions = $false; RetryKnownMissing = $false
        SkipTests = $false; KeepImages = $false
        OutputRoot = (Join-Path $testRoot 'output with spaces & accents é')
        LogRoot = (Join-Path $testRoot $Name)
    }
}
function Report([string] $Directory) {
    $path = @(Get-ChildItem -LiteralPath $Directory -Filter report.json -Recurse | Sort-Object FullName)[-1].FullName
    @{ Path = $path; Data = (Get-Content -LiteralPath $path -Raw | ConvertFrom-Json -AsHashtable) }
}

$available = @(Get-BinaryClient $root)
$selected = @(Select-BinaryClient $available @() @('Dotnet'))
Assert-True ($selected.Count -eq 8 -and 'Dotnet' -notin $selected.Name) 'Default selection must contain eight CLI clients without Dotnet.'
Assert-True (@(Select-BinaryClient $available @('Dotnet') @()).Name -eq 'Dotnet') 'Dotnet must be selectable explicitly.'
Assert-True (@(Select-BinaryClient $available @('git,DOCKER','Git') @()).Count -eq 2) 'Selection must support comma lists, case and deduplication.'
Assert-Throws { Select-BinaryClient $available @('Typo') @() } 'An unknown client must fail.'
Assert-Throws { Select-BinaryClient $available @('Dotnet') @('Dotnet') } 'Empty selection must fail.'
Assert-Throws { & (Join-Path $PSScriptRoot 'Invoke-BinaryClients.ps1') -ClientParallel 0 -List } 'Invalid concurrency must fail.'
$preview = Options 'preview'
Invoke-BinaryClients -Root $root -Options $preview -WhatIf
Assert-True (-not (Test-Path -LiteralPath $preview.LogRoot)) 'WhatIf must not create logs or launch commands.'

# Exercise every real Find-Missing wrapper with a command stub; do not build or access a runtime.
$fixtureState = @{ Forwarded = @(); NativeExit = 0 }
function dotnet {
    $fixtureState.Forwarded = @($args)
    $global:LASTEXITCODE = $fixtureState.NativeExit
}
try {
    foreach ($item in $available) {
        & $item.Script -NoBuild -FailFast -Missing -RetryKnownMissing -KeepImages -Configuration Release `
            -Framework net11.0 -Parallel 3 -ScrapeParallel 7 -StopFile 'stop path with spaces'
        foreach ($flag in '--no-build','--fail-fast','--missing','--retry-known-missing','--keep-images') {
            Assert-True ($flag -in $fixtureState.Forwarded) "$($item.Name) lost $flag."
        }
        Assert-True ($fixtureState.Forwarded[$fixtureState.Forwarded.IndexOf('--scrape-parallel') + 1] -eq '7') "$($item.Name) lost scrape concurrency."
        Assert-True ($fixtureState.Forwarded[$fixtureState.Forwarded.IndexOf('--stop-file') + 1] -eq 'stop path with spaces') "$($item.Name) split the stop path."
        Assert-True ($fixtureState.Forwarded[$fixtureState.Forwarded.IndexOf('--configuration') + 1] -eq 'Release') "$($item.Name) lost configuration."
        $fixtureState.NativeExit = 17
        Assert-Throws { & $item.Script -NoBuild -FailFast } "$($item.Name) swallowed a native failure."
        $fixtureState.NativeExit = 0
    }
} finally { Remove-Item Function:dotnet }

# Real subprocesses exercise both redirected streams, literal arguments and exit codes.
$child = Join-Path $testRoot 'child with spaces é.ps1'
[IO.File]::WriteAllText($child, @'
param([string] $Value, [int] $Code, [int] $Delay = 0, [string] $StopFile)
[Console]::Out.WriteLine($Value)
[Console]::Error.WriteLine('stderr fixture')
if ($StopFile) {
    $deadline = [DateTime]::UtcNow.AddSeconds(10)
    while (-not (Test-Path -LiteralPath $StopFile)) {
        if ([DateTime]::UtcNow -gt $deadline) { exit 99 }
        Start-Sleep -Milliseconds 20
    }
    [Console]::Out.WriteLine('observed stop')
}
Start-Sleep -Milliseconds $Delay
exit $Code
'@)
$literal = 'spaces é & apostrophe '' $() `backtick'
$real = & $module {
    param($testRoot, $child, $literal)
    $pwsh = (Get-Command pwsh -CommandType Application | Select-Object -First 1).Source
    $stop = Join-Path $testRoot 'real.stop'
    $a = Start-BinaryProcess $pwsh @('-NoProfile','-File',$child,'-Value',$literal,'-Code','7','-Delay','250') $testRoot A collect (Join-Path $testRoot 'A.log')
    $b = Start-BinaryProcess $pwsh @('-NoProfile','-File',$child,'-Value','sibling','-Code','0','-StopFile',$stop) $testRoot B collect (Join-Path $testRoot 'B.log')
    @(Wait-BinaryProcess @($a, $b) $stop)
} $testRoot $child $literal
Assert-True ($real.Count -eq 2 -and $real[0].ExitCode -eq 7 -and $real[1].ExitCode -eq 0) 'The process monitor must propagate errors and drain siblings.'
$lines = [IO.File]::ReadAllText((Join-Path $testRoot 'A.log'))
Assert-True ($lines.Contains($literal) -and $lines.Contains('stderr fixture')) 'Arguments and both streams must survive without shell interpolation.'
Assert-True ([IO.File]::ReadAllText((Join-Path $testRoot 'B.log')).Contains('observed stop')) 'A failed process must signal its sibling.'

# Keep simulated orchestration and its checkout lock separate from a live user run.
$fixtureRoot = Join-Path $testRoot 'checkout/Net/FrenchExDev'
foreach ($item in $available) {
    foreach ($relative in @(
        'scripts/Find-Missing.ps1',
        "src/FrenchExDev.Net.$($item.Name).Design/FrenchExDev.Net.$($item.Name).Design.csproj",
        "FrenchExDev.Net.$($item.Name).slnx"
    )) {
        $destination = Join-Path $fixtureRoot "$($item.Name)/$relative"
        $null = New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($destination)) -Force
        Copy-Item -LiteralPath (Join-Path $item.Directory $relative) -Destination $destination
    }
}
$root = $fixtureRoot

# Exercise orchestration with fake native commands. No engine or network is contacted.
& $module {
    $script:trace = [Collections.Generic.List[object]]::new()
    $script:fail = ''
    $script:startFailure = ''
    function script:Start-BinaryProcess($File, $Arguments, $Directory, $Name, $Stage, $Log) {
        if ("$Name/$Stage" -eq $script:startFailure) { throw 'fixture startup failure' }
        $job = [pscustomobject]@{ Name = $Name; Stage = $Stage; Log = $Log; Arguments = $Arguments }
        $script:trace.Add($job)
        return $job
    }
    function script:Wait-BinaryProcess([object[]] $Jobs, [string] $StopFile) {
        foreach ($job in $Jobs) {
            $exitCode = if ("$($job.Name)/$($job.Stage)" -eq $script:fail) { 9 } else { 0 }
            if ($exitCode) { Set-BinaryStop $StopFile }
            [pscustomobject]@{ Client = $job.Name; Stage = $job.Stage; ExitCode = $exitCode; Log = $job.Log }
        }
    }
    function script:Get-Command {
        param($Name, $CommandType, $ErrorAction)
        if ($Name -eq 'podman') { return [pscustomobject]@{ Source = 'fixture-podman' } }
        $parameters = @{ Name = $Name; ErrorAction = 'Stop' }
        if ($CommandType) { $parameters.CommandType = $CommandType }
        Microsoft.PowerShell.Core\Get-Command @parameters
    }
}
try {
    $goodOptions = Options 'success'
    Invoke-BinaryClients -Root $root -Options $goodOptions
    $trace = @(& $module { $script:trace.ToArray() })
    $good = Report $goodOptions.LogRoot
    Assert-True ($good.Data.Status -eq 'Succeeded') 'A complete run must succeed.'
    Assert-True (@($good.Data.Clients.Values | Where-Object { $_ -eq 'Succeeded' }).Count -eq 3) 'Every client must be validated and cleaned before success.'
    $names = @($trace | ForEach-Object { "$($_.Name)/$($_.Stage)" })
    Assert-True ($names.IndexOf('Docker/collect') + 1 -eq $names.IndexOf('DockerCompose/collect')) 'The pair must start together.'
    Assert-True ($names.IndexOf('DockerCompose/clean') -lt $names.IndexOf('Git/pre-build')) 'The next batch must wait for both cleanups.'
    Assert-True ($names.IndexOf('Docker/post-test') -lt $names.IndexOf('Docker/clean')) 'Newly generated source must be tested before cleanup.'
    $collect = @($trace | Where-Object Stage -eq collect)[0].Arguments
    foreach ($flag in '-Missing','-FailFast','-NoBuild','-StopFile','-Parallel','-ScrapeParallel','-MinVersion','-Output') {
        Assert-True ($flag -in $collect) "Missing collection argument $flag."
    }
    Assert-True ($collect[$collect.IndexOf('-ScrapeParallel') + 1] -eq '12') 'Scrape parallelism must be forwarded.'
    Assert-True ($collect[$collect.IndexOf('-Configuration') + 1] -eq 'Release') 'Configuration must be forwarded.'
    $clean = @($trace | Where-Object Stage -eq clean)[0].Arguments
    Assert-True ('-CleanImages' -in $clean -and '-Missing' -notin $clean -and '-StopFile' -notin $clean) 'Cleanup must use a separate operation without the cancelled stop file.'

    foreach ($failure in 'Docker/pre-build','Docker/pre-test','Docker/collect','Docker/post-build','Docker/post-test','Docker/clean') {
        & $module { param($failure) $script:trace.Clear(); $script:fail = $failure } $failure
        $options = Options ($failure.Replace('/','-'))
        Assert-Throws { Invoke-BinaryClients -Root $root -Options $options } "Failure $failure was swallowed."
        $failed = Report $options.LogRoot
        $trace = @(& $module { $script:trace.ToArray() })
        Assert-True ($failed.Data.Status -eq 'Failed') 'A failure must be recorded.'
        Assert-True (@($trace | Where-Object Name -eq Git).Count -eq 0) 'No subsequent batch may start after an error.'
        if ($failure -like '*/pre-*') {
            Assert-True (@($trace | Where-Object Stage -eq collect).Count -eq 0) 'Build/test failure must prevent collection.'
        } else {
            Assert-True (@($trace | Where-Object Stage -eq clean).Count -eq 2) 'Both cleanups must be attempted even after a failure.'
        }
    }

    # Failure in batch 2: replay only that incomplete batch, with fresh logs and stop file.
    & $module { $script:trace.Clear(); $script:fail = 'Git/collect' }
    $options = Options 'replay-failed'
    Assert-Throws { Invoke-BinaryClients -Root $root -Options $options } 'Replay fixture must fail on batch 2.'
    $failed = Report $options.LogRoot
    & $module { $script:trace.Clear(); $script:fail = '' }
    $resumeOptions = Options 'replay-success'
    Invoke-BinaryClients -Root $root -Options $resumeOptions -Resume $failed.Path -ExplicitOptions @('LogRoot')
    $trace = @(& $module { $script:trace.ToArray() })
    Assert-True (@($trace | Where-Object Stage -eq collect).Name -eq 'Git') 'Replay must skip completed batches.'
    Assert-True ((Report $resumeOptions.LogRoot).Data.Status -eq 'Succeeded') 'Replay must succeed after fixing the fixture.'
    $changedOptions = Options 'invalid-replay'
    $changedOptions.OutputRoot = Join-Path $testRoot 'different-output'
    Assert-Throws { Invoke-BinaryClients -Root $root -Options $changedOptions -Resume $failed.Path -ExplicitOptions @('OutputRoot') } 'Replay must not reuse success for a different output corpus.'

    & $module { $script:trace.Clear(); $script:startFailure = 'DockerCompose/collect' }
    $options = Options 'startup-failure'
    Assert-Throws { Invoke-BinaryClients -Root $root -Options $options } 'A worker startup failure must fail the batch.'
    $trace = @(& $module { $script:trace.ToArray() })
    Assert-True (@($trace | Where-Object Stage -eq clean).Count -eq 2) 'A worker startup failure must still drain and clean the batch.'
    & $module { $script:trace.Clear(); $script:startFailure = '' }
    $options = Options 'single'
    $options.ClientParallel = 1
    $options.SkipTests = $true
    $options.KeepImages = $true
    $options.AllVersions = $true
    $options.RetryKnownMissing = $true
    Invoke-BinaryClients -Root $root -Options $options
    $trace = @(& $module { $script:trace.ToArray() })
    Assert-True (@($trace | Where-Object Stage -like '*test').Count -eq 0) 'SkipTests must be honored.'
    Assert-True (@($trace | Where-Object Stage -eq clean).Count -eq 0) 'KeepImages must be honored.'
    $arguments = @($trace | Where-Object Stage -eq collect)[0].Arguments
    Assert-True ('-Missing' -notin $arguments -and '-RetryKnownMissing' -in $arguments) 'Collection options must be forwarded.'

    # Simulate another launcher holding the checkout lock.
    $lockPath = [IO.Path]::GetFullPath((Join-Path $root '../../.fake/binary-clients/run.lock'))
    $lock = [IO.File]::Open($lockPath, 'OpenOrCreate', 'ReadWrite', 'None')
    try { Assert-Throws { Invoke-BinaryClients -Root $root -Options (Options 'locked') } 'Concurrent launchers must be rejected.' }
    finally { $lock.Dispose() }
} finally {
    Import-Module (Join-Path $PSScriptRoot 'BinaryClients.psm1') -Force
}
& (Join-Path $PSScriptRoot 'Test-BinaryClientEncoding.ps1')
Write-Host "PASS: $script:checks assertions. Evidence: $testRoot"
