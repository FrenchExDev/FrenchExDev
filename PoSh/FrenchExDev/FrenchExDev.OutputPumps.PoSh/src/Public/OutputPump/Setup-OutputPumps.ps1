function Setup-OutputPumps {
    [CmdletBinding()]
    param([string]$ConfigurationPath=(Join-Path $HOME '.pumps-config.yaml'),[string]$InventoryPath=(Join-Path $HOME '.pwsh-pumps/registry.json'),[string[]]$ProviderId)
    $session=$script:OutputPumpSession
    if($session.Transaction){return New-PumpResult -Summary 'OutputPumps setup is already active.' -Outcome Failed -RetryAdvice CorrectRequest -ErrorInfo @{Code='SetupReentrancy'}}
    $previousRegistry=$session.Registry;$previousProviders=$session.Providers;$previousConfiguration=$session.Configuration;$previousPath=$session.ConfigurationPath;$previousInitialized=$session.Initialized
    try {
        $configuration=Get-ConfiguredOutputPumps -Path $ConfigurationPath
        $inventory=New-OutputPumpCompilation -ConfigurationPath $configuration.ConfigurationPath -Path $InventoryPath
        if($inventory.Outcome -ne 'Success'){throw 'Inventory preparation failed.'}
        $allProviders=@(Get-OutputPumpProviders $configuration)
        $providers=$allProviders
        if($ProviderId){$providers=@($providers | Where-Object ProviderId -in $ProviderId);foreach($id in $ProviderId){if($id -notin @($providers | ForEach-Object ProviderId)){throw 'Requested provider is unavailable.'}}}
        $next=@{};foreach($id in $previousRegistry.Keys){$next[$id]=[Collections.Generic.List[object]]::new();foreach($entry in $previousRegistry[$id]){$next[$id].Add($entry)}}
        $session.Registry=$next;$states=@{};$reports=[Collections.Generic.List[object]]::new()
        if($ProviderId){foreach($id in $previousProviders.Keys){if($id -notin $ProviderId -and $id -in @($allProviders | ForEach-Object ProviderId)){$states[$id]=$previousProviders[$id]}}}
        $configurationChanged=$previousPath -ne $configuration.ConfigurationPath -or (ConvertTo-Json $previousConfiguration -Depth 16 -Compress) -cne (ConvertTo-Json $configuration -Depth 16 -Compress)
        foreach($id in @($next.Keys)) {
            foreach($entry in @($next[$id].ToArray())) {
                if($entry.Origin -eq 'Provider' -and $entry.ProviderId -notin @($allProviders | ForEach-Object ProviderId)){[void]$next[$id].Remove($entry)}
            }
            if(!$next[$id].Count){$next.Remove($id)}
        }
        foreach($provider in $providers) {
            $id=$provider.ProviderId
            $hash=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([IO.File]::ReadAllBytes($provider.RegistrationPath)))
            $old=if($previousProviders.ContainsKey($id)){$previousProviders[$id]}else{$null}
            $unchanged=$old -and !$configurationChanged -and $old.Hash -eq $hash -and [object]::ReferenceEquals($old.Module,$provider.Module)
            if($unchanged){$states[$id]=$old;$reports.Add(@{ProviderId=$id;Changed=$false;Reused=$true});continue}
            foreach($key in @($next.Keys)) {
                foreach($entry in @($next[$key].ToArray())){if($entry.Origin -eq 'Provider' -and $entry.ProviderId -eq $id){[void]$next[$key].Remove($entry)}}
                if(!$next[$key].Count){$next.Remove($key)}
            }
            $options=if($configuration.Providers.ContainsKey($id)){$configuration.Providers[$id].Options}else{@{}}
            $context=[pscustomobject]@{PSTypeName='FrenchExDev.OutputPump.SetupContext';ProviderId=$id;ProviderRoot=$provider.ProviderRoot;ManifestPath=$provider.ManifestPath;RegistrationPath=$provider.RegistrationPath;Module=$provider.Module;Options=$options;Register=(Get-Command Register-OutputPump -CommandType Function)}
            if(!$provider.Module){
                # Publish the existing engine instance for RequiredModules resolution when
                # setup is called from a script-local import. This does not reload code.
                Import-Module -ModuleInfo $ExecutionContext.SessionState.Module -Global -DisableNameChecking -ErrorAction Stop
            }
            $session.Transaction=[pscustomobject]@{ProviderId=$id;Configuration=$configuration;Entries=[Collections.Generic.List[object]]::new();KnownIds=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)}
            $messages=@(& $provider.RegistrationPath -Context $context *>&1)
            $registrationErrors=@($messages | Where-Object {$_ -is [Management.Automation.ErrorRecord]})
            if($registrationErrors.Count){throw ('Provider registration failed: '+$registrationErrors[0].Exception.Message)}
            foreach($entry in $session.Transaction.Entries) {
                if(!$next.ContainsKey($entry.Id)){$next[$entry.Id]=[Collections.Generic.List[object]]::new()}
                $next[$entry.Id].Insert(0,$entry)
            }
            $states[$id]=[pscustomobject]@{Hash=$hash;Module=$context.Module;KnownIds=@($session.Transaction.KnownIds)}
            $session.Transaction=$null
            $reports.Add(@{ProviderId=$id;Changed=$true;Reused=($null -ne $provider.Module)})
        }
        if(!$ProviderId){foreach($id in $configuration.Pumps.Keys){if($configuration.Pumps[$id] -and $id -notin @($states.Values | ForEach-Object KnownIds) -and !$next.ContainsKey($id)){throw 'Configuration enables an unknown pump.'}}}
        $session.Providers=$states;$session.Configuration=$configuration;$session.ConfigurationPath=$configuration.ConfigurationPath;$session.Initialized=$true;$session.LastError=$null
        New-PumpResult -Summary 'OutputPumps session ready.' -Data @{Items=$reports.ToArray();Providers=$states.Count;Pumps=@(Get-OutputPumpRegistry).Count}
    }catch{
        $session.Registry=$previousRegistry;$session.Providers=$previousProviders;$session.Configuration=$previousConfiguration;$session.ConfigurationPath=$previousPath;$session.Initialized=$previousInitialized;$session.LastError='SetupFailed'
        New-PumpResult -Summary 'OutputPumps setup failed; no partial registrations published.' -Outcome Failed -RetryAdvice CorrectRequest -ErrorInfo @{Code='SetupFailed';Message=$_.Exception.Message;Line=$_.InvocationInfo.ScriptLineNumber}
    }finally{$session.Transaction=$null}
}
