function Test-PumpRequiredParameters {
    param([Management.Automation.CommandInfo]$Command,[Collections.IDictionary]$Parameters)
    foreach($set in $Command.ParameterSets){
        $missing=@($set.Parameters | Where-Object {$_.IsMandatory -and !$Parameters.Contains($_.Name)})
        if(!$missing.Count){return $true}
    }
    return $false
}
