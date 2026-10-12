<#
.SYNOPSIS
Initialize shared session state and the pump alias.
.DESCRIPTION
Bind the engine to the session singleton without activating providers or launching native commands.
#>


$script:PumpLibraryRoot = if(Get-Variable DevPoShProjectRoot -Scope Script -ErrorAction SilentlyContinue){$script:DevPoShProjectRoot}else{Split-Path (Split-Path $PSScriptRoot)}
$script:PumpCaptureSource=$null
$script:IsOutputPumpCompilation=$false
$script:CompiledPumpRegistry=@()
$script:LoadedPumpModules=@{}
$existing=Get-Variable FrenchExDevOutputPumpSession -Scope Global -ErrorAction SilentlyContinue
if(!$existing) {
    $global:FrenchExDevOutputPumpSession=[pscustomobject]@{
        Version=2; Initialized=$false; ConfigurationPath=$null; Configuration=$null
        Registry=@{}; Providers=@{}; Transaction=$null; LastError=$null
    }
}
$script:OutputPumpSession=$global:FrenchExDevOutputPumpSession
if($script:OutputPumpSession.Version -ne 2){throw 'Unsupported OutputPumps session registry.'}
Set-Alias -Name pump -Value Pump-Me -Scope Script
