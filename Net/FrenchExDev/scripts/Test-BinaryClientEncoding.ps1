#requires -Version 7.0
<#
.SYNOPSIS
Checks child-process UTF-8 decoding under legacy console code pages, without a container engine.
.PARAMETER Integration
Also checks a real, local dotnet build with no restore or project dependencies.
#>
[CmdletBinding()]
param([switch] $Integration)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'BinaryClients.psm1') -Force
$module = Get-Module BinaryClients
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$testRoot = Join-Path $workspace ('.fake/binary-clients-tests/encoding-' + [guid]::NewGuid().ToString('N'))
$null = New-Item -ItemType Directory -Path $testRoot -Force
$child = Join-Path $testRoot 'utf8 child.ps1'
$expected = 'La génération a réussi : été, Noël, € et 中文.'
$errorLine = 'échec : déjà reçu, durée 12 ms.'
[IO.File]::WriteAllText($child, @'
$utf8 = [Text.UTF8Encoding]::new($false)
foreach ($item in @(
    @{ Stream = [Console]::OpenStandardOutput(); Text = "La génération a réussi : été, Noël, € et 中文." },
    @{ Stream = [Console]::OpenStandardError(); Text = "échec : déjà reçu, durée 12 ms." }
)) {
    # Split UTF-8 sequences across writes to exercise the asynchronous stream decoder.
    foreach ($value in $utf8.GetBytes($item.Text + [Environment]::NewLine)) {
        $item.Stream.WriteByte($value)
        $item.Stream.Flush()
    }
}
exit 7
'@, [Text.UTF8Encoding]::new($false))

$script:checks = 0
function Assert-True([bool] $Condition, [string] $Message) {
    if (-not $Condition) { throw $Message }
    $script:checks++
}
function Run-Probe([string] $File, [string[]] $Arguments, [string] $Stage, [string] $Log) {
    & $module {
        param($File, $Arguments, $Stage, $Log, $testRoot)
        $job = Start-BinaryProcess $File $Arguments $testRoot Utf8 $Stage $Log
        Wait-BinaryProcess @($job) 6>&1
    } $File $Arguments $Stage $Log $testRoot
}
function Get-Messages($Records) {
    @($Records | Where-Object { $_ -is [Management.Automation.InformationRecord] } |
        ForEach-Object { $_.MessageData.ToString() })
}
$pwsh = (Get-Command pwsh -CommandType Application | Select-Object -First 1).Source
$savedInput = [Console]::InputEncoding
$savedOutput = [Console]::OutputEncoding
$savedForceUtf8 = [Environment]::GetEnvironmentVariable('DOTNET_CLI_FORCE_UTF8_ENCODING')
try {
    foreach ($codePage in 850, 1252) {
        [Console]::InputEncoding = [Text.Encoding]::GetEncoding($codePage)
        [Console]::OutputEncoding = [Text.Encoding]::GetEncoding($codePage)
        $log = Join-Path $testRoot "stdout-stderr-$codePage.log"
        $records = @(Run-Probe $pwsh @('-NoProfile','-File',$child) "streams-$codePage" $log)
        $lines = [IO.File]::ReadAllLines($log, [Text.UTF8Encoding]::new($false, $true))
        $messages = @(Get-Messages $records)
        $result = @($records | Where-Object { $_ -isnot [Management.Automation.InformationRecord] })
        Assert-True ($expected -cin $lines) "stdout corrupted with console CP$codePage."
        Assert-True ($errorLine -cin $lines) "stderr corrupted with console CP$codePage."
        Assert-True ("[Utf8/streams-$codePage] $expected" -cin $messages) "Console stdout corrupted with CP$codePage."
        Assert-True ("[Utf8/streams-$codePage] $errorLine" -cin $messages) "Console stderr corrupted with CP$codePage."
        Assert-True ($result.Count -eq 1 -and $result[0].ExitCode -eq 7) 'Child exit code was lost.'
        Assert-True ([Console]::OutputEncoding.CodePage -eq $codePage) 'The launcher changed its caller console encoding.'

        if ($Integration) {
            $project = Join-Path $testRoot 'encoding.proj'
            [IO.File]::WriteAllText($project, @'
<Project DefaultTargets="Build">
  <Target Name="Build">
    <Message Text="La génération a réussi : été, Noël, € et 中文." Importance="high" />
  </Target>
</Project>
'@, [Text.UTF8Encoding]::new($false))
            $dotnet = (Get-Command dotnet -CommandType Application | Select-Object -First 1).Source
            $log = Join-Path $testRoot "dotnet-build-$codePage.log"
            $records = @(Run-Probe $dotnet @('build',$project,'--no-restore','--nologo','--verbosity','minimal') "build-$codePage" $log)
            $lines = @([IO.File]::ReadAllLines($log, [Text.UTF8Encoding]::new($false, $true)) | ForEach-Object { $_.Trim() })
            $result = @($records | Where-Object { $_ -isnot [Management.Automation.InformationRecord] })
            Assert-True ($expected -cin $lines) "Real dotnet build output corrupted with CP$codePage."
            Assert-True ($result.Count -eq 1 -and $result[0].ExitCode -eq 0) 'Local dotnet build failed.'
        }
    }
    Assert-True ([Environment]::GetEnvironmentVariable('DOTNET_CLI_FORCE_UTF8_ENCODING') -eq $savedForceUtf8) 'The launcher changed its caller environment.'
} finally {
    [Console]::InputEncoding = $savedInput
    [Console]::OutputEncoding = $savedOutput
}
Write-Host "PASS: $script:checks encoding assertions. Evidence: $testRoot"
