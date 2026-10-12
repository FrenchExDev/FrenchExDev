<#
.SYNOPSIS
Unit tests for Get-PumpHelp.
.DESCRIPTION
Exercise the named command or contract with isolated fixtures and mocks; keep process execution under test control.
#>


BeforeAll {. (Join-Path $PSScriptRoot '../TestSupport.ps1');Import-Module (Join-Path $script:Library 'Core.psm1') -Force}
Describe 'Get-PumpHelp' {
    It 'extracts only selected help sections' {
        InModuleScope Core {
            Mock Invoke-PumpProcess {[pscustomobject]@{Stdout="irrelevant`n--useful description";Stderr='';StartError=$null;TimedOut=$false;Truncated=$false;ExitCode=0}}
            $r=Get-PumpHelp -Executable fake -Filter '^--'
            $r.Data.Items.Count | Should -Be 1
            $r.Data.Items[0].Section | Should -Be '--useful description'
            Should -Invoke Invoke-PumpProcess -Exactly -Times 1
        }
    }
}
