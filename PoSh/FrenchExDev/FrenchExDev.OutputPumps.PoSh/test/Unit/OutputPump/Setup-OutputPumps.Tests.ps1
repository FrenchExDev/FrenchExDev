BeforeAll { . (Join-Path $PSScriptRoot '../TestSupport.ps1');Import-Module (Join-Path $script:Library 'OutputPump.PoSh.psd1') -Force -DisableNameChecking }
Describe 'Setup-OutputPumps' {
    BeforeEach {Reset-TestOutputPumpSession; $root=Join-Path $TestDrive ([guid]::NewGuid().ToString('N'));[void][IO.Directory]::CreateDirectory($root);$provider=New-TestOPumper $root;$config=New-TestConfiguration $root $root;$inventory=Join-Path $root 'registry.json'}
    It 'loads an absent provider and captures its private function' {
        (Setup-OutputPumps -ConfigurationPath $config -InventoryPath $inventory).Outcome | Should -Be Success
        (Pump-Me fixture).Data.Branch | Should -Be main
    }
    It 'reuses an already loaded module and is idempotent' {
        $module=Import-Module (Join-Path $provider 'Fixture-OPumper.psd1') -PassThru -DisableNameChecking
        $first=Setup-OutputPumps -ConfigurationPath $config -InventoryPath $inventory
        $first.Data.Items[0].Reused | Should -BeTrue
        $second=Setup-OutputPumps -ConfigurationPath $config -InventoryPath $inventory
        @($second.Data.Items | Where-Object Changed).Count | Should -Be 0
        [object]::ReferenceEquals($module,$global:FrenchExDevOutputPumpSession.Providers.fixture.Module) | Should -BeTrue
    }
    It 'rolls back partial registrations and prevents raw fallback' {
        [IO.File]::AppendAllText((Join-Path $provider 'src/Setup-OutputPumps.ps1'),"`nthrow 'fixture failure'")
        (Setup-OutputPumps -ConfigurationPath $config -InventoryPath $inventory).Outcome | Should -Be Failed
        $global:FrenchExDevOutputPumpSession.Registry.Count | Should -Be 0
        (Pump-Me fixture).Outcome | Should -Be Failed
    }
    It 'disables providers without unloading an existing module' {
        $module=Import-Module (Join-Path $provider 'Fixture-OPumper.psd1') -PassThru -DisableNameChecking
        [IO.File]::AppendAllText($config,"`nproviders:`n  fixture:`n    enabled: false`n")
        (Setup-OutputPumps -ConfigurationPath $config -InventoryPath $inventory).Data.Providers | Should -Be 0
        @(Get-Module -All | Where-Object Path -eq $module.Path).Count | Should -BeGreaterThan 0
    }
    It 'passes options as data and filters disabled pump ids' {
        [IO.File]::AppendAllText($config,"`nproviders:`n  fixture:`n    options:`n      flavor: custom`n")
        [IO.File]::AppendAllText((Join-Path $provider 'src/Setup-OutputPumps.ps1'),"`nif(`$Context.Options.flavor -ne 'custom'){throw 'missing options'}")
        [IO.File]::AppendAllText($config,"`npumps:`n  fixture.run: false`n")
        # Replace the earlier empty pumps mapping rather than create a duplicate YAML key.
        [IO.File]::WriteAllText($config,[IO.File]::ReadAllText($config).Replace('pumps: {}',''))
        $r=Setup-OutputPumps -ConfigurationPath $config -InventoryPath $inventory
        $r.Outcome | Should -Be Success
        $r.Data.Pumps | Should -Be 0
    }
}
