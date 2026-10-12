<#
.SYNOPSIS
Unit tests for ConvertTo-OutputPumpJson.
.DESCRIPTION
Exercise the named command or contract with isolated fixtures and mocks; keep process execution under test control.
#>


BeforeAll {. (Join-Path $PSScriptRoot '../TestSupport.ps1');Import-Module (Join-Path $script:Library 'OutputPump.PoSh.psd1') -DisableNameChecking -Force;Import-Module (Join-Path $script:Library 'Core.psm1')}
Describe 'ConvertTo-OutputPumpJson' {
    It 'reapplies redaction and the byte budget after arbitrary structured projection' {
        $r=New-PumpResult -Summary test
        $r.Data=@{Items=@(1..20|ForEach-Object {@{Text=('é'*500);Password='example-secret'}})}
        $json=$r | ConvertTo-OutputPumpJson -MaxOutputBytes 1024
        [Text.Encoding]::UTF8.GetByteCount($json) | Should -BeLessOrEqual 1024
        $json | Should -Not -Match 'example-secret'
        ($json|ConvertFrom-Json).Truncation.Applied | Should -BeTrue
    }
    It 'rejects raw passthrough input' {('native output'|ConvertTo-OutputPumpJson|ConvertFrom-Json).Error.Code | Should -Be InvalidEnvelope}
}
