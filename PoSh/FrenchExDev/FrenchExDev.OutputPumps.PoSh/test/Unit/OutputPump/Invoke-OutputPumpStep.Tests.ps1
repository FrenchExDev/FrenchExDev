BeforeAll {. (Join-Path $PSScriptRoot '../TestSupport.ps1');Import-Module (Join-Path $script:Library 'OutputPump.PoSh.psd1') -Force -DisableNameChecking}
Describe 'Invoke-OutputPumpStep' {
    It 'requires a task without executing the script' {
        $result=Invoke-OutputPumpStep orphan {throw 'must not execute'}
        $result.Error.Code | Should -Be NoActiveTask
    }
    It 'converts multiple outputs or diagnostic streams into a recorded failure' {
        $result=Invoke-OutputPumpTask {
            $local=Invoke-OutputPumpStep invalid {New-OutputPumpResult -Summary complete;Write-Warning 'private warning'}
        }
        $result.Data.Status.Failed | Should -Be 1
        $result.Data.Issues[0].ErrorCode | Should -Be TaskStepFailed
        (ConvertTo-OutputPumpJson $result) | Should -Not -Match 'private warning'
    }
    It 'returns the original business object for local processing' {
        $result=Invoke-OutputPumpTask {
            $local=Invoke-OutputPumpStep useful {New-OutputPumpResult -Summary useful -Data ([pscustomobject]@{Count=25})}
            @{Double=$local.Data.Count*2}
        }
        $result.Data.Items[0].Double | Should -Be 50
    }
}
