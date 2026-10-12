BeforeAll {. (Join-Path $PSScriptRoot '../TestSupport.ps1');Import-Module (Join-Path $script:ProviderRoot 'PowerShell-OPumper.psd1') -Force}
Describe 'Get-PumpDirectory' {
    It 'filters and bounds paths with spaces and Unicode' {
        [IO.File]::WriteAllText((Join-Path $TestDrive 'é space.txt'),'a')
        [IO.File]::WriteAllText((Join-Path $TestDrive 'b.txt'),'b')
        [IO.File]::WriteAllText((Join-Path $TestDrive 'c.md'),'c')
        $r=Get-PumpDirectory -Path $TestDrive -Filter '*.txt' -MaxItems 1
        $r.Outcome | Should -Be Success
        $r.Truncation.TotalCount | Should -Be 2
        $r.Truncation.Applied | Should -BeTrue
        $r.Data.Items.Count | Should -Be 1
    }
    It 'returns a read failure for a missing directory' {(Get-PumpDirectory -Path (Join-Path $TestDrive missing)).Outcome | Should -Be Failed}
}
