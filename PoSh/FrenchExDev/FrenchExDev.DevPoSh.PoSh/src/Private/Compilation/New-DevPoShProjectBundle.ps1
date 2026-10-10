function New-DevPoShProjectBundle {
    param($Project,[string]$Destination,[hashtable]$DependencyPaths=@{})
    $root=$Project.Path
    $source=[Collections.Generic.List[object]]::new()
    foreach($category in @('Classes','Private','Public','Completers','Variables')) {
        $folder=Join-Path $root "src/$category"
        if(Test-Path -LiteralPath $folder) {
            foreach($file in Get-ChildItem -LiteralPath $folder -Filter '*.ps1' -File -Recurse | Sort-Object FullName) {
                $source.Add([ordered]@{Relative=[IO.Path]::GetRelativePath($root,$file.FullName);Code=[Convert]::ToBase64String([IO.File]::ReadAllBytes($file.FullName));Category=$category;Name=$file.BaseName})
            }
        }
    }
    if(!$source.Count){throw 'Project has no supported src scripts.'}
    $assets=[ordered]@{}
    $resourceDirectory=Join-Path $root 'resources'
    if(Test-Path -LiteralPath $resourceDirectory){foreach($file in Get-ChildItem -LiteralPath $resourceDirectory -File -Recurse | Sort-Object FullName){$assets[[IO.Path]::GetRelativePath($root,$file.FullName)]=[IO.File]::ReadAllBytes($file.FullName)}}
    foreach($name in @('LICENSE','.gitignore')){if(Test-Path -LiteralPath (Join-Path $root $name)){$assets[$name]=[IO.File]::ReadAllBytes((Join-Path $root $name))}}
    if(Test-Path -LiteralPath (Join-Path $root 'catalogue.json')){$assets['catalogue.json']=[IO.File]::ReadAllBytes((Join-Path $root 'catalogue.json'))}
    $providerMetadata=$null
    if($Project.Data.ContainsKey('PrivateData') -and $Project.Data.PrivateData.ContainsKey('OutputPumps')) {
        $providerMetadata=$Project.Data.PrivateData.OutputPumps
        $registration=$providerMetadata.RegistrationScript
        $registrationPath=[IO.Path]::GetFullPath((Join-Path $root $registration))
        if(!$registrationPath.StartsWith([IO.Path]::GetFullPath((Join-Path $root 'src'))+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Provider registration outside src.'}
        $assets[$registration]=[IO.File]::ReadAllBytes($registrationPath)
    }
    $captureDirectory=Join-Path $root 'src/Private/CSharp'
    if(Test-Path $captureDirectory){foreach($file in Get-ChildItem -LiteralPath $captureDirectory -File -Recurse){$assets[[IO.Path]::GetRelativePath($root,$file.FullName)]=[IO.File]::ReadAllBytes($file.FullName)}}
    $sourceJson=ConvertTo-Json -InputObject $source.ToArray() -Depth 5 -Compress
    $snapshotHash=Get-DevPoShFingerprint $sourceJson $assets
    $prefix=".assets/$snapshotHash"
    $packaged=[ordered]@{};foreach($name in $assets.Keys){$packaged["$prefix/$name"]=$assets[$name]}
    $encoded=[Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($sourceJson))
    $code=@"
#requires -Version 7.4
#requires -PSEdition Core
`$script:DevPoShProjectRoot=Join-Path `$PSScriptRoot '$prefix'
"@
    foreach($dependency in $(if($Project.Data.ContainsKey('RequiredModules')){$Project.Data.RequiredModules}else{@()})) {
        $name=if($dependency -is [Collections.IDictionary]){$dependency.ModuleName}else{[string]$dependency}
        if($DependencyPaths.ContainsKey($name)) {
            $dependencyPath=$DependencyPaths[$name].Replace("'","''")
            $code+="`nImport-Module '$dependencyPath' -DisableNameChecking -ErrorAction Stop`n"
        } elseif($loadedDependency=Get-Module -All -Name $name | Select-Object -First 1) {
            $dependencyPath=$loadedDependency.Path.Replace("'","''")
            $code+="`nImport-Module '$dependencyPath' -DisableNameChecking -ErrorAction Stop`n"
        } else {
            $spec=ConvertTo-Json -InputObject $dependency -Depth 5 -Compress
            $spec64=[Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($spec))
            $code+="`n`$dependency=ConvertFrom-Json -AsHashtable -InputObject ([Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('$spec64'))); Import-Module -FullyQualifiedName `$dependency -DisableNameChecking -ErrorAction Stop`n"
        }
    }
    $code+=@"

`$sources=ConvertFrom-Json -InputObject ([Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('$encoded')))
foreach(`$entry in `$sources) {
    `$text=[Text.Encoding]::UTF8.GetString([Convert]::FromBase64String(`$entry.Code))
    `$virtualPath=Join-Path `$script:DevPoShProjectRoot `$entry.Relative
    `$parseTokens=`$null;`$parseErrors=`$null
    `$ast=[Management.Automation.Language.Parser]::ParseInput(`$text,`$virtualPath,[ref]`$parseTokens,[ref]`$parseErrors)
    if(`$parseErrors.Count){throw 'Compiled source could not be parsed.'}
    . `$ast.GetScriptBlock()
}
"@
    if($providerMetadata) {
        $metadata=@{};foreach($key in $providerMetadata.Keys){$metadata[$key]=$providerMetadata[$key]}
        $metadata.SourceRoot=$root;$metadata.ModuleGuid=[string]$Project.Data.GUID
        $encodedMetadata=[Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes((ConvertTo-Json $metadata -Depth 10 -Compress)))
        $code+="`n`$providerMetadata=ConvertFrom-Json -AsHashtable -InputObject ([Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('$encodedMetadata')))`n"
        $code+="`$providerMetadata.AssetRoot=`$script:DevPoShProjectRoot`n`$providerMetadata.CompiledRegistrationPath=Join-Path `$script:DevPoShProjectRoot `$providerMetadata.RegistrationScript`n"
        $code+="`$ExecutionContext.SessionState.Module.PrivateData=@{OutputPumps=`$providerMetadata}`n"
    }
    $exports=@($source | Where-Object Category -eq Public | ForEach-Object Name)
    if($Project.Data.ContainsKey('FunctionsToExport') -and '*' -notin @($Project.Data.FunctionsToExport)){$exports=@($Project.Data.FunctionsToExport)}
    $variables=@($source | Where-Object Category -eq Variables | ForEach-Object Name)
    if($Project.Data.ContainsKey('VariablesToExport') -and '*' -notin @($Project.Data.VariablesToExport)){$variables=@($Project.Data.VariablesToExport)}
    $code+="`nExport-ModuleMember -Function @("+(($exports | ForEach-Object {"'$_'"}) -join ',')+')'
    if($variables.Count){$code+=' -Variable @('+(($variables | ForEach-Object {"'$_'"}) -join ',')+')'}
    if($Project.Data.ContainsKey('AliasesToExport') -and $Project.Data.AliasesToExport){$code+=' -Alias @('+(($Project.Data.AliasesToExport | ForEach-Object {"'$_'"}) -join ',')+')'}
    [pscustomobject]@{Code=$code+"`n";Assets=$packaged}
}
