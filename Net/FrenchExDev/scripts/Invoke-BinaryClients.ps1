#requires -Version 7.0
<#
.SYNOPSIS
Build, collect, test and clean BinaryWrapper clients in bounded batches.
.EXAMPLE
./scripts/Invoke-BinaryClients.ps1
.EXAMPLE
./scripts/Invoke-BinaryClients.ps1 -ClientParallel 3 -ExcludeClient Dotnet,Git -Parallel 4 -ScrapeParallel 12
.EXAMPLE
./scripts/Invoke-BinaryClients.ps1 -Client Docker,Git -MinVersion @{ Docker = '29.8.0'; Git = '2.54.0' } -WhatIf
.EXAMPLE
./scripts/Invoke-BinaryClients.ps1 -Resume '../../.fake/binary-clients/<run>/report.json'
#>
[CmdletBinding(SupportsShouldProcess)]
param(
    [string[]] $Client = @(),
    [AllowEmptyCollection()]
    [string[]] $ExcludeClient = @('Dotnet'),
    [Alias('BatchSize')]
    [ValidateRange(1, 64)]
    [int] $ClientParallel = 2,
    [ValidateRange(1, 64)]
    [int] $Parallel = 4,
    [ValidateRange(1, 64)]
    [int] $ScrapeParallel = 12,
    [ValidateSet('net10.0', 'net11.0')]
    [string] $Framework = 'net10.0',
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Debug',
    [ValidateSet('podman', 'docker')]
    [string] $Runtime = 'podman',
    [hashtable] $MinVersion = @{},
    [switch] $AllVersions,
    [switch] $RetryKnownMissing,
    [switch] $SkipTests,
    [switch] $KeepImages,
    [switch] $List,
    [string] $OutputRoot,
    [string] $LogRoot,
    [string] $Resume
)

$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'BinaryClients.psm1') -Force
$options = @{
    Client = $Client; ExcludeClient = $ExcludeClient; ClientParallel = $ClientParallel
    Parallel = $Parallel; ScrapeParallel = $ScrapeParallel; Framework = $Framework
    Configuration = $Configuration; Runtime = $Runtime; MinVersion = $MinVersion
    AllVersions = [bool]$AllVersions; RetryKnownMissing = [bool]$RetryKnownMissing
    SkipTests = [bool]$SkipTests; KeepImages = [bool]$KeepImages
    OutputRoot = $OutputRoot; LogRoot = $LogRoot
}
$invokeParameters = @{
    Root = (Split-Path $PSScriptRoot -Parent); Options = $options
    ExplicitOptions = @($PSBoundParameters.Keys); Resume = $Resume
    List = $List; WhatIf = $WhatIfPreference
}
if ($PSBoundParameters.ContainsKey('Confirm')) { $invokeParameters.Confirm = $PSBoundParameters.Confirm }
Invoke-BinaryClients @invokeParameters
