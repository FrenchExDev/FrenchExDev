<#
.SYNOPSIS
Unit tests for New-DevPoshPowershellInstallCommand.
.DESCRIPTION
Exercise the named command or contract with isolated fixtures and mocks; keep process execution under test control.
#>


BeforeAll { . (Join-Path $PSScriptRoot '../TestSupport.ps1'); Import-Module (Join-Path $script:DevLibrary 'DevPoSh.PoSh.psd1') -Force -DisableNameChecking }
Describe 'New-DevPoshPowershellInstallCommand' {
    It 'returns installation text without executing its native commands' {
        $text=New-DevPoshPowershellInstallCommand -Dependencies {'apk add curl'} -PowerShellVersion 7.4.6
        $text | Should -Match '^apk add curl'
        $text | Should -Match 'powershell-7.4.6-linux-musl-x64.tar.gz'
    }
}
