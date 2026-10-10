BeforeAll {. (Join-Path $PSScriptRoot '../TestSupport.ps1');Import-Module (Join-Path $script:Library 'Core.psm1') -Force}
Describe 'Complete-PumpProcess' {
    It 'distinguishes no match exit code from an actual search error' {
        $r=Complete-PumpProcess (New-TestCapture '' 1) -Summary 'empty' -SuccessCodes @(0,1) -Parse {@{Items=@()}}
        $r.Outcome | Should -Be Success
        (Complete-PumpProcess (New-TestCapture '' 2) -Summary 'failed').Outcome | Should -Be Failed
    }
    It 'rejects incomplete captures before parsing' {
        $capture=New-TestCapture 'untrusted'
        $capture.Truncated=$true
        $r=Complete-PumpProcess $capture -Summary 'capture' -Parse {throw 'must not parse'}
        $r.Error.Code | Should -Be CaptureLimit
        $r.RetryAdvice | Should -Be NarrowRequest
    }
    It 'returns a structured parser error' {
        $r=Complete-PumpProcess (New-TestCapture 'invalid') -Summary 'parser' -Parse {throw 'raw output must stay private'}
        $r.Error.Code | Should -Be UnrecognizedFormat
        (ConvertTo-Json $r -Depth 24) | Should -Not -Match 'raw output must stay private'
    }
}
