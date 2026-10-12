<#
.SYNOPSIS
Unit tests for New-OutputPumpCompilation.
.DESCRIPTION
Exercise the named command or contract with isolated fixtures and mocks; keep process execution under test control.
#>


BeforeAll { . (Join-Path $PSScriptRoot '../TestSupport.ps1'); Import-Module (Join-Path $script:Library 'OutputPump.PoSh.psd1') -Force -DisableNameChecking }
Describe 'New-OutputPumpCompilation inventory' {
    It 'preserves the previous inventory on invalid configuration' {
        $path=Join-Path $TestDrive registry.json;[IO.File]::WriteAllText($path,'previous valid artifact')
        (New-OutputPumpCompilation -ConfigurationPath (Join-Path $TestDrive absent.yaml) -Path $path).Outcome | Should -Be Failed
        [IO.File]::ReadAllText($path) | Should -Be 'previous valid artifact'
    }
    It 'writes deterministic metadata without executing registration and detects removal' {
        $root=Join-Path $TestDrive inventory; $provider=New-TestOPumper $root
        [IO.File]::AppendAllText((Join-Path $provider 'src/Setup-OutputPumps.ps1'),"`nthrow 'Must not execute during discovery'")
        $config=New-TestConfiguration $TestDrive $root;$path=Join-Path $TestDrive registry.json
        $first=New-OutputPumpCompilation -ConfigurationPath $config -Path $path
        $first.Outcome | Should -Be Success
        $time=(Get-Item $path).LastWriteTimeUtc
        (New-OutputPumpCompilation -ConfigurationPath $config -Path $path).Data.Items[0].Changed | Should -BeFalse
        (Get-Item $path).LastWriteTimeUtc | Should -Be $time
        $json=[IO.File]::ReadAllText($path)|ConvertFrom-Json
        $json.Version | Should -Be 2
        $json.Providers.Count | Should -Be 1
        Remove-Item -LiteralPath (Join-Path $provider 'Fixture-OPumper.psd1')
        (New-OutputPumpCompilation -ConfigurationPath $config -Path $path).Data.Items[0].Changed | Should -BeTrue
        ([IO.File]::ReadAllText($path)|ConvertFrom-Json).Providers.Count | Should -Be 0
    }
    It 'rejects ambiguous provider identities before executing scripts' {
        $root=Join-Path $TestDrive duplicate; $p=New-TestOPumper $root
        Copy-Item -LiteralPath $p -Destination (Join-Path $root duplicate) -Recurse
        $config=New-TestConfiguration $TestDrive $root
        (New-OutputPumpCompilation -ConfigurationPath $config -Path (Join-Path $TestDrive duplicate.json)).Outcome | Should -Be Failed
    }
}
