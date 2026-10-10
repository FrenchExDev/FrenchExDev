function Get-OutputPumpProviders {
    param([Parameter(Mandatory)]$Configuration)
    $found=@{};$paths=[Collections.Generic.HashSet[string]]::new($(if($IsWindows){[StringComparer]::OrdinalIgnoreCase}else{[StringComparer]::Ordinal}))
    foreach($root in $Configuration.Roots | Where-Object Enabled) {
        if(!(Test-Path -LiteralPath $root.Path -PathType Container)){continue}
        $pending=[Collections.Generic.Stack[string]]::new();$pending.Push($root.Path)
        while($pending.Count) {
            $directory=$pending.Pop()
            if(!$paths.Add([IO.Path]::GetFullPath($directory))){continue}
            foreach($file in Get-ChildItem -LiteralPath $directory -Filter '*.psd1' -File | Sort-Object Name) {
                $data=Import-PowerShellDataFile -LiteralPath $file.FullName -ErrorAction Stop
                if(!$data.ContainsKey('PrivateData') -or !$data.PrivateData.ContainsKey('OutputPumps')){continue}
                $meta=$data.PrivateData.OutputPumps
                if($meta.Version -ne 1 -or $meta.ProviderId -notmatch '^[a-z][a-z0-9.-]*$' -or !$meta.RegistrationScript -or !$data.GUID){throw 'Invalid OutputPumps provider manifest.'}
                $scriptPath=[IO.Path]::GetFullPath((Join-Path $directory $meta.RegistrationScript))
                $srcPrefix=[IO.Path]::GetFullPath((Join-Path $directory 'src'))+[IO.Path]::DirectorySeparatorChar
                if(!$scriptPath.StartsWith($srcPrefix,[StringComparison]::OrdinalIgnoreCase) -or [IO.Path]::GetFileName($scriptPath) -ne 'Setup-OutputPumps.ps1'){throw 'Provider registration script must be declared under src.'}
                if($found.ContainsKey($meta.ProviderId)){throw 'Duplicate OutputPumps provider identity.'}
                $found[$meta.ProviderId]=[pscustomobject]@{ProviderId=$meta.ProviderId;ProviderRoot=$directory;ManifestPath=$file.FullName;RegistrationPath=$scriptPath;ModuleGuid=[string]$data.GUID;ModuleName=$file.BaseName;Module=$null}
            }
            foreach($child in Get-ChildItem -LiteralPath $directory -Directory) {
                if(!$child.Name.StartsWith('.') -and $child.Name -notin @('src','test','tests','resources','node_modules','bin','obj') -and !($child.Attributes -band [IO.FileAttributes]::ReparsePoint)){$pending.Push($child.FullName)}
            }
        }
    }
    foreach($module in Get-Module -All) {
        if($module.PrivateData -isnot [Collections.IDictionary] -or !$module.PrivateData.Contains('OutputPumps')){continue}
        $meta=$module.PrivateData.OutputPumps
        if(!$meta.Contains('ProviderId')){continue}
        $origin=if($meta.Contains('SourceRoot')){$meta.SourceRoot}else{$module.ModuleBase}
        $within=@($Configuration.Roots | Where-Object {$_.Enabled -and (Test-OutputPumpContainedPath $origin $_.Path -AllowEqual)}).Count -gt 0
        if(!$within){continue}
        $guid=if($meta.Contains('ModuleGuid')){[string]$meta.ModuleGuid}else{[string]$module.Guid}
        $id=$meta.ProviderId
        if(!$found.ContainsKey($id)) {
            $registration=if($meta.Contains('CompiledRegistrationPath')){$meta.CompiledRegistrationPath}else{Join-Path $origin $meta.RegistrationScript}
            $found[$id]=[pscustomobject]@{ProviderId=$id;ProviderRoot=$origin;ManifestPath=$null;RegistrationPath=$registration;ModuleGuid=$guid;ModuleName=$module.Name;Module=$null}
        }
        $entry=$found[$id]
        if($entry.ModuleGuid -ne $guid -or [IO.Path]::GetFullPath($entry.ProviderRoot) -ne [IO.Path]::GetFullPath($origin)){throw 'Loaded provider conflicts with configured provider identity.'}
        if($entry.Module -and ![object]::ReferenceEquals($entry.Module,$module)){throw 'Multiple compatible provider instances are loaded.'}
        $entry.Module=$module
        if($meta.Contains('CompiledRegistrationPath')){$entry.RegistrationPath=$meta.CompiledRegistrationPath}
    }
    foreach($id in $Configuration.Providers.Keys){if(!$found.ContainsKey($id) -and $Configuration.Providers[$id].Enabled){throw "Configured provider not found: $id"}}
    foreach($entry in $found.Values | Sort-Object ProviderId) {
        if($Configuration.Providers.ContainsKey($entry.ProviderId) -and !$Configuration.Providers[$entry.ProviderId].Enabled){continue}
        if(!(Test-Path -LiteralPath $entry.RegistrationPath -PathType Leaf)){throw 'Provider registration script unavailable; restore provider sources or rebuild its compiled artifact.'}
        $entry
    }
}

function Test-OutputPumpContainedPath {
    param([string]$Path,[string]$Root,[switch]$AllowEqual)
    $p=[IO.Path]::GetFullPath($Path).TrimEnd('/','\');$r=[IO.Path]::GetFullPath($Root).TrimEnd('/','\')
    $comparison=if($IsWindows){[StringComparison]::OrdinalIgnoreCase}else{[StringComparison]::Ordinal}
    ($AllowEqual -and $p.Equals($r,$comparison)) -or $p.StartsWith($r+[IO.Path]::DirectorySeparatorChar,$comparison)
}
