BeforeAll { . (Join-Path $PSScriptRoot '../TestSupport.ps1'); Import-Module (Join-Path $script:Library 'OutputPump.PoSh.psd1') -Force -DisableNameChecking }
Describe 'Get-OutputPumpRegistry' {
    BeforeEach { Reset-TestOutputPumpSession }
    It 'requires a successfully initialized session' {
        InModuleScope OutputPump.PoSh { { Get-OutputPumpRegistry } | Should -Throw }
    }
    It 'reads the shared registry without reading YAML or importing modules' {
        $config=Join-Path $TestDrive 'empty.yaml';[IO.File]::WriteAllText($config,"version: 2`nroots: []`nproviders: {}`npumps: {}")
        (Setup-OutputPumps -ConfigurationPath $config -InventoryPath (Join-Path $TestDrive registry.json)).Outcome | Should -Be Success
        Register-OutputPump -Id live.example -Route @{Executable='example';Tokens=@();Options=@{};Positional=$null;Multiple=$false} -Handler {param($c) New-OutputPumpResult -Summary 'Example'} | Out-Null
        InModuleScope OutputPump.PoSh {
            Mock Get-ConfiguredOutputPumps {throw 'Unexpected configuration read'}
            Mock Import-Module {throw 'Unexpected import'}
            @(Get-OutputPumpRegistry).Count | Should -Be 1
            Should -Invoke Get-ConfiguredOutputPumps -Times 0
            Should -Invoke Import-Module -Times 0
        }
    }
    It 'refuses a configuration different from the initialized session' {
        InModuleScope OutputPump.PoSh {
            $script:OutputPumpSession.Initialized=$true;$script:OutputPumpSession.ConfigurationPath='a.yaml'
            {Get-OutputPumpRegistry 'b.yaml'} | Should -Throw
        }
    }
}
