function Get-OutputPumpRuntime {
    # Provider compatibility services. Commands remain bound to the runtime module.
    [CmdletBinding()]
    param()
    $commands=@{}
    foreach($name in @('Invoke-PumpProcess','Complete-PumpProcess','New-PumpResult','Protect-PumpValue','Initialize-PumpCapture','Get-PumpHelp')){$commands[$name]=Get-Command $name -CommandType Function -ErrorAction Stop}
    [pscustomobject]@{PSTypeName='FrenchExDev.OutputPump.RuntimeServices';Version=1;Module=$ExecutionContext.SessionState.Module;Commands=$commands}
}
