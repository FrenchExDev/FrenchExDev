#requires -Version 7.4
#requires -PSEdition Core
# Development entrypoint. Production imports an explicit DevPoSh compilation.
$script:DevPoShProjectRoot=$PSScriptRoot
$publicFunctions=@()
$variables=@()
foreach($category in @('Classes','Private','Public','Completers','Variables')) {
    $directory=Join-Path $PSScriptRoot "src/$category"
    if(Test-Path -LiteralPath $directory) {
        foreach($file in Get-ChildItem -LiteralPath $directory -Filter '*.ps1' -File -Recurse | Sort-Object FullName) {
            . $file.FullName
            if($category -eq 'Public'){$publicFunctions+=$file.BaseName}
            if($category -eq 'Variables'){$variables+=$file.BaseName}
        }
    }
}
Export-ModuleMember -Function $publicFunctions -Variable $variables
