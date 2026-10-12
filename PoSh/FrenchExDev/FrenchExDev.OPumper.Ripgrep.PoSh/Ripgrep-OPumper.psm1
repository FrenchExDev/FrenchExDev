#requires -Version 7.4
#requires -PSEdition Core
Set-StrictMode -Version Latest
$exports=@()
foreach($category in @('Classes','Private','Public','Completers','Variables')) {
    $folder=Join-Path $PSScriptRoot "src/$category"
    if(!(Test-Path -LiteralPath $folder -PathType Container)){continue}
    foreach($file in Get-ChildItem -LiteralPath $folder -Filter '*.ps1' -File -Recurse | Sort-Object FullName) {
        . $file.FullName
        if($category -eq 'Public'){$exports+=$file.BaseName}
    }
}
Export-ModuleMember -Function $exports