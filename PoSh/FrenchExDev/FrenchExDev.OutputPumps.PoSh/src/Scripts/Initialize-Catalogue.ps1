<#
.SYNOPSIS
Refresh the aggregate catalogue from the independent providers.
.DESCRIPTION
Read the maintained provider descriptors, validate their manifests and exports, and regenerate the engine's catalogue, index and software sheets.
#>

#requires -Version 7.4
#requires -PSEdition Core
[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$library=Split-Path (Split-Path $PSScriptRoot)
Import-Module (Join-Path $library 'OutputPump.PoSh.psd1') -DisableNameChecking -ErrorAction Stop
$failed=[Collections.Generic.List[object]]::new()
foreach($name in @('Git','Ripgrep','PowerShell','Dotnet','Podman','Pnpm')){
    try{
        $providerDirectory="FrenchExDev.OPumper.$name.PoSh"
        $providerRoot=Join-Path (Split-Path $library) $providerDirectory
        $descriptors=@(ConvertFrom-Json -InputObject ([IO.File]::ReadAllText((Join-Path $providerRoot 'catalogue.json'))) -AsHashtable -ErrorAction Stop)
        if($descriptors.Count -ne 1 -or $descriptors[0].Id -cne $name.ToLowerInvariant()){throw 'Expected one matching provider descriptor'}
        $descriptor=$descriptors[0]
        $descriptor.Module="../$providerDirectory/$($descriptor.Module)"
        $result=Register-OutputPump -Descriptor $descriptor -LibraryRoot $library
        if($result.Outcome -ne 'Success'){$failed.Add(@{Id=$descriptor.Id;Error=$result.Error})}
    }catch{$failed.Add(@{Id=$name.ToLowerInvariant();Message=$_.Exception.Message})}
}
if($failed.Count){
    New-OutputPumpResult -Summary 'Aggregate catalogue registration failed.' -Outcome Failed -Data @{Items=$failed.ToArray()} -RetryAdvice CorrectRequest
    exit 1
}
Test-OutputPumpCatalogue -LibraryRoot $library
