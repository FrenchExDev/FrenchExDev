BeforeAll {. (Join-Path $PSScriptRoot '../TestSupport.ps1');Import-Module (Join-Path $script:Library 'OutputPump.PoSh.psd1') -DisableNameChecking}
Describe 'Get-OutputPumpProviders validation' {
    BeforeEach {Reset-TestOutputPumpSession}
    It 'rejects a registration entry point outside src without execution' {
        $root=Join-Path $TestDrive outside;$provider=New-TestOPumper $root
        $manifest=Join-Path $provider Fixture-OPumper.psd1
        [IO.File]::WriteAllText($manifest,([IO.File]::ReadAllText($manifest).Replace('src/Setup-OutputPumps.ps1','../Setup-OutputPumps.ps1')))
        $configuration=Get-ConfiguredOutputPumps -Path (New-TestConfiguration $TestDrive $root)
        InModuleScope OutputPump.PoSh -Parameters @{Configuration=$configuration} {{Get-OutputPumpProviders $Configuration} | Should -Throw}
    }
    It 'ignores unrelated loaded module origins even if names match' {
        InModuleScope OutputPump.PoSh {
            $configuration=[pscustomobject]@{Roots=@();Providers=@{}}
            Mock Get-Module {[pscustomobject]@{PrivateData=@{OutputPumps=@{ProviderId='fixture';RegistrationScript='src/Setup-OutputPumps.ps1'}};ModuleBase='C:/unrelated';Guid=[guid]::NewGuid();Name='Fixture-OPumper'}}
            @(Get-OutputPumpProviders $configuration).Count | Should -Be 0
        }
    }
    It 'refuses multiple loaded compatible instances' {
        $root=Join-Path $TestDrive ambiguous;$provider=New-TestOPumper $root
        $manifest=Import-PowerShellDataFile (Join-Path $provider Fixture-OPumper.psd1)
        $configuration=Get-ConfiguredOutputPumps -Path (New-TestConfiguration $TestDrive $root)
        InModuleScope OutputPump.PoSh -Parameters @{Configuration=$configuration;Root=$provider;Guid=$manifest.GUID} {
            $script:FakeProviderInstances=@(
                [pscustomobject]@{PrivateData=@{OutputPumps=@{ProviderId='fixture'}};ModuleBase=$Root;Guid=$Guid;Name='Fixture-OPumper'}
                [pscustomobject]@{PrivateData=@{OutputPumps=@{ProviderId='fixture'}};ModuleBase=$Root;Guid=$Guid;Name='Fixture-OPumper'}
            )
            Mock Get-Module {$script:FakeProviderInstances}
            {Get-OutputPumpProviders $Configuration} | Should -Throw '*Multiple compatible*'
        }
    }
}
