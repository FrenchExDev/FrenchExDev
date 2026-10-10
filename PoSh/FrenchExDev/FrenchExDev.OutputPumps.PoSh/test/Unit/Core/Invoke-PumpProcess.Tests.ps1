BeforeAll {. (Join-Path $PSScriptRoot '../TestSupport.ps1');Import-Module (Join-Path $script:Library 'Core.psm1') -Force}
Describe 'Invoke-PumpProcess internal capture' {
    It 'turns command resolution failure into an internal failure without starting a process' {
        InModuleScope Core {
            Mock Initialize-PumpCapture {}
            Mock Get-Command {throw 'missing executable'}
            $r=Invoke-PumpProcess -Executable fake
            $r.StartError | Should -Not -BeNullOrEmpty
            $r.Stdout | Should -BeNullOrEmpty
            $r.Stderr | Should -BeNullOrEmpty
        }
    }
}
