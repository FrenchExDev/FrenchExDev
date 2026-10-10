function Unregister-OutputPump {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Id)
    if($script:OutputPumpSession.Transaction){throw 'Cannot unregister during provider setup.'}
    $registry=$script:OutputPumpSession.Registry
    if(!$registry.ContainsKey($Id) -or !$registry[$Id].Count){throw 'Pump not registered.'}
    $removed=$registry[$Id][$registry[$Id].Count-1]
    $registry[$Id].RemoveAt($registry[$Id].Count-1)
    if(!$registry[$Id].Count){$registry.Remove($Id)}
    [pscustomobject]@{PSTypeName='FrenchExDev.OutputPump.Removal';Id=$Id;RemovedOrigin=$removed.Origin;Restored=$registry.ContainsKey($Id)}
}
