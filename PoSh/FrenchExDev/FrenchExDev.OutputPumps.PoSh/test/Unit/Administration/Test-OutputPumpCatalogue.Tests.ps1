BeforeAll { . (Join-Path $PSScriptRoot '../TestSupport.ps1'); Import-Module (Join-Path $script:Library 'OutputPump.PoSh.psd1') -Force -DisableNameChecking }
Describe 'Test-OutputPumpCatalogue' {
    It 'accepts coherent registration and detects a stale identity' {
        $root=Join-Path $TestDrive 'catalogue';$descriptor=New-TestPumpLibrary $root
        Register-OutputPump $descriptor -LibraryRoot $root | Out-Null
        (Test-OutputPumpCatalogue -LibraryRoot $root).Outcome | Should -Be Success
        [IO.File]::WriteAllText((Join-Path $root 'software/fixture.md'),'stale')
        (Test-OutputPumpCatalogue -LibraryRoot $root).Outcome | Should -Be Failed
    }
}
