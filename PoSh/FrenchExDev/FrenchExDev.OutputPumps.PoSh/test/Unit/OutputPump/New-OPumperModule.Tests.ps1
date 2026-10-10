BeforeAll {. (Join-Path $PSScriptRoot '../TestSupport.ps1');Import-Module (Join-Path $script:Library 'OutputPump.PoSh.psd1') -Force -DisableNameChecking}
Describe 'New-OPumperModule' {
    It 'creates a standalone provider with pinned tests and no DevPoSh dependency' {
        $path=Join-Path $TestDrive 'FrenchExDev.OPumper.MyCli.PoSh'
        (New-OPumperModule -Name MyCli -Executable my-cli -Path $path).Outcome | Should -Be Success
        $manifest=Import-PowerShellDataFile (Join-Path $path 'MyCli-OPumper.psd1')
        $manifest.RequiredModules | Should -Not -Contain DevPoSh.PoSh
        Test-Path (Join-Path $path 'src/Setup-OutputPumps.ps1') | Should -BeTrue
        (Import-PowerShellDataFile (Join-Path $path 'resources/dependencies.psd1')).Pester | Should -Be '6.2.0'
    }
    It 'does not create files with WhatIf or overwrite a nonempty directory' {
        $path=Join-Path $TestDrive 'whatif';New-OPumperModule -Name Probe -Executable probe -Path $path -WhatIf | Out-Null;Test-Path $path | Should -BeFalse
        [void][IO.Directory]::CreateDirectory($path);[IO.File]::WriteAllText((Join-Path $path 'keep'),'keep')
        (New-OPumperModule -Name Probe -Executable probe -Path $path).Outcome | Should -Be Failed
    }
}
