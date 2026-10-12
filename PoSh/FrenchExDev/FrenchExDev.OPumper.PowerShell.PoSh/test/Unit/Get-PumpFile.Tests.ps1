BeforeAll {. (Join-Path $PSScriptRoot '../TestSupport.ps1');Import-Module (Join-Path $script:ProviderRoot 'PowerShell-OPumper.psd1') -Force}
Describe 'Get-PumpFile' {
    It 'reads only the numbered requested window' {
        $path=Join-Path $TestDrive 'é space.txt';[IO.File]::WriteAllText($path,"first`nsecond`nthird")
        $r=Get-PumpFile -Path $path -StartLine 2 -LineCount 1
        $r.Data.Items[0].Line | Should -Be 2
        $r.Data.Items[0].Text | Should -Be second
        { [IO.File]::Open($path,[IO.FileMode]::Open,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None).Dispose() } | Should -Not -Throw
    }
    It 'signals giant lines and masks secrets' {
        $path=Join-Path $TestDrive 'long.txt';[IO.File]::WriteAllText($path,('x'*2000)+"`npassword=example-secret")
        $r=Get-PumpFile -Path $path
        $r.Truncation.Applied | Should -BeTrue
        (ConvertTo-Json $r -Depth 24 -Compress) | Should -Not -Match 'example-secret'
    }
}
