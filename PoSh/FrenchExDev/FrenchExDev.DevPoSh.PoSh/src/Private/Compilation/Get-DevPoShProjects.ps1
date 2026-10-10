function Get-DevPoShProjects {
    param([Parameter(Mandatory)][string[]]$Path)
    $projects=[Collections.Generic.List[object]]::new()
    $seen=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach($selection in $Path) {
        $resolved=(Resolve-Path -LiteralPath $selection -ErrorAction Stop).ProviderPath
        $files=if(Test-Path -LiteralPath $resolved -PathType Leaf){@(Get-Item -LiteralPath $resolved)}else{
            $directories=[Collections.Generic.Stack[string]]::new();$directories.Push($resolved)
            while($directories.Count){
                $directory=$directories.Pop()
                Get-ChildItem -LiteralPath $directory -Filter '*.psd1' -File
                foreach($child in Get-ChildItem -LiteralPath $directory -Directory){
                    if(!$child.Name.StartsWith('.') -and $child.Name -notin @('src','test','tests','resources','node_modules')){$directories.Push($child.FullName)}
                }
            }
        }
        foreach($file in $files | Sort-Object FullName) {
            $data=Import-PowerShellDataFile -LiteralPath $file.FullName -ErrorAction Stop
            if(!$data.ContainsKey('RootModule') -and !$data.ContainsKey('ModuleToProcess')){continue}
            if($data.ContainsKey('PrivateData') -and $data.PrivateData.ContainsKey('DevPoSh') -and $data.PrivateData.DevPoSh.ContainsKey('Primary') -and !$data.PrivateData.DevPoSh.Primary){continue}
            if(!$seen.Add($file.DirectoryName)){continue}
            $dependencies=@(if($data.ContainsKey('RequiredModules')){foreach($dependency in $data.RequiredModules){if($dependency -is [Collections.IDictionary]){$dependency.ModuleName}else{[string]$dependency}}})
            $projects.Add([pscustomobject]@{Path=$file.DirectoryName;Manifest=$file.FullName;Name=$file.BaseName;Acme=(Split-Path (Split-Path $file.DirectoryName) -Leaf);Data=$data;Dependencies=$dependencies})
        }
    }
    $ordered=[Collections.Generic.List[object]]::new()
    if(@($projects | Group-Object Name | Where-Object Count -gt 1).Count){throw 'Ambiguous project module names.'}
    $pending=[Collections.Generic.List[object]]::new();foreach($project in $projects){$pending.Add($project)}
    while($pending.Count) {
        $ready=@($pending | Where-Object { $p=$_; !@($p.Dependencies | Where-Object { $dependency=$_; @($pending | Where-Object {$_.Name -eq $dependency -or (Split-Path $_.Path -Leaf) -eq $dependency}).Count }).Count })
        if(!$ready.Count){throw 'Cyclic DevPoSh module dependencies.'}
        foreach($project in $ready | Sort-Object Name){$ordered.Add($project);[void]$pending.Remove($project)}
    }
    return $ordered.ToArray()
}
