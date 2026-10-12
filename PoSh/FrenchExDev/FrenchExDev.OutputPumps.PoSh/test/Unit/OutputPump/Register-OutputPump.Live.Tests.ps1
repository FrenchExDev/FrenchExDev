<#
.SYNOPSIS
Unit tests for Register-OutputPump.Live.
.DESCRIPTION
Exercise the named command or contract with isolated fixtures and mocks; keep process execution under test control.
#>


BeforeAll {. (Join-Path $PSScriptRoot '../TestSupport.ps1');Import-Module (Join-Path $script:Library 'OutputPump.PoSh.psd1') -DisableNameChecking}
Describe 'Register-OutputPump live routes and transformations' {
    BeforeEach {
        Reset-TestOutputPumpSession
        $config=Join-Path $TestDrive empty.yaml;[IO.File]::WriteAllText($config,"version: 2`nroots: []`nproviders: {}`npumps: {}")
        Setup-OutputPumps -ConfigurationPath $config -InventoryPath (Join-Path $TestDrive registry.json) | Out-Null
        $route=@{Executable='fixture';Tokens=@('read');Options=@{};Positional='ArgumentList';Multiple=$true}
    }
    It 'captures a live variable and passes argument boundaries without executing a shell' {
        $prefix='captured'
        $handler={param($c) New-OutputPumpResult -Summary $prefix -Data @{Items=$c.Parameters.ArgumentList}}.GetNewClosure()
        Register-OutputPump -Id fixture.read -Route $route -Handler $handler | Out-Null
        $r=Pump-Me fixture -NativeArguments @('read','space and été','literal;$(text)')
        $r.Outcome | Should -Be Success;$r.Summary | Should -Be captured
        $r.Data.Items | Should -Be @('space and été','literal;$(text)')
    }
    It 'refuses ID and route collisions without explicit replacement' {
        Register-OutputPump -Id fixture.read -Route $route -Handler {param($c) New-OutputPumpResult -Summary fixture} | Out-Null
        {Register-OutputPump -Id fixture.read -Route $route -Handler {}} | Should -Throw
        {Register-OutputPump -Id other.read -Route $route -Handler {}} | Should -Throw
        {Register-OutputPump -Id fixture.read -Route @{Executable='different';Tokens=@();Options=@{};Positional=$null;Multiple=$false} -Replace -Handler {}} | Should -Throw
    }
    It 'captures callback diagnostic streams instead of leaking them' {
        Register-OutputPump -Id fixture.read -Route $route -Handler {param($c) Write-Warning 'private diagnostic';Write-Host 'private host';New-OutputPumpResult -Summary fixture} | Out-Null
        $values=@(Pump-Me fixture read *>&1)
        $values.Count | Should -Be 1;$values[0].Outcome | Should -Be Failed
        ($values|ConvertTo-Json -Depth 16) | Should -Not -Match 'private diagnostic|private host'
    }
    It 'keeps explicit transformations single-pass and passes unknown objects unchanged' {
        $suffix=':mapped'
        Register-OutputPump -Id fixture.transform -TestInput {param($x) $x.Kind -eq 'fixture'} -Transform {param($x) New-OutputPumpResult -Summary transformed -Data @{Value=($x.Value+$suffix)}}.GetNewClosure() | Out-Null
        $unknown=[pscustomobject]@{Kind='unknown'}
        [object]::ReferenceEquals($unknown,($unknown | Pump-Me -PumpId fixture.transform)) | Should -BeTrue
        ([pscustomobject]@{Kind='fixture';Value='one'} | Pump-Me -PumpId fixture.transform).Data.Value | Should -Be 'one:mapped'
    }
    It 'shares registrations across an engine reimport without rewriting files' {
        Register-OutputPump -Id fixture.read -Route $route -Handler {param($c) New-OutputPumpResult -Summary fixture} | Out-Null
        Import-Module (Join-Path $script:Library 'OutputPump.PoSh.psd1') -Force -DisableNameChecking
        (Get-OutputPump -Id fixture.read).Data.Items.Count | Should -Be 1
    }
}
