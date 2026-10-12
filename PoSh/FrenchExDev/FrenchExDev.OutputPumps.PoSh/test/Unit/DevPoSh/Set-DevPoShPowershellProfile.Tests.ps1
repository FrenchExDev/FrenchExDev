<#
.SYNOPSIS
Unit tests for Set-DevPoShPowershellProfile.
.DESCRIPTION
Exercise the named command or contract with isolated fixtures and mocks; keep process execution under test control.
#>


BeforeAll { . (Join-Path $PSScriptRoot '../TestSupport.ps1'); Import-Module (Join-Path $script:DevLibrary 'DevPoSh.PoSh.psd1') -Force -DisableNameChecking }
Describe 'Set-DevPoShPowershellProfile' {
    It 'copies exact bytes and preserves timestamps on repeated use' {
        $path=Join-Path $TestDrive 'nested/profile.ps1'
        (Set-DevPoShPowershellProfile -Path $path).Changed | Should -BeTrue
        [Convert]::ToBase64String([IO.File]::ReadAllBytes($path)) | Should -Be ([Convert]::ToBase64String([IO.File]::ReadAllBytes((Join-Path $script:DevLibrary 'resources/profile.ps1.tpl'))))
        $before=(Get-Item $path).LastWriteTimeUtc
        (Set-DevPoShPowershellProfile -Path $path).Changed | Should -BeFalse
        (Get-Item $path).LastWriteTimeUtc | Should -Be $before
    }
    It 'refuses replacement and backs up existing bytes with Force' {
        $path=Join-Path $TestDrive 'existing.ps1';[IO.File]::WriteAllText($path,'# personal profile')
        {Set-DevPoShPowershellProfile -Path $path} | Should -Throw
        $r=Set-DevPoShPowershellProfile -Path $path -Force
        [IO.File]::ReadAllText($r.Backup) | Should -Be '# personal profile'
    }
    It 'does not create directories with WhatIf' {
        $path=Join-Path $TestDrive 'whatif/profile.ps1'
        (Set-DevPoShPowershellProfile -Path $path -WhatIf).Changed | Should -BeFalse
        Test-Path (Split-Path $path) | Should -BeFalse
    }
    It 'rejects a directory and requires an explicit Path' {
        {Set-DevPoShPowershellProfile -Path $TestDrive} | Should -Throw
        (Get-Command Set-DevPoShPowershellProfile).Parameters.Path.Attributes.Mandatory | Should -Contain $true
    }
}
