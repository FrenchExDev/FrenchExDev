function Get-ConfiguredOutputPumps {
    [CmdletBinding()]
    param([string]$Path=(Join-Path $HOME '.pumps-config.yaml'))
    $resolved=$ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Path)
    if(!(Test-Path -LiteralPath $resolved -PathType Leaf)){throw 'Create ~/.pumps-config.yaml or specify -Path.'}
    if(!(Get-Command ConvertFrom-Yaml -ErrorAction SilentlyContinue)){Import-Module powershell-yaml -MinimumVersion 0.4.12 -ErrorAction Stop}
    $yaml=ConvertFrom-Yaml -Yaml ([IO.File]::ReadAllText($resolved)) -ErrorAction Stop
    if($yaml -isnot [Collections.IDictionary] -or $yaml['version'] -ne 2){throw 'OutputPumps requires YAML version: 2. Migrate sources to roots in ~/.pumps-config.yaml.'}
    foreach($key in $yaml.Keys){if($key -notin @('version','roots','providers','pumps')){throw "Unknown configuration field: $key"}}
    $roots=[Collections.Generic.List[object]]::new()
    $seen=[Collections.Generic.HashSet[string]]::new($(if($IsWindows){[StringComparer]::OrdinalIgnoreCase}else{[StringComparer]::Ordinal}))
    foreach($entry in @($yaml['roots'])) {
        if($entry -isnot [Collections.IDictionary] -or !$entry['path']){throw 'Each root requires a path.'}
        foreach($key in $entry.Keys){if($key -notin @('path','enabled')){throw "Unknown root field: $key"}}
        if($entry.Contains('enabled') -and $entry['enabled'] -isnot [bool]){throw 'Root enabled must be a boolean.'}
        $p=[string]$entry['path']
        if($p -eq '~'){$p=$HOME}elseif($p.StartsWith('~/') -or $p.StartsWith('~\')){$p=Join-Path $HOME $p.Substring(2)}elseif(![IO.Path]::IsPathRooted($p)){$p=Join-Path (Split-Path $resolved) $p}
        $p=[IO.Path]::GetFullPath($p)
        if(!$seen.Add($p)){throw 'Duplicate configured root.'}
        $roots.Add([pscustomobject]@{Path=$p;Enabled=(!$entry.Contains('enabled') -or $entry['enabled'])})
    }
    $providers=@{};$pumps=@{}
    if($yaml.Contains('providers') -and $null -ne $yaml['providers']) {
        if($yaml['providers'] -isnot [Collections.IDictionary]){throw 'providers must be a mapping.'}
        foreach($id in $yaml['providers'].Keys) {
            $selection=$yaml['providers'][$id]
            if($selection -isnot [Collections.IDictionary]){throw 'Provider selection requires enabled/options fields.'}
            foreach($key in $selection.Keys){if($key -notin @('enabled','options')){throw "Unknown provider field: $key"}}
            if($selection.Contains('enabled') -and $selection['enabled'] -isnot [bool]){throw 'Provider enabled must be a boolean.'}
            if($selection.Contains('options') -and $selection['options'] -isnot [Collections.IDictionary]){throw 'Provider options must be a mapping.'}
            $providers[$id]=[pscustomobject]@{Enabled=(!$selection.Contains('enabled') -or $selection['enabled']);Options=$(if($selection.Contains('options')){$selection['options']}else{@{}})}
        }
    }
    if($yaml.Contains('pumps') -and $null -ne $yaml['pumps']) {
        if($yaml['pumps'] -isnot [Collections.IDictionary]){throw 'pumps must be a mapping.'}
        foreach($id in $yaml['pumps'].Keys){if($yaml['pumps'][$id] -isnot [bool]){throw 'Pump selection must be a boolean.'};$pumps[$id]=$yaml['pumps'][$id]}
    }
    [pscustomobject]@{PSTypeName='FrenchExDev.OutputPump.Configuration';ConfigurationPath=$resolved;Version=2;Roots=$roots.ToArray();Sources=$roots.ToArray();Providers=$providers;Pumps=$pumps}
}
