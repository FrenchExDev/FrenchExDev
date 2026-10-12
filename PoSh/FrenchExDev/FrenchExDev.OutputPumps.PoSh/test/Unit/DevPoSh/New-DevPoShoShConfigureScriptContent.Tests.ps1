<#
.SYNOPSIS
Unit tests for New-DevPoShoShConfigureScriptContent.
.DESCRIPTION
Exercise the named command or contract with isolated fixtures and mocks; keep process execution under test control.
#>


BeforeAll {
    . (Join-Path $PSScriptRoot '../TestSupport.ps1'); Import-Module (Join-Path $script:DevLibrary 'DevPoSh.PoSh.psd1') -Force -DisableNameChecking
    InModuleScope 'DevPoSh.PoSh' {function script:New-Bash {
    <#
    .SYNOPSIS
    Provide an offline replacement for the legacy Bash scaffolding command.
    .DESCRIPTION
    Keep the DevPoSh unit test independent of the external Bash environment.
    #>
    param($Parameters,$Code) & $Code}}
}
Describe 'New-DevPoShoShConfigureScriptContent' {
    It 'emits explicit copy destinations and no retired profile generation parameters' {
        $text=New-DevPoShoShConfigureScriptContent | Out-String
        $text | Should -Match 'Set-DevPoShPowershellProfile -Path \$profile'
        $text | Should -Not -Match 'MyPoShPath|Install-DevPoSh'
    }
}
