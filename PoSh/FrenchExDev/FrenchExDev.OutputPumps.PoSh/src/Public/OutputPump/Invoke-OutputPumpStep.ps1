<#
.SYNOPSIS
Run and account for exactly one pump result inside a task.
.DESCRIPTION
Define Invoke-OutputPumpStep for the OutputPumps runtime. Record failures even when the result is assigned locally and reject callback output that is not a single envelope.
#>


function Invoke-OutputPumpStep {
    <#
    .SYNOPSIS
    Run and account for exactly one pump result inside a task.
    .DESCRIPTION
    Record failures even when the result is assigned locally and reject callback output that is not a single envelope.
    #>
    
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Name,[Parameter(Mandatory)][scriptblock]$ScriptBlock)
    $timer=[Diagnostics.Stopwatch]::StartNew()
    try {
    $state=Get-OutputPumpTaskState
    if(!$state){return New-OutputPumpResult -Summary 'Invoke-OutputPumpStep requires an active task.' -Outcome Failed -RetryAdvice CorrectRequest -ErrorInfo @{Code='NoActiveTask'}}
    $result=$null;$count=0
    try {
        & $ScriptBlock 2>&1 3>&1 4>&1 6>&1 | ForEach-Object {if(++$count -eq 1){$result=$_}}
        if($count -ne 1){throw 'A step must return exactly one envelope.'}
        foreach($field in @('Summary','Outcome','Data','ExitCode','Error','RetryAdvice','Truncation')){if(!$result.PSObject.Properties[$field]){throw 'Invalid step envelope.'}}
        if($result.Outcome -notin @('Success','Failed','Timeout','Unsupported')){throw 'Invalid step outcome.'}
    }catch{$result=New-OutputPumpResult -Summary 'Task step failed.' -Outcome Failed -RetryAdvice CorrectRequest -ErrorInfo @{Code='TaskStepFailed'} -MaxOutputBytes $state.Budget}
    Add-OutputPumpTaskResult -State $state -Result $result -Name $Name
    $result
    }finally{Write-OutputPumpTiming 'Task.Step' $timer.Elapsed.TotalMilliseconds}
}
