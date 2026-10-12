<#
.SYNOPSIS
Compatibility loader for Core.
.DESCRIPTION
Delegate to the maintained engine or independent provider while preserving the historical import entry point.
#>


#requires -Version 7.4
#requires -PSEdition Core
Set-StrictMode -Version Latest
$script:PumpLibraryRoot = $PSScriptRoot
$script:PumpCaptureSource = $null
foreach ($visibility in @('Private','Public')) {
    $folder = Join-Path $script:PumpLibraryRoot ('src/{0}/{1}' -f $visibility,'Core')
    if (Test-Path -LiteralPath $folder) { Get-ChildItem -LiteralPath $folder -Filter '*.ps1' -File | Sort-Object Name | ForEach-Object { . $_.FullName } }
}
Export-ModuleMember -Function 'New-PumpResult','Complete-PumpProcess','Get-PumpHelp','Initialize-PumpCapture','Invoke-PumpProcess'
