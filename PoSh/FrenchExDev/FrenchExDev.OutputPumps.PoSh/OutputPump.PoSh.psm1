<#
.SYNOPSIS
Load the OutputPumps engine and export its public commands.
.DESCRIPTION
Load source categories in dependency order, initialize shared state and leave contribution activation to Setup-OutputPumps.
#>


#requires -Version 7.4
#requires -PSEdition Core
Set-StrictMode -Version Latest
foreach($folder in @('src/Private/Core','src/Public/Core','src/Private/Administration','src/Public/Administration','src/Private/OutputPump','src/Public/OutputPump')) {
    foreach($file in Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot $folder) -Filter '*.ps1' -File | Sort-Object Name){. $file.FullName}
}
. (Join-Path $PSScriptRoot 'src/Variables/SoftwareProxies.ps1')
. (Join-Path $PSScriptRoot 'src/Variables/Initialize-OutputPumps.ps1')
$exports=@('Pump-Me','Get-ConfiguredOutputPumps','New-OutputPumpCompilation','Setup-OutputPumps','Get-OutputPump','Register-OutputPump','Unregister-OutputPump','New-OPumperModule','Get-OutputPumpRuntime','Invoke-OutputPumpNative','Invoke-OutputPumpTask','Invoke-OutputPumpStep','New-OutputPumpResult','ConvertTo-OutputPumpJson','New-OutputPumpScript','Test-OutputPumpCatalogue','Get-PumpHelp')+$script:PumpProxyNames
Export-ModuleMember -Function $exports -Alias pump
