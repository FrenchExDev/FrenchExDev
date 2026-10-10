BeforeAll { . (Join-Path $PSScriptRoot '../TestSupport.ps1'); Import-Module (Join-Path $script:DevLibrary 'DevPoSh.PoSh.psd1') -Force -DisableNameChecking }
Describe 'Get-PowershellReleaseUrl' {
    It 'preserves the requested version and platform without network access' {
        Get-PowershellReleaseUrl -PowerShellVersion 7.4.6 -Flavor linux-arm64 | Should -Be 'https://github.com/PowerShell/PowerShell/releases/download/v7.4.6/powershell-7.4.6-linux-arm64.tar.gz'
    }
}
