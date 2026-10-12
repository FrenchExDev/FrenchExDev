#requires -Version 7.4
#requires -PSEdition Core
$script:DevPoShProjectRoot=$PSScriptRoot
foreach($category in @('Classes','Private','Public','Completers','Variables')) {
    $folder=Join-Path $PSScriptRoot "src/$category"
    if(Test-Path -LiteralPath $folder){Get-ChildItem -LiteralPath $folder -Filter '*.ps1' -File -Recurse | Sort-Object FullName | ForEach-Object {. $_.FullName}}
}
$public=@(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src/Public') -Filter '*.ps1' -File -Recurse | ForEach-Object BaseName)
Export-ModuleMember -Function $public
