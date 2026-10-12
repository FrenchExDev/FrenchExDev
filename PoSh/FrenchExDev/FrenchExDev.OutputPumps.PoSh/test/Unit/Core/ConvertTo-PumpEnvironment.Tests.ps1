BeforeAll {
    . (Join-Path $PSScriptRoot '../TestSupport.ps1')
    Import-Module (Join-Path $script:Library 'OutputPump.PoSh.psd1') -Force -DisableNameChecking
}
Describe 'ConvertTo-PumpEnvironment' {
    It 'preserves a true null removal separately from an empty string and leaves the input unchanged' {
        InModuleScope OutputPump.PoSh {
            $inputValues=@{REMOVED=$null;EMPTY='';NUMBER=42}
            $converted=ConvertTo-PumpEnvironment $inputValues
            ($null -eq $converted['REMOVED']) | Should -BeTrue
            ($null -eq $converted['EMPTY']) | Should -BeFalse
            $converted['EMPTY'].Length | Should -Be 0
            $converted['NUMBER'] | Should -BeExactly '42'
            $inputValues.NUMBER | Should -BeOfType ([int])
            $inputValues.Count | Should -Be 3
        }
    }
    It 'rejects invalid names and NUL values without revealing entries' {
        InModuleScope OutputPump.PoSh {
            foreach($values in @(@{'BAD=NAME'='private-value'},@{VALID="private`0value"},@{42='private-value'})) {
                {ConvertTo-PumpEnvironment $values} | Should -Throw -ExpectedMessage 'Invalid process environment entry.'
            }
        }
    }
}
