function New-OutputPumpResult {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Summary,[object]$Data=@{},[ValidateSet('Success','Failed','Timeout','Unsupported')][string]$Outcome='Success',
          [Nullable[int]]$ExitCode=$null,[Collections.IDictionary]$ErrorInfo,[string]$RetryAdvice='SafeToRetry',
          [ValidateRange(1,1000)][int]$MaxItems=20,[ValidateRange(1024,8192)][int]$MaxOutputBytes=8192,[switch]$Truncated,[string[]]$Reasons=@())
    $parameters=@{};foreach($key in $PSBoundParameters.Keys){$parameters[$key]=$PSBoundParameters[$key]}
    $typeNames=@('FrenchExDev.OutputPump.Data')
    if($Data -is [pscustomobject]){
        $typeNames=@($Data.PSObject.TypeNames)+$typeNames
        $dictionary=[ordered]@{};foreach($property in $Data.PSObject.Properties){$dictionary[$property.Name]=$property.Value}
        $parameters.Data=$dictionary
    }elseif($Data -isnot [Collections.IDictionary]){
        return New-PumpResult -Summary 'Business data must be a PSCustomObject or dictionary.' -Outcome Failed -RetryAdvice CorrectRequest -MaxOutputBytes $MaxOutputBytes -ErrorInfo @{Code='InvalidBusinessData'}
    }
    $result=New-PumpResult @parameters
    $result.Data=[pscustomobject]$result.Data
    foreach($typeName in $typeNames){if($typeName -notin $result.Data.PSObject.TypeNames){$result.Data.PSObject.TypeNames.Add($typeName)}}
    $result.PSObject.TypeNames.Insert(0,'FrenchExDev.OutputPump.Result')
    $result
}
