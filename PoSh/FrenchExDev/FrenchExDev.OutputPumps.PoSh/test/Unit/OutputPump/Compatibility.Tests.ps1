<#
.SYNOPSIS
Unit tests for Compatibility.
.DESCRIPTION
Exercise the named command or contract with isolated fixtures and mocks; keep process execution under test control.
#>


BeforeAll {. (Join-Path $PSScriptRoot '../TestSupport.ps1');Import-Module (Join-Path $script:Library 'OutputPump.PoSh.psd1') -DisableNameChecking}
Describe 'OutputPumps compatibility import' {
    It 'delegates every pipeline input to the main engine without duplicating implementations' {
        Reset-TestOutputPumpSession
        $global:FrenchExDevOutputPumpSession.Initialized=$true
        $global:FrenchExDevOutputPumpSession.Configuration=[pscustomobject]@{Pumps=@{}}
        Register-OutputPump -Id fixture.transform -TestInput {param($x) $true} -Transform {param($x) New-OutputPumpResult -Summary transformed -Data @{Value=$x.Value}} | Out-Null
        $compatibility=Import-Module (Join-Path $script:Library OutputPumps.psd1) -PassThru -DisableNameChecking
        try {
            $inputValues=@([pscustomobject]@{Value='one'},[pscustomobject]@{Value='two'})
            $results=@($inputValues | & $compatibility.ExportedFunctions['Pump-Me'] -PumpId fixture.transform)
            $results.Count | Should -Be 2
            $results.Data.Value | Should -Be @('one','two')
        }finally{Remove-Module $compatibility;Import-Module (Join-Path $script:Library 'OutputPump.PoSh.psd1') -Force -DisableNameChecking}
    }
}
