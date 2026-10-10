BeforeAll {. (Join-Path $PSScriptRoot '../TestSupport.ps1');Import-Module (Join-Path $script:Library 'OutputPump.PoSh.psd1') -Force -DisableNameChecking}
Describe 'Invoke-OutputPumpTask' {
    It 'retains an assigned intermediate failure and truncation in one bounded envelope' {
        $output=@(Invoke-OutputPumpTask -MaxOutputBytes 1024 {
            $bad=Invoke-OutputPumpStep search {New-OutputPumpResult -Summary failed -Outcome Failed -ExitCode 2 -ErrorInfo @{Code='SearchErrors'}}
            $good=Invoke-OutputPumpStep count {New-OutputPumpResult -Summary counted -Data @{Count=100} -Truncated -Reasons @('ItemLimit')}
            [pscustomobject]@{Total=$good.Data.Count}
        })
        $output.Count | Should -Be 1
        $output[0].Outcome | Should -Be Failed
        $output[0].Data.Status.Steps | Should -Be 2
        $output[0].Data.Items[0].Total | Should -Be 100
        $output[0].Data.Issues[0].ErrorCode | Should -Be SearchErrors
        $output[0].Truncation.Reasons | Should -Contain IntermediateTruncation
    }
    It 'bounds the entire task rather than emitting twenty individually bounded JSON strings' {
        $json=@(Invoke-OutputPumpTask -AsJson -MaxOutputBytes 1024 {
            foreach($index in 1..20){
                $result=Invoke-OutputPumpStep "step-$index" {New-OutputPumpResult -Summary complete -Data @{Items=@(1..20 | ForEach-Object {@{Text=('x'*300)}})}}
                [pscustomobject]@{Step=$index;Preview=$result.Data.Items}
            }
        })
        $json.Count | Should -Be 1
        [Text.Encoding]::UTF8.GetByteCount($json[0]) | Should -BeLessOrEqual 1024
        $result=ConvertFrom-Json $json[0]
        $result.Data.Status.Steps | Should -Be 20
        $result.Truncation.Applied | Should -BeTrue
    }
    It 'preserves failure counters when diagnostic details do not fit' {
        $result=Invoke-OutputPumpTask -MaxOutputBytes 1024 {
            foreach($i in 1..20){$local=Invoke-OutputPumpStep ('stage-'+('n'*500)) {New-OutputPumpResult -Summary failed -Outcome Failed -ErrorInfo @{Code=('e'*500)}}}
        }
        $result.Outcome | Should -Be Failed
        $result.Data.Status.Failed | Should -Be 20
        $result.Data.Status.OmittedIssues | Should -BeGreaterThan 0
        $result.Truncation.Applied | Should -BeTrue
        $result.Error.Code | Should -Be TaskFailed
    }
    It 'prioritizes a later failure over successful truncated previews' {
        $result=Invoke-OutputPumpTask {
            1..3 | ForEach-Object {$local=Invoke-OutputPumpStep preview {New-OutputPumpResult -Summary preview -Truncated}}
            $local=Invoke-OutputPumpStep important {New-OutputPumpResult -Summary failed -Outcome Failed -ErrorInfo @{Code='ImportantFailure'}}
        }
        $result.Data.Issues[0].ErrorCode | Should -Be ImportantFailure
        $result.Data.Status.OmittedIssues | Should -Be 1
    }
    It 'preserves timeout and unsupported outcomes without rerunning steps' {
        $result=Invoke-OutputPumpTask {
            $local=Invoke-OutputPumpStep unsupported {New-OutputPumpResult -Summary unsupported -Outcome Unsupported}
            $local=Invoke-OutputPumpStep timeout {New-OutputPumpResult -Summary timeout -Outcome Timeout -RetryAdvice Unknown}
        }
        $result.Outcome | Should -Be Timeout
        $result.RetryAdvice | Should -Be Unknown
        $result.Data.Status.Steps | Should -Be 2
    }
    It 'does not count an emitted step envelope twice' {
        $result=Invoke-OutputPumpTask {Invoke-OutputPumpStep once {New-OutputPumpResult -Summary complete}}
        $result.Data.Status.Steps | Should -Be 1
        @($result.Data.Items).Count | Should -Be 0
    }
    It 'keeps the narrowing advice and reason of an incomplete intermediate analysis' {
        $result=Invoke-OutputPumpTask {
            $local=Invoke-OutputPumpStep search {New-OutputPumpResult -Summary incomplete -Outcome Failed -RetryAdvice NarrowRequest -Truncated -Reasons @('RecordLimit') -ErrorInfo @{Code='RecordLimit'}}
        }
        $result.RetryAdvice | Should -Be NarrowRequest
        $result.Data.Issues[0].Reasons | Should -Contain RecordLimit
    }
    It 'restores the outer task after a nested task' {
        $result=Invoke-OutputPumpTask {
            $inner=Invoke-OutputPumpTask {$local=Invoke-OutputPumpStep inner {New-OutputPumpResult -Summary inner}}
            $local=Invoke-OutputPumpStep outer {New-OutputPumpResult -Summary outer}
            @{InnerSteps=$inner.Data.Status.Steps}
        }
        $result.Data.Status.Steps | Should -Be 1
        $result.Data.Items[0].InnerSteps | Should -Be 1
    }
    It 'contains diagnostic streams and exceptions and redacts projected secrets' {
        $result=Invoke-OutputPumpTask {
            @{Password='private-value';Useful=42}
            Write-Warning 'private diagnostic'
            Write-Host 'private host message'
            throw 'private exception'
        }
        $result.Outcome | Should -Be Failed
        $json=ConvertTo-OutputPumpJson $result
        $json | Should -Not -Match 'private-value|private diagnostic|private host message|private exception'
        $result.Data.Items[0].Useful | Should -Be 42
    }
}
