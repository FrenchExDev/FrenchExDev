<#
.SYNOPSIS
Unit tests for Invoke-OutputPumpFunction.
.DESCRIPTION
Exercise the named command or contract with isolated fixtures and mocks; keep process execution under test control.
#>


BeforeAll {. (Join-Path $PSScriptRoot '../TestSupport.ps1');Import-Module (Join-Path $script:Library 'OutputPump.PoSh.psd1') -Force -DisableNameChecking}
Describe 'Invoke-OutputPumpFunction' {
    It 'uses the same business type identifier in source mode as in the native route and compilation' {
        $r=Get-PumpDirectory -Path $TestDrive
        $r.Outcome | Should -Be Success
        $r.Data.PSTypeNames[0] | Should -Be 'FrenchExDev.OutputPump.powershell.directory'
    }
}
