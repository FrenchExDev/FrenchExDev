<#
.SYNOPSIS
Compatibility loader for OutputPumps.
.DESCRIPTION
Delegate to the maintained engine or independent provider while preserving the historical import entry point.
#>


#requires -Version 7.4
#requires -PSEdition Core
# Compatibility entry point. Implementations remain in OutputPump.PoSh.
$script:OutputPumpCompatibilityTarget=Import-Module (Join-Path $PSScriptRoot 'OutputPump.PoSh.psd1') -PassThru -Global -DisableNameChecking -ErrorAction Stop
foreach($name in $script:OutputPumpCompatibilityTarget.ExportedFunctions.Keys){
    $command=$script:OutputPumpCompatibilityTarget.ExportedFunctions[$name]
    $parameters=$command.ScriptBlock.Ast.Find({param($node) $node -is [Management.Automation.Language.ParamBlockAst]},$true)
    $body=$parameters.Extent.Text+"`n"+'process { & $script:OutputPumpCompatibilityTarget.ExportedFunctions['''+$name+'''] @PSBoundParameters }'
    Set-Item -LiteralPath "Function:script:$name" -Value ($ExecutionContext.SessionState.Module.NewBoundScriptBlock([scriptblock]::Create($body)))
}
Set-Alias -Name pump -Value Pump-Me -Scope Script
Export-ModuleMember -Function @($script:OutputPumpCompatibilityTarget.ExportedFunctions.Keys) -Alias pump
