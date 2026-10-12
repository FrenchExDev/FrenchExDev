<#
.SYNOPSIS
Unit tests for Import-DevPoShModule.
.DESCRIPTION
Exercise the named command or contract with isolated fixtures and mocks; keep process execution under test control.
#>


BeforeAll { . (Join-Path $PSScriptRoot '../TestSupport.ps1'); Import-Module (Join-Path $script:DevLibrary 'DevPoSh.PoSh.psd1') -Force -DisableNameChecking }
Describe 'Import-DevPoShModule' {
    BeforeEach {
        Mock Get-DevPoShProjects -ModuleName 'DevPoSh.PoSh' {[pscustomobject]@{Name='Example.PoSh';Acme='Fixture';Path='C:/fixture/Example.PoSh';Manifest='C:/fixture/Example.PoSh/Example.PoSh.psd1';Data=@{RootModule='Example.PoSh.psm1'}}}
        Mock Get-Module -ModuleName 'DevPoSh.PoSh' {@()}
        Mock Import-Module -ModuleName 'DevPoSh.PoSh' {}
    }
    It 'imports selected sources globally without compilation' {
        Import-DevPoShModule -Path $TestDrive -Acme Fixture
        Should -Invoke Import-Module -ModuleName 'DevPoSh.PoSh' -Times 1 -Exactly -ParameterFilter {$Global -and !$Force}
    }
    It 'skips an already loaded primary module' {
        Mock Get-Module -ModuleName 'DevPoSh.PoSh' {[pscustomobject]@{Path=[IO.Path]::GetFullPath('C:/fixture/Example.PoSh/Example.PoSh.psm1')}}
        Import-DevPoShModule -Path $TestDrive
        Should -Invoke Import-Module -ModuleName 'DevPoSh.PoSh' -Times 0
    }
    It 'continues after a failed module without retrying it' {
        Mock Import-Module -ModuleName 'DevPoSh.PoSh' {throw 'unavailable'}
        Import-DevPoShModule -Path $TestDrive -WarningAction SilentlyContinue
        Should -Invoke Import-Module -ModuleName 'DevPoSh.PoSh' -Times 1 -Exactly
    }
}
