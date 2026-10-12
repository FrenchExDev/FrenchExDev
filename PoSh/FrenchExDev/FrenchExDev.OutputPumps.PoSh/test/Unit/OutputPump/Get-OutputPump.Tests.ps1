<#
.SYNOPSIS
Unit tests for Get-OutputPump.
.DESCRIPTION
Exercise the named command or contract with isolated fixtures and mocks; keep process execution under test control.
#>


BeforeAll {. (Join-Path $PSScriptRoot '../TestSupport.ps1');Import-Module (Join-Path $script:Library 'OutputPump.PoSh.psd1') -Force -DisableNameChecking}
Describe 'Get-OutputPump' {
    It 'exposes routing arguments without treating them as authentication tokens' {
        Reset-TestOutputPumpSession
        $global:FrenchExDevOutputPumpSession.Initialized=$true
        $global:FrenchExDevOutputPumpSession.Configuration=[pscustomobject]@{Pumps=@{}}
        Register-OutputPump -Id fixture.status -Route @{Executable='fixture';Tokens=@('status');Options=@{};Positional=$null;Multiple=$false} -Handler {} | Out-Null
        $r=Get-OutputPump -Id fixture.status
        $r.Data.Items[0].ArgumentPrefix | Should -Be @('status')
        $r.Truncation.Applied | Should -BeFalse
    }
    It 'reports an uninitialized registry as a structured failure' {Reset-TestOutputPumpSession;(Get-OutputPump).Outcome | Should -Be Failed}
    It 'never exposes callback captures' {
        Reset-TestOutputPumpSession;$global:FrenchExDevOutputPumpSession.Initialized=$true;$global:FrenchExDevOutputPumpSession.Configuration=[pscustomobject]@{Pumps=@{}}
        $secret='private-capture';$handler={param($c) New-OutputPumpResult -Summary $secret}.GetNewClosure()
        Register-OutputPump -Id fixture.live -Route @{Executable='fixture';Tokens=@();Options=@{};Positional=$null;Multiple=$false} -Handler $handler | Out-Null
        $r=Get-OutputPump
        $r.Data.Items[0].Id | Should -Be fixture.live
        ($r | ConvertTo-Json -Depth 10) | Should -Not -Match 'private-capture|ScriptBlock|Handler'
    }
}
