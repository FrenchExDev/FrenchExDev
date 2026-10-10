BeforeAll {. (Join-Path $PSScriptRoot '../TestSupport.ps1');Import-Module (Join-Path $script:Library 'OutputPump.PoSh.psd1') -Force -DisableNameChecking}
Describe 'Invoke-OutputPumpNative' {
    BeforeEach {
        Mock Start-OutputPumpStream -ModuleName OutputPump.PoSh {throw 'Unexpected external streaming command in a unit test.'}
    }
    It 'parses one bounded capture and redacts its useful result' {
        Mock Invoke-PumpProcess -ModuleName OutputPump.PoSh {[pscustomobject]@{Stdout='2';Stderr='';StartError=$null;TimedOut=$false;Truncated=$false;ExitCode=0}}
        $r=Invoke-OutputPumpNative -Executable fixture -Parse {param($c) @{Count=[int]$c.Stdout;Password='fixture-secret'}}
        $r.Data.Count | Should -Be 2;$r.Data.Password | Should -Be '[REDACTED]'
        Should -Invoke Invoke-PumpProcess -ModuleName OutputPump.PoSh -Exactly -Times 1
    }
    It 'converts unexpected execution exceptions into a bounded failure' {
        Mock Invoke-PumpProcess -ModuleName OutputPump.PoSh {throw 'private error'}
        (Invoke-OutputPumpNative -Executable fixture -Parse {}).Outcome | Should -Be Failed
    }
    It 'runs streaming callbacks on the caller thread and finalizes only after draining' {
        InModuleScope OutputPump.PoSh {
            $script:stream=[pscustomobject]@{Index=0;Disposed=$false;Result=[pscustomobject]@{Stdout='';Stderr='';ExitCode=0;StartError='';TimedOut=$false;Truncated=$false;OmittedRecords=0}}
            $script:stream | Add-Member ScriptMethod Next {if($this.Index++ -lt 3){[pscustomobject]@{Text='event';Stream='stdout';Truncated=$false;Length=5}}}
            $script:stream | Add-Member ScriptMethod Dispose {$this.Disposed=$true}
            Mock Start-OutputPumpStream {$script:stream}
            $state=@{Count=0}
            $result=Invoke-OutputPumpNative -Executable fixture -State $state -OnRecord {param($r,$s) $s.Count++} -Parse {param($s,$c) @{Count=$s.Count}}
            $result.Data.Count | Should -Be 3
            $script:stream.Disposed | Should -BeTrue
            Should -Invoke Start-OutputPumpStream -Exactly -Times 1
        }
    }
    It 'continues draining after a callback failure without exposing its exception' {
        InModuleScope OutputPump.PoSh {
            $script:stream=[pscustomobject]@{Index=0;Disposed=$false;Result=[pscustomobject]@{Stdout='';Stderr='';ExitCode=7;StartError='';TimedOut=$false;Truncated=$false;OmittedRecords=0}}
            $script:stream | Add-Member ScriptMethod Next {if($this.Index++ -lt 5){[pscustomobject]@{Text='event';Stream='stdout';Truncated=$false;Length=5}}}
            $script:stream | Add-Member ScriptMethod Dispose {$this.Disposed=$true}
            Mock Start-OutputPumpStream {$script:stream}
            $result=Invoke-OutputPumpNative -Executable fixture -State @{} -OnRecord {throw 'private parse failure'} -Parse {throw 'must not finalize'}
            $result.Error.Code | Should -Be UnrecognizedFormat
            $result.ExitCode | Should -Be 7
            $script:stream.Index | Should -Be 6
            (ConvertTo-OutputPumpJson $result) | Should -Not -Match 'private parse failure'
        }
    }
}
