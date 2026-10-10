function New-OutputPumpResourceSnapshot {
    param([string]$Root,[string]$ModuleText,$Compiler)
    $resources=[ordered]@{}
    $folder=Join-Path $Root 'resources'
    if(Test-Path -LiteralPath $folder){
        foreach($file in Get-ChildItem -LiteralPath $folder -File -Recurse | Sort-Object FullName){
            if($file.LinkType){throw 'Linked resources are not supported.'}
            $resources[[IO.Path]::GetRelativePath($Root,$file.FullName)]=[IO.File]::ReadAllBytes($file.FullName)
        }
    }
    $hash=& $Compiler {param($code,$assets) Get-DevPoShFingerprint $code $assets} $ModuleText $resources
    $prefix=".assets/$hash"
    $assets=[ordered]@{}
    foreach($name in $resources.Keys){$assets["$prefix/$name"]=$resources[$name]}
    [pscustomobject]@{Prefix=$prefix;Assets=$assets}
}
