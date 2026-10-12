<#
.SYNOPSIS
Unit tests for New-OutputPumpScript.
.DESCRIPTION
Exercise the named command or contract with isolated fixtures and mocks; keep process execution under test control.
#>


BeforeAll { . (Join-Path $PSScriptRoot '../TestSupport.ps1'); Import-Module (Join-Path $script:Library 'OutputPump.PoSh.psd1') -Force -DisableNameChecking }
Describe 'New-OutputPumpScript' {
    It 'generates a task caller with an inert body and serialized arguments' {
        $call=New-OutputPumpScript -TaskScriptBlock {param($value) @{Value=$value}} -ArgumentList @('$(throw "never")') -MaxOutputBytes 1024 -ScriptDirectory $TestDrive
        $call.Outcome | Should -Be Success
        $text=[IO.File]::ReadAllText($call.Data.Items[0].ScriptPath)
        $text | Should -Match 'Invoke-OutputPumpTask'
        $text | Should -Not -Match '\$\(throw'
    }
    It 'generates an inert caller with argument data and no shell interpolation' {
        $root=Join-Path $TestDrive 'caller';$descriptor=New-TestPumpLibrary $root
        Register-OutputPump $descriptor -LibraryRoot $root | Out-Null
        $r=New-OutputPumpScript -SoftwareId fixture -Function Get-FixturePump -Parameters @{Path='$(throw "executed")'} -LibraryRoot $root -ScriptDirectory $TestDrive
        $r.Outcome | Should -Be Success
        $scriptText=[IO.File]::ReadAllText($r.Data.Items[0].ScriptPath)
        $scriptText | Should -Not -Match '\$\(throw'
        $scriptText | Should -Match 'FromBase64String'
        Remove-Item -LiteralPath $r.Data.Items[0].ScriptPath
    }
    It 'rejects an unknown parameter before creating a caller' {
        (New-OutputPumpScript -SoftwareId git -Function Get-PumpGitStatus -Parameters @{Missing=1} -ScriptDirectory $TestDrive).Outcome | Should -Be Failed
        @(Get-ChildItem $TestDrive -Filter 'output-pump-call-*').Count | Should -Be 0
    }
    It 'rejects missing required parameters before creating a prompting caller' {
        (New-OutputPumpScript -SoftwareId powershell -Function Get-PumpFile -ScriptDirectory $TestDrive).Outcome | Should -Be Failed
        @(Get-ChildItem $TestDrive -Filter 'output-pump-call-*').Count | Should -Be 0
    }
}
