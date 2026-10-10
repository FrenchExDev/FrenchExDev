BeforeAll {. (Join-Path $PSScriptRoot '../TestSupport.ps1');Import-Module (Join-Path $script:Library 'OutputPump.PoSh.psd1') -Force -DisableNameChecking}
Describe 'Unregister-OutputPump' {
    It 'restores an overridden callback and preserves it through repeated setup' {
        Reset-TestOutputPumpSession;$root=Join-Path $TestDrive 'restore';[void][IO.Directory]::CreateDirectory($root);New-TestOPumper $root | Out-Null;$config=New-TestConfiguration $root $root;$inventory=Join-Path $root 'registry.json'
        (Setup-OutputPumps -ConfigurationPath $config -InventoryPath $inventory).Outcome | Should -Be Success
        $route=@{Executable='fixture';Tokens=@();Options=@{};Positional='ArgumentList';Multiple=$true}
        Register-OutputPump -Id fixture.run -Route $route -Handler {param($c) New-OutputPumpResult -Summary live -Data @{Branch='override'}} -Replace | Out-Null
        (Pump-Me fixture).Data.Branch | Should -Be override
        Setup-OutputPumps -ConfigurationPath $config -InventoryPath $inventory | Out-Null
        (Pump-Me fixture).Data.Branch | Should -Be override
        (Unregister-OutputPump fixture.run).Restored | Should -BeTrue
        (Pump-Me fixture).Data.Branch | Should -Be main
    }
    It 'rejects missing registrations' {{Unregister-OutputPump absent.id}|Should -Throw}
}
