function Register-OutputPump {
    [CmdletBinding(DefaultParameterSetName='Native')]
    param(
        [Parameter(Mandatory,Position=0,ParameterSetName='Documentation')][Collections.IDictionary]$Descriptor,
        [Parameter(ParameterSetName='Documentation')][string]$LibraryRoot,
        [Parameter(Mandatory,ParameterSetName='Native')][Parameter(Mandatory,ParameterSetName='Object')][ValidatePattern('^[a-z][a-z0-9.-]*$')][string]$Id,
        [Parameter(Mandatory,ParameterSetName='Native')][Collections.IDictionary]$Route,
        [Parameter(Mandatory,ParameterSetName='Native')][scriptblock]$Handler,
        [Parameter(Mandatory,ParameterSetName='Object')][scriptblock]$TestInput,
        [Parameter(Mandatory,ParameterSetName='Object')][scriptblock]$Transform,
        [Parameter(ParameterSetName='Native')][Management.Automation.CommandInfo]$Command,
        [string]$ProviderId='live',[switch]$Replace
    )
    if($PSCmdlet.ParameterSetName -eq 'Documentation'){if(!$LibraryRoot){$LibraryRoot=$script:PumpLibraryRoot};return Register-OutputPumpDocumentation -Descriptor $Descriptor -LibraryRoot $LibraryRoot}
    $session=$global:FrenchExDevOutputPumpSession
    $transaction=$session.Transaction
    if($transaction) {
        if($ProviderId -ne $transaction.ProviderId){throw 'Provider cannot register under another identity.'}
        [void]$transaction.KnownIds.Add($Id)
        if($transaction.Configuration.Pumps.ContainsKey($Id) -and !$transaction.Configuration.Pumps[$Id]){return}
        if($Replace){throw 'Provider setup cannot replace another provider or a live override.'}
    }elseif(!$session.Initialized){throw 'Run Setup-OutputPumps before registering live behavior.'}
    if($Route) {
        foreach($field in @('Executable','Tokens','Options','Positional','Multiple')){if(!$Route.Contains($field)){throw 'Native route requires Executable, Tokens, Options, Positional and Multiple.'}}
        if(!$Route.Executable -or $Route.Options -isnot [Collections.IDictionary] -or $Route.Multiple -isnot [bool]){throw 'Invalid native route.'}
    }
    $entry=[pscustomobject]@{Id=$Id;Software=$ProviderId;ProviderId=$ProviderId;Route=$Route;Handler=$Handler;TestInput=$TestInput;Transform=$Transform;Command=$Command;Function=$(if($Command){$Command.Name}else{$null});Origin=$(if($transaction){'Provider'}else{'Live'})}
    $routeKey=if($Route){"$($Route.Executable)|$($Route.Tokens -join '|')"}else{$null}
    $candidates=@(foreach($stack in $session.Registry.Values){if($stack.Count){$stack[$stack.Count-1]}})
    if($transaction){$candidates+=@($transaction.Entries)}
    $collisions=@($candidates | Where-Object {$_.Id -eq $Id -or ($routeKey -and $_.Route -and "$($_.Route.Executable)|$($_.Route.Tokens -join '|')" -eq $routeKey)})
    if($collisions.Count) {
        $ownOverlay=$transaction -and !@($collisions | Where-Object {$_.Id -ne $Id -or $_.Origin -ne 'Live' -or !$_.PSObject.Properties['BaseProviderId'] -or $_.BaseProviderId -ne $ProviderId}).Count
        if((!$Replace -and !$ownOverlay) -or @($collisions | Where-Object Id -ne $Id).Count){throw 'Duplicate pump identifier or native route. Use -Replace with the existing pump Id for a live override.'}
        $original=$collisions[-1]
        if($original.Route -and $routeKey -ne "$($original.Route.Executable)|$($original.Route.Tokens -join '|')"){throw 'A replacement must preserve its native route.'}
        if(!$transaction){$entry.Software=$original.Software;if(!$entry.Function){$entry.Function=$original.Function};$entry | Add-Member BaseProviderId $(if($original.PSObject.Properties['BaseProviderId']){$original.BaseProviderId}else{$original.ProviderId})}
    }
    if($transaction){$transaction.Entries.Add($entry)}else{
        if(!$session.Registry.ContainsKey($Id)){$session.Registry[$Id]=[Collections.Generic.List[object]]::new()}
        $session.Registry[$Id].Add($entry)
    }
    [pscustomobject]@{PSTypeName='FrenchExDev.OutputPump.Registration';Id=$Id;ProviderId=$ProviderId;Origin=$entry.Origin;Replaced=($collisions.Count -gt 0)}
}
