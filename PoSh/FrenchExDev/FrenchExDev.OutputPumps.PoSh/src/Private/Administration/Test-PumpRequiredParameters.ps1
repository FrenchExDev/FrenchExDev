<#
.SYNOPSIS
Check whether supplied arguments satisfy a parameter set.
.DESCRIPTION
Define Test-PumpRequiredParameters for the OutputPumps runtime. Accept the call when at least one command parameter set has no missing mandatory argument.
#>


function Test-PumpRequiredParameters {
    <#
    .SYNOPSIS
    Check whether supplied arguments satisfy a parameter set.
    .DESCRIPTION
    Accept the call when at least one command parameter set has no missing mandatory argument.
    #>
    
    param([Management.Automation.CommandInfo]$Command,[Collections.IDictionary]$Parameters)
    foreach($set in $Command.ParameterSets){
        $missing=@($set.Parameters | Where-Object {$_.IsMandatory -and !$Parameters.Contains($_.Name)})
        if(!$missing.Count){return $true}
    }
    return $false
}
