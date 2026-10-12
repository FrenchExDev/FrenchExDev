<#
.SYNOPSIS
Unit tests for Get-OutputPumpRuntime.
.DESCRIPTION
Exercise the named command or contract with isolated fixtures and mocks; keep process execution under test control.
#>


BeforeAll {. (Join-Path $PSScriptRoot '../TestSupport.ps1');Import-Module (Join-Path $script:Library 'OutputPump.PoSh.psd1') -Force -DisableNameChecking}
Describe 'Get-OutputPumpRuntime' { It 'provides module-bound compatibility services without executing a process' {$sdk=Get-OutputPumpRuntime;$sdk.Version | Should -Be 1;$sdk.Module.Name | Should -Be OutputPump.PoSh;$sdk.Commands.Keys | Should -Contain Invoke-PumpProcess} }
