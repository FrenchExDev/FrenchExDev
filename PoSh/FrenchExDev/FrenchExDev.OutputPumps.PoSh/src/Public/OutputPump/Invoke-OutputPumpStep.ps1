function Invoke-OutputPumpStep {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Name,[Parameter(Mandatory)][scriptblock]$ScriptBlock)
    $state=Get-OutputPumpTaskState
    if(!$state){return New-OutputPumpResult -Summary 'Invoke-OutputPumpStep requires an active task.' -Outcome Failed -RetryAdvice CorrectRequest -ErrorInfo @{Code='NoActiveTask'}}
    $result=$null;$count=0
    try {
        & $ScriptBlock *>&1 | ForEach-Object {if(++$count -eq 1){$result=$_}}
        if($count -ne 1){throw 'A step must return exactly one envelope.'}
        foreach($field in @('Summary','Outcome','Data','ExitCode','Error','RetryAdvice','Truncation')){if(!$result.PSObject.Properties[$field]){throw 'Invalid step envelope.'}}
        if($result.Outcome -notin @('Success','Failed','Timeout','Unsupported')){throw 'Invalid step outcome.'}
    }catch{$result=New-OutputPumpResult -Summary 'Task step failed.' -Outcome Failed -RetryAdvice CorrectRequest -ErrorInfo @{Code='TaskStepFailed'} -MaxOutputBytes $state.Budget}
    Add-OutputPumpTaskResult -State $state -Result $result -Name $Name
    $result
}
