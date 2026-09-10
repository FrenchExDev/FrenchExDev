#requires -Version 7.0
[CmdletBinding()]
param(
    [switch] $Missing,
    [switch] $NoBuild,
    [switch] $FailFast,
    [string] $StopFile,
    [switch] $RetryKnownMissing,
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Debug',
    [switch] $List,
    [switch] $Reparse,
    [switch] $BuildBase,
    [switch] $BuildImages,
    [switch] $CleanImages,
    [switch] $KeepImages,
    [string] $MinVersion,
    [ValidateRange(1, 64)]
    [int] $Parallel = 4,
    [ValidateRange(1, 64)]
    [int] $ScrapeParallel = 4,
    [ValidateSet('podman', 'docker')]
    [string] $Runtime = 'podman',
    [string] $Output,
    [switch] $Dashboard,
    [string] $AddKnownMissing,
    [string] $RemoveKnownMissing,
    [switch] $ListKnownMissing,
    [ValidateSet('net10.0', 'net11.0')]
    [string] $Framework = 'net10.0'
)
$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot '../src/FrenchExDev.Net.GitLab.Cli.Design/FrenchExDev.Net.GitLab.Cli.Design.csproj'
$designArgs = @('--parallel', "$Parallel", '--scrape-parallel', "$ScrapeParallel", '--runtime', $Runtime)
if ($Missing) { $designArgs += '--missing' }
if ($FailFast) { $designArgs += '--fail-fast' }
if ($StopFile) { $designArgs += @('--stop-file', $StopFile) }
if ($RetryKnownMissing) { $designArgs += '--retry-known-missing' }
if ($List) { $designArgs += '--list' }
if ($Reparse) { $designArgs += '--reparse' }
if ($BuildBase) { $designArgs += '--build-base' }
if ($BuildImages) { $designArgs += '--build-images' }
if ($CleanImages) { $designArgs += '--clean-images' }
if ($KeepImages) { $designArgs += '--keep-images' }
if ($MinVersion) { $designArgs += @('--min-version', $MinVersion) }
if ($Output) { $designArgs += @('--output', $Output) }
if ($Dashboard) { $designArgs += '--dashboard' }
if ($AddKnownMissing) { $designArgs += @('--add-known-missing', $AddKnownMissing) }
if ($RemoveKnownMissing) { $designArgs += @('--remove-known-missing', $RemoveKnownMissing) }
if ($ListKnownMissing) { $designArgs += '--list-known-missing' }
$runArgs = @('run', '--project', $project, '--framework', $Framework, '--configuration', $Configuration)
if ($NoBuild) { $runArgs += '--no-build' }
& dotnet @runArgs -- @designArgs
if ($LASTEXITCODE -ne 0) { throw "Design failed (exit code $LASTEXITCODE)." }
