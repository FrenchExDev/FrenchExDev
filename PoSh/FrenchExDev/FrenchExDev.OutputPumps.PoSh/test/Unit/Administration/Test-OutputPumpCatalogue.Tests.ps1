<#
.SYNOPSIS
Unit tests for Test-OutputPumpCatalogue.
.DESCRIPTION
Exercise the named command or contract with isolated fixtures and mocks; keep process execution under test control.
#>


BeforeAll {
    . (Join-Path $PSScriptRoot '../TestSupport.ps1')
    Import-Module (Join-Path $script:Library 'OutputPump.PoSh.psd1') -Force -DisableNameChecking
    function New-SiblingTestPumpLibrary([string]$Path){
        $descriptor=New-TestPumpLibrary $Path
        $providerRoot=Join-Path (Split-Path $Path) 'FrenchExDev.OPumper.Fixture.PoSh'
        [void][IO.Directory]::CreateDirectory($providerRoot)
        Copy-Item -LiteralPath (Join-Path $Path 'Modules/Fixture.psm1') -Destination (Join-Path $providerRoot 'Fixture-OPumper.psm1')
        [IO.File]::WriteAllText((Join-Path $providerRoot 'Fixture-OPumper.psd1'),"@{RootModule='Fixture-OPumper.psm1';ModuleVersion='1.0.0';PrivateData=@{OutputPumps=@{Version=1;ProviderId='fixture';RegistrationScript='src/Setup-OutputPumps.ps1'}}}")
        $descriptor.Module='../FrenchExDev.OPumper.Fixture.PoSh/Fixture-OPumper.psm1'
        return $descriptor
    }
}
Describe 'Test-OutputPumpCatalogue' {
    It 'accepts coherent registration and detects a stale identity' {
        $root=Join-Path $TestDrive 'catalogue';$descriptor=New-TestPumpLibrary $root
        Register-OutputPump $descriptor -LibraryRoot $root | Out-Null
        (Test-OutputPumpCatalogue -LibraryRoot $root).Outcome | Should -Be Success
        [IO.File]::WriteAllText((Join-Path $root 'software/fixture.md'),'stale')
        (Test-OutputPumpCatalogue -LibraryRoot $root).Outcome | Should -Be Failed
    }
    It 'accepts a sibling provider with a matching manifest' {
        $root=Join-Path $TestDrive 'sibling/catalogue'
        $descriptor=New-SiblingTestPumpLibrary $root
        (Register-OutputPump $descriptor -LibraryRoot $root).Outcome | Should -Be Success
        (Test-OutputPumpCatalogue -LibraryRoot $root).Outcome | Should -Be Success
    }
    It 'rejects a sibling provider whose identity differs from its descriptor' {
        $root=Join-Path $TestDrive 'mismatch/catalogue'
        $descriptor=New-SiblingTestPumpLibrary $root
        $descriptor.Id='another-provider'
        (Register-OutputPump $descriptor -LibraryRoot $root).Outcome | Should -Be Failed
        Test-Path -LiteralPath (Join-Path $root 'catalogue.json') | Should -BeFalse
    }
    It 'rejects arbitrary paths outside the catalogue root' {
        $root=Join-Path $TestDrive 'outside/catalogue'
        $descriptor=New-SiblingTestPumpLibrary $root
        $descriptor.Module='../FrenchExDev.OPumper.Fixture.PoSh/../FrenchExDev.OPumper.Fixture.PoSh/Fixture-OPumper.psm1'
        (Register-OutputPump $descriptor -LibraryRoot $root).Outcome | Should -Be Failed
    }
}
