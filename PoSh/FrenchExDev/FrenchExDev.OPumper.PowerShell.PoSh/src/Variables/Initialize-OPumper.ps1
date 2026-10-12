$script:OPumperRuntime=Get-OutputPumpRuntime
foreach($serviceName in $script:OPumperRuntime.Commands.Keys) {
    $parameters=$script:OPumperRuntime.Commands[$serviceName].ScriptBlock.Ast.Find({param($node) $node -is [Management.Automation.Language.ParamBlockAst]},$true)
    $body=$(if($parameters){$parameters.Extent.Text}else{'param()'})+"`n"+'& $script:OPumperRuntime.Module {param($name,$parameters) & (Get-Command $name -CommandType Function) @parameters} '''+$serviceName+''' $PSBoundParameters'
    Set-Item -LiteralPath "Function:script:$serviceName" -Value ($ExecutionContext.SessionState.Module.NewBoundScriptBlock([scriptblock]::Create($body)))
}