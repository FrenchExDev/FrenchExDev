BeforeAll {. (Join-Path $PSScriptRoot '../TestSupport.ps1');Import-Module (Join-Path $script:Library 'Core.psm1') -Force}
Describe 'Initialize-PumpCapture' {
    It 'loads the bounded reader once and can be called repeatedly' {
        Initialize-PumpCapture
        Initialize-PumpCapture
        ('FrenchExDev.OutputPumps.BoundedProcess' -as [type]) | Should -Not -BeNullOrEmpty
    }
}
