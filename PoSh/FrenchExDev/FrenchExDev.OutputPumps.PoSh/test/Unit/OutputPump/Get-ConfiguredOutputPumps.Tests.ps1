BeforeAll {. (Join-Path $PSScriptRoot '../TestSupport.ps1');Import-Module (Join-Path $script:Library 'OutputPump.PoSh.psd1') -DisableNameChecking -Force}
Describe 'Get-ConfiguredOutputPumps' {
    It 'resolves relative sources from YAML independently of the current directory' {
        $relative=[IO.Path]::GetRelativePath($TestDrive,$script:Library)
        $path=New-TestConfiguration $TestDrive $relative
        $r=Get-ConfiguredOutputPumps -Path $path
        $r.Sources[0].Path | Should -Be $script:Library
        $r.PSObject.TypeNames | Should -Contain 'FrenchExDev.OutputPump.Configuration'
    }
    It 'distinguishes missing YAML from a valid empty configuration' {
        {Get-ConfiguredOutputPumps -Path (Join-Path $TestDrive missing.yaml)} | Should -Throw
        $path=Join-Path $TestDrive empty.yaml;[IO.File]::WriteAllText($path,"version: 2`nroots: []`nproviders: {}`npumps: {}")
        @( (Get-ConfiguredOutputPumps -Path $path).Sources ).Count | Should -Be 0
    }
    It 'rejects duplicate sources and non-boolean enabled values' {
        $path=New-TestConfiguration $TestDrive
        [IO.File]::AppendAllText($path,"`ninvalid: true")
        {Get-ConfiguredOutputPumps -Path $path} | Should -Throw
        [IO.File]::WriteAllText($path,"version: 2`nroots:`n - path: '.'`n   enabled: maybe")
        {Get-ConfiguredOutputPumps -Path $path} | Should -Throw
    }
}
