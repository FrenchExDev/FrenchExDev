BeforeAll {
    . (Join-Path $PSScriptRoot '../TestSupport.ps1')
    Import-Module (Join-Path $script:Library 'OutputPump.PoSh.psd1') -DisableNameChecking
}
Describe 'Pnpm-OPumper setup contract' {
    It 'imports without registering routes or executing native processes' {
        Reset-TestOutputPumpSession
        $provider=Import-Module (Join-Path $script:ProviderRoot 'Pnpm-OPumper.psd1') -PassThru -DisableNameChecking
        $provider.PrivateData.OutputPumps.ProviderId | Should -Be 'pnpm'
        $global:FrenchExDevOutputPumpSession.Registry.Count | Should -Be 0
        $provider.RequiredModules.Name | Should -Not -Contain 'DevPoSh.PoSh'
        Test-Path (Join-Path $provider.ModuleBase $provider.PrivateData.OutputPumps.RegistrationScript) | Should -BeTrue
    }
}