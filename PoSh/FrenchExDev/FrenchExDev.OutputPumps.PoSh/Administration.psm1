#requires -Version 7.4
#requires -PSEdition Core
Set-StrictMode -Version Latest
$script:PumpLibraryRoot = $PSScriptRoot
$script:PumpCaptureSource = $null
Import-Module (Join-Path $script:PumpLibraryRoot 'Core.psm1') -ErrorAction Stop
foreach ($visibility in @('Private','Public')) {
    $folder = Join-Path $script:PumpLibraryRoot ('src/{0}/{1}' -f $visibility,'Administration')
    if (Test-Path -LiteralPath $folder) { Get-ChildItem -LiteralPath $folder -Filter '*.ps1' -File | Sort-Object Name | ForEach-Object { . $_.FullName } }
}
Export-ModuleMember -Function 'Register-OutputPump','Test-OutputPumpCatalogue','New-OutputPumpScript'
