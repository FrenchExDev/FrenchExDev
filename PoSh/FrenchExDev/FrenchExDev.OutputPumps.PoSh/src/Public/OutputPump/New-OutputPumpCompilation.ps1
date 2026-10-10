function New-OutputPumpCompilation {
    [CmdletBinding()]
    param([string]$ConfigurationPath=(Join-Path $HOME '.pumps-config.yaml'),[string]$Path=(Join-Path $HOME '.pwsh-pumps/registry.json'))
    $lock=$null
    try {
        if([IO.Path]::GetExtension($Path) -ne '.json'){throw 'OutputPumps now compiles a JSON inventory. Use registry.json instead of OutputPump.psm1.'}
        $configuration=Get-ConfiguredOutputPumps -Path $ConfigurationPath
        $providers=@(Get-OutputPumpProviders $configuration)
        $entries=@(foreach($p in $providers){[ordered]@{ProviderId=$p.ProviderId;ProviderRoot=$p.ProviderRoot;ManifestPath=$p.ManifestPath;RegistrationPath=$p.RegistrationPath;ModuleGuid=$p.ModuleGuid;ModuleName=$p.ModuleName;RegistrationHash=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([IO.File]::ReadAllBytes($p.RegistrationPath)))}})
        $configurationHash=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([IO.File]::ReadAllBytes($configuration.ConfigurationPath)))
        $text=ConvertTo-Json -InputObject ([ordered]@{Version=2;ConfigurationPath=$configuration.ConfigurationPath;ConfigurationHash=$configurationHash;Providers=$entries}) -Depth 12 -Compress
        $destination=$ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Path)
        [void][IO.Directory]::CreateDirectory((Split-Path $destination))
        $lock=[IO.File]::Open($destination+'.lock',[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)
        $changed=![IO.File]::Exists($destination) -or [IO.File]::ReadAllText($destination) -cne $text
        if($changed){Write-PumpAtomicText $destination $text}
        New-PumpResult -Summary 'OutputPumps loading inventory prepared.' -Data @{Items=@(@{Path=$destination;Changed=$changed;Providers=$providers.Count})}
    }catch{New-PumpResult -Summary 'OutputPumps inventory could not be prepared.' -Outcome Failed -RetryAdvice CorrectRequest -ErrorInfo @{Code='InventoryFailed';Message=$_.Exception.Message}}
    finally{if($lock){$lock.Dispose()}}
}
