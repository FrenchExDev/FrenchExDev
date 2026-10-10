#requires -Version 7.4
Import-Module (Join-Path (Split-Path $PSScriptRoot) 'OutputPump.PoSh.psd1') -Global -DisableNameChecking -ErrorAction Stop
$provider=Import-Module (Join-Path (Split-Path (Split-Path $PSScriptRoot)) 'FrenchExDev.OPumper.Pnpm.PoSh/Pnpm-OPumper.psd1') -PassThru -DisableNameChecking
$script:LegacyProvider=$provider
foreach($functionName in $provider.ExportedFunctions.Keys){
    $parameterBlock=$provider.ExportedFunctions[$functionName].ScriptBlock.Ast.Find({param($node) $node -is [Management.Automation.Language.ParamBlockAst]},$true)
    $body=$parameterBlock.Extent.Text+"`n"+'& $script:LegacyProvider.ExportedFunctions['''+$functionName+'''] @PSBoundParameters'
    Set-Item -LiteralPath ("Function:script:"+$functionName) -Value ($ExecutionContext.SessionState.Module.NewBoundScriptBlock([scriptblock]::Create($body)))
}
Export-ModuleMember -Function @($provider.ExportedFunctions.Keys)