function Invoke-OutputPumpTask {
    [CmdletBinding()]
    param([Parameter(Mandatory)][scriptblock]$ScriptBlock,[object[]]$ArgumentList=@(),
          [ValidateRange(1,1000)][int]$MaxItems=20,[ValidateRange(1024,8192)][int]$MaxOutputBytes=8192,[switch]$AsJson)
    if(!(Get-Variable -Name OutputPumpTaskStack -Scope Script -ErrorAction SilentlyContinue)){$script:OutputPumpTaskStack=[Collections.Generic.Stack[object]]::new()}
    $state=@{Steps=0;Failed=0;Timeout=0;Unsupported=0;Truncated=0;ScriptErrors=0;OmittedIssues=0;UnknownRetry=$false;CorrectionRequired=$false;NarrowRequired=$false;Budget=$MaxOutputBytes;Issues=[Collections.Generic.List[object]]::new();Seen=[Runtime.CompilerServices.ConditionalWeakTable[object,object]]::new()}
    $items=[Collections.Generic.List[object]]::new();$produced=0L;$projectionTruncated=$false
    $script:OutputPumpTaskStack.Push($state)
    try {
        & $ScriptBlock @ArgumentList *>&1 | ForEach-Object {
            $value=$_
            if($value -is [Management.Automation.ErrorRecord] -or $value -is [Management.Automation.InformationalRecord] -or $value -is [Management.Automation.InformationRecord]){$state.ScriptErrors++;return}
            if($value.PSObject.Properties['Outcome'] -and $value.PSObject.Properties['Truncation']) {
                Add-OutputPumpTaskResult -State $state -Result $value -Name 'Output'
                # Envelopes are intermediate results. Project the required business values explicitly.
                return
            }
            $produced++
            if($items.Count -lt $MaxItems) {
                $safe=New-PumpResult -Summary 'Task projection.' -Data @{Items=@($value)} -MaxItems 1 -MaxOutputBytes $MaxOutputBytes
                if($safe.Truncation.Applied){$projectionTruncated=$true}
                foreach($item in $safe.Data.Items){$items.Add($item)}
            }else{$projectionTruncated=$true}
        }
    }catch{$state.ScriptErrors++}
    finally{[void]$script:OutputPumpTaskStack.Pop()}
    $outcome=if($state.Timeout){'Timeout'}elseif($state.Failed -or $state.ScriptErrors){'Failed'}elseif($state.Unsupported){'Unsupported'}else{'Success'}
    $summary="Task: $($state.Steps) steps; $($state.Failed) failed; $($state.Timeout) timeout; $($state.Unsupported) unsupported; $($state.Truncated) truncated; $($state.ScriptErrors) script errors."
    $status=@{Steps=$state.Steps;Failed=$state.Failed;Timeout=$state.Timeout;Unsupported=$state.Unsupported;Truncated=$state.Truncated;ScriptErrors=$state.ScriptErrors;OmittedIssues=$state.OmittedIssues}
    $errorInfo=if($outcome -ne 'Success'){@{Code='TaskFailed';Failed=$state.Failed;Timeout=$state.Timeout;Unsupported=$state.Unsupported;ScriptErrors=$state.ScriptErrors}}else{$null}
    $reasons=[Collections.Generic.List[string]]::new()
    if($state.Truncated){$reasons.Add('IntermediateTruncation')}
    if($projectionTruncated){$reasons.Add('TaskProjectionLimit')}
    if($state.OmittedIssues){$reasons.Add('TaskIssueLimit')}
    $retry=if($state.Timeout -or $state.UnknownRetry){'Unknown'}elseif($state.ScriptErrors -or $state.CorrectionRequired){'CorrectRequest'}elseif($state.NarrowRequired){'NarrowRequest'}elseif($outcome -ne 'Success'){'CorrectRequest'}elseif($reasons.Count){'NarrowRequest'}else{'SafeToRetry'}
    # Reserve space for status and failures before adding the final business projection.
    do {
        $arguments=@{Summary=$summary;Outcome=$outcome;RetryAdvice=$retry;ErrorInfo=$errorInfo;Data=@{Status=$status;Issues=$state.Issues.ToArray();Items=@()};Truncated=($reasons.Count -gt 0);Reasons=$reasons.ToArray();TotalCount=$produced;MaxItems=$MaxItems;MaxOutputBytes=8192}
        $base=New-PumpResult @arguments
        $size=[Text.Encoding]::UTF8.GetByteCount((ConvertTo-Json $base -Depth 24 -Compress))
        if($size -le $MaxOutputBytes -or !$state.Issues.Count){break}
        $state.Issues.RemoveAt($state.Issues.Count-1);$state.OmittedIssues++;$status.OmittedIssues=$state.OmittedIssues
        if(!$reasons.Contains('TaskIssueLimit')){$reasons.Add('TaskIssueLimit')}
    }while($true)
    $arguments.Data.Items=$items.ToArray();$arguments.MaxOutputBytes=$MaxOutputBytes
    $result=New-PumpResult @arguments
    if($result.Truncation.Applied -and $result.Outcome -eq 'Success'){$result.RetryAdvice='NarrowRequest'}
    $result.Data=[pscustomobject]$result.Data
    $result.Data.PSObject.TypeNames.Insert(0,'FrenchExDev.OutputPump.TaskData')
    $result.PSObject.TypeNames.Insert(0,'FrenchExDev.OutputPump.TaskResult')
    $result.PSObject.TypeNames.Insert(1,'FrenchExDev.OutputPump.Result')
    if($AsJson){ConvertTo-OutputPumpJson -InputObject $result -MaxItems $MaxItems -MaxOutputBytes $MaxOutputBytes}else{$result}
}
