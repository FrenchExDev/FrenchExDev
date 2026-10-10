function Complete-OutputPumpResult {
    param($Result,[string]$PumpId,[scriptblock]$Filter,[scriptblock]$Map,[int]$MaxItems=20,[int]$MaxOutputBytes=8192)
    $data = [pscustomobject]$Result.Data
    $data.PSObject.TypeNames.Insert(0,"FrenchExDev.OutputPump.$PumpId")
    try {
        if ($Filter) {
            $decision = @(& $Filter $data *>&1)
            if ($decision.Count -ne 1 -or $decision[0] -isnot [bool]) { throw 'Filter must return exactly one boolean.' }
            if (!$decision[0]) { $data = [pscustomobject]@{Items=@()} }
        }
        if ($Map) {
            $projection=@(& $Map $data *>&1)
            foreach($value in $projection){if($value -is [Management.Automation.InformationalRecord] -or $value -is [Management.Automation.ErrorRecord]){throw 'Map emitted a diagnostic stream.'}}
            $data=if(!$projection.Count){$null}elseif($projection.Count -eq 1){$projection[0]}else{$projection}
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
        $safe = New-PumpResult @arguments
        $safe.Data = [pscustomobject]$safe.Data
        $safe.Data.PSObject.TypeNames.Insert(0,"FrenchExDev.OutputPump.$PumpId")
        foreach($typeName in $businessTypes){if($typeName -notin $safe.Data.PSObject.TypeNames){$safe.Data.PSObject.TypeNames.Add($typeName)}}
        $safe.PSObject.TypeNames.Insert(0,'FrenchExDev.OutputPump.Result')
        return $safe
    } catch {
        return New-PumpResult -Summary 'Pump transformation failed.' -Outcome Failed -ExitCode $Result.ExitCode -RetryAdvice CorrectRequest -MaxOutputBytes $MaxOutputBytes -Truncated:([bool]$Result.Truncation.Applied) -Reasons @($Result.Truncation.Reasons) -ErrorInfo @{Code='TransformationFailed';NativeOutcome=$Result.Outcome;ExceptionType=$_.Exception.GetType().Name}
    }
}
