# Test-only helpers. Providers depend on the engine, never on DevPoSh.
$script:ProviderRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$engine=Get-Module -All -Name OutputPump.PoSh | Select-Object -First 1
if(!$engine){
    $manifest=Join-Path (Split-Path $script:ProviderRoot) 'FrenchExDev.OutputPumps.PoSh/OutputPump.PoSh.psd1'
    $engine=if(Test-Path $manifest){Import-Module $manifest -PassThru -Global -DisableNameChecking}else{Import-Module OutputPump.PoSh -PassThru -Global -DisableNameChecking -ErrorAction Stop}
}
$script:Library=$engine.ModuleBase
$script:Cases=[IO.File]::ReadAllText((Join-Path $script:ProviderRoot 'test/Fixtures/native.json'))|ConvertFrom-Json -AsHashtable
function New-TestCapture([string]$Text='',[int]$ExitCode=0){
    [pscustomobject]@{Stdout=$Text;Stderr='';StartError=$null;ExitCode=$ExitCode;TimedOut=$false;Truncated=$false;StdoutCharacters=$Text.Length;StderrCharacters=0}
}
function Reset-TestOutputPumpSession {
    $session=$global:FrenchExDevOutputPumpSession
    $session.Registry=@{};$session.Providers=@{};$session.Initialized=$false;$session.LastError=$null;$session.Transaction=$null;$session.Configuration=$null;$session.ConfigurationPath=$null
}