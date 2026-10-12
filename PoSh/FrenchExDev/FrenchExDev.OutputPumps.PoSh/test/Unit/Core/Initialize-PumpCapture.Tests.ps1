<#
.SYNOPSIS
Unit tests for Initialize-PumpCapture.
.DESCRIPTION
Exercise the named command or contract with isolated fixtures and mocks; keep process execution under test control.
#>


BeforeAll {. (Join-Path $PSScriptRoot '../TestSupport.ps1');Import-Module (Join-Path $script:Library 'Core.psm1') -Force}
Describe 'Initialize-PumpCapture' {
    It 'loads the bounded reader once and can be called repeatedly' {
        Initialize-PumpCapture
        Initialize-PumpCapture
        ('FrenchExDev.OutputPumps.BoundedProcess' -as [type]) | Should -Not -BeNullOrEmpty
    }
}
