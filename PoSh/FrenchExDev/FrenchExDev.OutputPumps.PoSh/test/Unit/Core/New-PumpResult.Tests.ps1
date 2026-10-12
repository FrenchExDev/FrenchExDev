<#
.SYNOPSIS
Unit tests for New-PumpResult.
.DESCRIPTION
Exercise the named command or contract with isolated fixtures and mocks; keep process execution under test control.
#>


BeforeAll {. (Join-Path $PSScriptRoot '../TestSupport.ps1');Import-Module (Join-Path $script:Library 'Core.psm1') -Force}
Describe 'New-PumpResult' {
    It 'bounds UTF-8 output, reports omissions and preserves an unknown total' {
        $r=New-PumpResult -Summary 'bounded' -Data @{Items=@(1..80|ForEach-Object {@{Text=('é'*400)}})} -MaxItems 3 -MaxOutputBytes 1024 -UnknownTotal
        [Text.Encoding]::UTF8.GetByteCount((ConvertTo-Json $r -Depth 24 -Compress)) | Should -BeLessOrEqual 1024
        $r.Truncation.Applied | Should -BeTrue
        $r.Truncation.TotalCount | Should -BeNullOrEmpty
        $r.Truncation.Reasons | Should -Contain ItemLimit
    }
    It 'redacts secrets in data, summary and errors' {
        $r=New-PumpResult -Summary 'password=example-secret' -Data @{Items=@(@{Password='example-secret'})} -ErrorInfo @{Message='Bearer example-secret'}
        (ConvertTo-Json $r -Depth 24 -Compress) | Should -Not -Match 'example-secret'
        $r.Truncation.Reasons | Should -Contain SensitiveData
    }
    It 'distinguishes empty success from failure' {
        (New-PumpResult -Summary 'empty').Outcome | Should -Be Success
        (New-PumpResult -Summary 'failed' -Outcome Failed -ErrorInfo @{Code='Expected'}).Error.Code | Should -Be Expected
    }
}
