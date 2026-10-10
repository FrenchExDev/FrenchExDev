BeforeAll {
    . (Join-Path $PSScriptRoot '../TestSupport.ps1'); Import-Module (Join-Path $script:DevLibrary 'DevPoSh.PoSh.psd1') -Force -DisableNameChecking
    InModuleScope 'DevPoSh.PoSh' {function script:New-Bash {param($Parameters,$Code) & $Code}}
}
Describe 'New-DevPoShoShConfigureScriptContent' {
    It 'emits explicit copy destinations and no retired profile generation parameters' {
        $text=New-DevPoShoShConfigureScriptContent | Out-String
        $text | Should -Match 'Set-DevPoShPowershellProfile -Path \$profile'
        $text | Should -Not -Match 'MyPoShPath|Install-DevPoSh'
    }
}
