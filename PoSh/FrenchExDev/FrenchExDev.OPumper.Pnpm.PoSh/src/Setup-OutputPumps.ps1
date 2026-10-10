#requires -Version 7.4
param([Parameter(Mandatory)][pscustomobject]$Context)
$module=$Context.Module
if(!$module){$module=Import-Module -Name $Context.ManifestPath -Global -PassThru -DisableNameChecking -ErrorAction Stop}
$Context.Module=$module
& $module {
    param($context)
    $root=if($ExecutionContext.SessionState.Module.PrivateData.OutputPumps.ContainsKey('AssetRoot')){$ExecutionContext.SessionState.Module.PrivateData.OutputPumps.AssetRoot}else{$context.ProviderRoot}
    $descriptor=@(ConvertFrom-Json -InputObject ([IO.File]::ReadAllText((Join-Path $root 'catalogue.json'))) -AsHashtable)[0]
    foreach($definition in $descriptor.Commands) {
        $command=Get-Command $definition.Function -CommandType Function -ErrorAction Stop
        $handler={param($call) $parameters=$call.Parameters; & $command @parameters}.GetNewClosure()
        & $context.Register -Id $definition.Id -Route $definition.Route -Handler $handler -Command $command -ProviderId $context.ProviderId
    }
} $Context