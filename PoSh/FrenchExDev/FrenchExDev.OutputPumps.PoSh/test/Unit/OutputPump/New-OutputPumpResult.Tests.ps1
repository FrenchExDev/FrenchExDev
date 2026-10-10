BeforeAll {. (Join-Path $PSScriptRoot '../TestSupport.ps1');Import-Module (Join-Path $script:Library 'OutputPump.PoSh.psd1') -Force -DisableNameChecking}
Describe 'New-OutputPumpResult' {
    It 'accepts a typed business object and preserves its custom type name' {
        $data=[pscustomobject]@{PSTypeName='Example.Domain';Value=42}
        $r=New-OutputPumpResult -Summary business -Data $data
        $r.Data | Should -BeOfType ([pscustomobject])
        $r.Data.Value | Should -Be 42
        $r.Data.PSObject.TypeNames | Should -Contain 'Example.Domain'
    }
    It 'preserves the envelope while bounding and redacting provider data' {
        $r=New-OutputPumpResult -Summary 'fixture' -Data @{Password='secret';Items=@(1,2,3)} -MaxItems 1
        $r.PSObject.Properties.Name.Count | Should -Be 7;$r.Data.Password | Should -Be '[REDACTED]';$r.Truncation.Applied | Should -BeTrue
    }
}
