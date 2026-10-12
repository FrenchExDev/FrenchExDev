<#
.SYNOPSIS
Unit tests for New-DevPoSh.
.DESCRIPTION
Exercise the named command or contract with isolated fixtures and mocks; keep process execution under test control.
#>


BeforeAll {
    . (Join-Path $PSScriptRoot '../TestSupport.ps1'); Import-Module (Join-Path $script:DevLibrary 'DevPoSh.PoSh.psd1') -Force -DisableNameChecking
    InModuleScope 'DevPoSh.PoSh' { function script:New-Git {
    <#
    .SYNOPSIS
    Provide an offline replacement for the legacy Git scaffolding command.
    .DESCRIPTION
    Keep the DevPoSh unit test independent of external Git execution.
    #>
    } }
}
Describe 'New-DevPoSh' {
    BeforeEach { Mock New-Git -ModuleName 'DevPoSh.PoSh' {} }
    It 'creates the project topology and rejects an existing project' {
        New-DevPoSh -Acme Fixture -Name Example -Author 'Test Author' -Copyright 'Test' -WorkingDirectory $TestDrive | Out-Null
        $root=Join-Path $TestDrive 'Fixture/Example.PoSh'
        foreach($name in @('Public','Private','Classes','Completers','Variables')){Test-Path (Join-Path $root "src/$name") | Should -BeTrue}
        Test-Path (Join-Path $root 'Example.PoSh.psd1') | Should -BeTrue
        {New-DevPoSh -Acme Fixture -Name Example -WorkingDirectory $TestDrive} | Should -Throw
        Should -Invoke New-Git -ModuleName 'DevPoSh.PoSh' -Times 1 -Exactly
    }
}
