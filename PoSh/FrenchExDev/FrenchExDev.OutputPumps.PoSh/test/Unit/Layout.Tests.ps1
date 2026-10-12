<#
.SYNOPSIS
Unit tests for Layout.
.DESCRIPTION
Exercise the named command or contract with isolated fixtures and mocks; keep process execution under test control.
#>


BeforeAll { . (Join-Path $PSScriptRoot 'TestSupport.ps1') }
Describe 'Project and test layout' {
    It 'has a named unit suite for every public source cmdlet' {
        $tests=@(Get-ChildItem $PSScriptRoot -Filter '*.Tests.ps1' -File -Recurse | ForEach-Object BaseName)
        foreach($root in @($script:Library,$script:DevLibrary)){
            foreach($source in Get-ChildItem (Join-Path $root 'src/Public') -Filter '*.ps1' -File -Recurse){$tests | Should -Contain ($source.BaseName+'.Tests')}
        }
    }
    It 'parses every maintained source and loader under PowerShell Core' {
        foreach($root in @($script:Library,$script:DevLibrary)){
            $files=@(Get-ChildItem (Join-Path $root 'src') -File -Recurse | Where-Object Extension -eq '.ps1')
            $files+=Get-ChildItem $root -File -Filter '*.psm1'
            foreach($file in $files){
                $parseTokens=$null;$parseErrors=$null
                [void][Management.Automation.Language.Parser]::ParseFile($file.FullName,[ref]$parseTokens,[ref]$parseErrors)
                @($parseErrors).Count | Should -Be 0 -Because $file.Name
            }
        }
    }
}
