BeforeAll { . (Join-Path $PSScriptRoot '../TestSupport.ps1'); Import-Module (Join-Path $script:Library 'OutputPump.PoSh.psd1') -Force -DisableNameChecking }
Describe 'ConvertFrom-OutputPumpArguments' {
    It 'preserves literal paths, options after a separator and repeated flags' {
        InModuleScope 'OutputPump.PoSh' {
            $route=@{Options=@{'-g'=@{Parameter='Glob';Type='String';Multiple=$true}};Positional='Path';Multiple=$true}
            $r=ConvertFrom-OutputPumpArguments -Route $route -Arguments @('-g','*.ps1','-g','*.md','--','-literal path')
            $r.Glob | Should -Be @('*.ps1','*.md')
            $r.Path | Should -Be @('-literal path')
        }
    }
    It 'rejects unsupported flags before process launch' {
        InModuleScope 'OutputPump.PoSh' {{ConvertFrom-OutputPumpArguments -Route @{Options=@{};Positional='Path';Multiple=$true} -Arguments @('--unknown')} | Should -Throw}
    }
}
