BeforeAll {. (Join-Path $PSScriptRoot '../TestSupport.ps1');Import-Module (Join-Path $script:Library 'OutputPump.PoSh.psd1') -Force -DisableNameChecking}
Describe 'Import-OutputPumpSoftware compatibility loader' {
    It 'reuses the module at the exact selected path' {
        InModuleScope OutputPump.PoSh {
            $module=[pscustomobject]@{Path=[IO.Path]::GetFullPath('selected.psm1')}
            Mock Get-Module {$module}
            Mock Import-Module {throw 'Unexpected reimport'}
            $entry=[pscustomobject]@{Root=(Get-Location).Path;Module='selected.psm1'}
            (Import-OutputPumpSoftware $entry).Path | Should -Be $module.Path
            Should -Invoke Import-Module -Times 0
        }
    }
}
