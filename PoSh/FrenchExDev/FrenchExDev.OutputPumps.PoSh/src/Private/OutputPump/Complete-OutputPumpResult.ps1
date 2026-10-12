<#
.SYNOPSIS
Apply business projections and enforce the final result contract.
.DESCRIPTION
Define Complete-OutputPumpResult for the OutputPumps runtime. Run Filter before Map, preserve technical outcome fields and convert callback errors into structured failures.
#>


function Complete-OutputPumpResult {
    <#
    .SYNOPSIS
    Apply business projections and enforce the final result contract.
    .DESCRIPTION
    Run Filter before Map, preserve technical outcome fields and convert callback errors into structured failures.
    #>
    
    param($Result,[string]$PumpId,[scriptblock]$Filter,[scriptblock]$Map,[int]$MaxItems=20,[int]$MaxOutputBytes=8192)
    $data = [pscustomobject]$Result.Data
    $data.PSObject.TypeNames.Insert(0,"FrenchExDev.OutputPump.$PumpId")
    try {
        if ($Filter) {
            $timer=[Diagnostics.Stopwatch]::StartNew()
            try {
            $decision = @(& $Filter $data *>&1)
            if ($decision.Count -ne 1 -or $decision[0] -isnot [bool]) { throw 'Filter must return exactly one boolean.' }
            if (!$decision[0]) { $data = [pscustomobject]@{Items=@()} }
            }finally{Write-OutputPumpTiming 'Projection.Filter' $timer.Elapsed.TotalMilliseconds}
        }
        if ($Map) {
            $timer=[Diagnostics.Stopwatch]::StartNew()
            try {
            $projection=@(& $Map $data *>&1)
            foreach($value in $projection){if($value -is [Management.Automation.InformationalRecord] -or $value -is [Management.Automation.ErrorRecord]){throw 'Map emitted a diagnostic stream.'}}
            $data=if(!$projection.Count){$null}elseif($projection.Count -eq 1){$projection[0]}else{$projection}
            }finally{Write-OutputPumpTiming 'Projection.Map' $timer.Elapsed.TotalMilliseconds}
        }
        $businessTypes=if($null -ne $data){@($data.PSObject.TypeNames)}else{@()}
        $dictionary = [ordered]@{}
        if ($data -is [Collections.IDictionary]) { foreach ($key in $data.Keys) { $dictionary[$key]=$data[$key] } }
        elseif ($data -is [pscustomobject]) { foreach ($p in $data.PSObject.Properties) { $dictionary[$p.Name]=$p.Value } }
        elseif ($null -eq $data) { $dictionary.Items=@() }
        elseif ($data -is [array]) { $dictionary.Items=$data }
        else { $dictionary.Value=$data }
        $arguments = @{Summary=$Result.Summary;Outcome=$Result.Outcome;Data=$dictionary;ExitCode=$Result.ExitCode;ErrorInfo=$Result.Error;RetryAdvice=$Result.RetryAdvice;MaxItems=$MaxItems;MaxOutputBytes=$MaxOutputBytes;Truncated=[bool]$Result.Truncation.Applied;Reasons=@($Result.Truncation.Reasons);UnknownTotal=($null -eq $Result.Truncation.TotalCount)}
        if ($null -ne $Result.Truncation.TotalCount) { $arguments.TotalCount=$Result.Truncation.TotalCount }
        $timer=[Diagnostics.Stopwatch]::StartNew()
        try{$safe = New-PumpResult @arguments}finally{Write-OutputPumpTiming 'Projection.Bound' $timer.Elapsed.TotalMilliseconds}
        $safe.Data = [pscustomobject]$safe.Data
        $safe.Data.PSObject.TypeNames.Insert(0,"FrenchExDev.OutputPump.$PumpId")
        foreach($typeName in $businessTypes){if($typeName -notin $safe.Data.PSObject.TypeNames){$safe.Data.PSObject.TypeNames.Add($typeName)}}
        $safe.PSObject.TypeNames.Insert(0,'FrenchExDev.OutputPump.Result')
        return $safe
    } catch {
        return New-PumpResult -Summary 'Pump transformation failed.' -Outcome Failed -ExitCode $Result.ExitCode -RetryAdvice CorrectRequest -MaxOutputBytes $MaxOutputBytes -Truncated:([bool]$Result.Truncation.Applied) -Reasons @($Result.Truncation.Reasons) -ErrorInfo @{Code='TransformationFailed';NativeOutcome=$Result.Outcome;ExceptionType=$_.Exception.GetType().Name}
    }
}
