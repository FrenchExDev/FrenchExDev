BeforeAll {. (Join-Path $PSScriptRoot '../TestSupport.ps1');Import-Module (Join-Path $script:ProviderRoot 'PowerShell-OPumper.psd1') -Force}
Describe 'Get-PumpLog' {
    It 'keeps the latest filtered events and identifies the window' {
        $path=Join-Path $TestDrive events.log;[IO.File]::WriteAllText($path,"error first`ninfo omitted`nerror last")
        $r=Get-PumpLog -Path $path -Filter error -MaxItems 1
        $r.Data.Items[0].Text | Should -Be 'error last'
        $r.Truncation.Applied | Should -BeTrue
    }
}
