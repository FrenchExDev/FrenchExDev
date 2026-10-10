function Get-OutputPumpTaskState {
    $variable=Get-Variable -Name OutputPumpTaskStack -Scope Script -ErrorAction SilentlyContinue
    if($variable -and $variable.Value.Count){return $variable.Value.Peek()}
    return $null
}
function Add-OutputPumpTaskResult {
    param($State,$Result,[string]$Name)
    $seen=$null
    if($State.Seen.TryGetValue($Result,[ref]$seen)){return}
    $State.Seen.Add($Result,[object]::new())
    $State.Steps++
    switch($Result.Outcome){
        'Failed' {$State.Failed++}
        'Timeout' {$State.Timeout++}
        'Unsupported' {$State.Unsupported++}
        'Success' {}
        default {$State.ScriptErrors++}
    }
    if($Result.Truncation.Applied){$State.Truncated++}
    switch($Result.RetryAdvice){'Unknown' {$State.UnknownRetry=$true};'CorrectRequest' {$State.CorrectionRequired=$true};'NarrowRequest' {$State.NarrowRequired=$true}}
    if($Result.Outcome -ne 'Success' -or $Result.Truncation.Applied) {
        if($State.Issues.Count -ge 3 -and $Result.Outcome -ne 'Success') {
            for($index=0;$index -lt $State.Issues.Count;$index++) {
                if($State.Issues[$index].Outcome -eq 'Success'){$State.Issues.RemoveAt($index);$State.OmittedIssues++;break}
            }
        }
        if($State.Issues.Count -lt 3){
            $errorCode=$null
            if($Result.Error -is [Collections.IDictionary]){$errorCode=$Result.Error['Code']}
            elseif($Result.Error -and $Result.Error.PSObject.Properties['Code']){$errorCode=$Result.Error.Code}
            $issue=@{Step=$Name;Outcome=$Result.Outcome;ExitCode=$Result.ExitCode;ErrorCode=$errorCode;RetryAdvice=$Result.RetryAdvice;Truncated=[bool]$Result.Truncation.Applied;TotalCount=$Result.Truncation.TotalCount;Reasons=@($Result.Truncation.Reasons | Select-Object -First 3)}
            if($Result.Outcome -ne 'Success'){$State.Issues.Insert(0,$issue)}else{$State.Issues.Add($issue)}
        }else{$State.OmittedIssues++}
    }
}
