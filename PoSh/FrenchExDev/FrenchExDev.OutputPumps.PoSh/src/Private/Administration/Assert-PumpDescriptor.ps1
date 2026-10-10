function Assert-PumpDescriptor {
    param([System.Collections.IDictionary]$Descriptor,[string]$LibraryRoot)
    foreach($key in @('Id','Name','Purpose','Module','Documentation','VerifiedVersion','IdentityMarkdown','Commands')){
        if(!$Descriptor.Contains($key) -or !$Descriptor[$key]){throw "Missing descriptor field: $key"}
    }
    if($Descriptor.Id -notmatch '^[a-z][a-z0-9-]{0,39}$'){throw 'Invalid software id'}
    if($Descriptor.Name -match '[\r\n|]' -or $Descriptor.Purpose -match '[\r\n|]'){throw 'Invalid index text'}
    if($Descriptor.Module -notmatch '^(?:(?:Modules/|src/(?:Public|Private)/)(?:[A-Za-z0-9.-]+/)*[A-Za-z0-9.-]+\.ps(?:m)?1|[A-Za-z0-9.-]+\.psm1)$' -or $Descriptor.Module -match '(?:^|/)\.\.(?:/|$)'){throw 'Module must be a root loader or inside Modules or src/Public or src/Private'}
    $modulePath=Join-Path $LibraryRoot $Descriptor.Module
    if(!(Test-Path -LiteralPath $modulePath -PathType Leaf)){throw 'Module missing'}
    if((Get-Item -LiteralPath $modulePath).LinkType){throw 'Linked modules are not supported'}
    foreach($link in $Descriptor.Documentation){if($link -notmatch '^https://[^\s]+$'){throw 'Official documentation needs HTTPS links'}}
    $module=Import-Module $modulePath -PassThru -DisableNameChecking -ErrorAction Stop
    $names=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach($entry in $Descriptor.Commands) {
        foreach($key in @('Function','When','Example','Parameters')){if(!$entry.Contains($key) -or !$entry[$key]){throw "Missing command field: $key"}}
        if($entry.Function -notmatch '^[A-Za-z]+-[A-Za-z][A-Za-z0-9]*$' -or !$names.Add($entry.Function)){throw 'Invalid or duplicate function'}
        if(!$module.ExportedFunctions.ContainsKey($entry.Function)){throw 'Function is not exported'}
        $command=$module.ExportedFunctions[$entry.Function]
        if($entry.Contains('Id') -and $entry.Id -notmatch '^[a-z][a-z0-9.-]+$'){throw 'Invalid pump identifier'}
        foreach($parameter in (@('MaxItems','MaxOutputBytes','TimeoutSeconds')+@($entry.Parameters))){if(!$command.Parameters.ContainsKey($parameter)){throw "Unknown parameter: $parameter"}}
        if($entry.Example -notmatch ('^'+[regex]::Escape($entry.Function)+'(?:\s|$)')){throw 'Example must start with its function name'}
        foreach($helper in @('TestInput','Transform')){if($entry.Contains($helper)){if(!$module.ExportedFunctions.ContainsKey($entry[$helper])){throw 'Object pump helper is not exported'};[void]$names.Add($entry[$helper])}}
        if($entry.Contains('TestInput') -ne $entry.Contains('Transform')){throw 'Recognition and transformation must be declared together'}
        if($entry.Contains('Route')) {
            if(!$entry.Contains('Id') -or $entry.Id -notmatch '^[a-z][a-z0-9.-]+$'){throw 'Routed pump requires an identifier'}
            foreach($field in @('Executable','Tokens','Options','Positional','Multiple')){if(!$entry.Route.Contains($field)){throw 'Invalid native route'}}
            foreach($option in $entry.Route.Options.Values){if(!$command.Parameters.ContainsKey($option.Parameter)){throw 'Route references an unknown parameter'}}
        }
    }
    if($names.Count -ne $module.ExportedFunctions.Count){throw 'All exported software functions must be documented'}
}
