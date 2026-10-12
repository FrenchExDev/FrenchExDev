<#
.SYNOPSIS
Unit tests for Invoke-OutputPumpNative.
.DESCRIPTION
Exercise the named command or contract with isolated fixtures and mocks; keep process execution under test control.
#>


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
    It 'forwards environment additions and removals to buffered capture without logging their values' {
        Mock Invoke-PumpProcess -ModuleName OutputPump.PoSh {[pscustomobject]@{Stdout='';Stderr='';StartError='';TimedOut=$false;Truncated=$false;ExitCode=0}}
        $events=@(Invoke-OutputPumpNative -Executable fixture -Environment @{ADDED='private-fixture-value';REMOVED=$null} -Parse {@{Items=@()}} -Debug 5>&1)
        $result=@($events | Where-Object {$_ -isnot [Management.Automation.DebugRecord]})
        $result.Count | Should -Be 1
        $result[0].Outcome | Should -Be Success
        Should -Invoke Invoke-PumpProcess -ModuleName OutputPump.PoSh -Exactly -Times 1 -ParameterFilter {$Environment.ADDED -eq 'private-fixture-value' -and $Environment.Contains('REMOVED') -and $null -eq $Environment.REMOVED}
        ($events | Where-Object {$_ -is [Management.Automation.DebugRecord]} | ForEach-Object Message) -join ' ' | Should -Not -Match 'private-fixture-value'
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
    It 'aggregates per-record timing and preserves one envelope under debug' {
        InModuleScope OutputPump.PoSh {
            $script:stream=[pscustomobject]@{Index=0;Result=[pscustomobject]@{Stdout='';Stderr='';ExitCode=0;StartError='';TimedOut=$false;Truncated=$false;OmittedRecords=0}}
            $script:stream | Add-Member ScriptMethod Next {if($this.Index++ -lt 3){[pscustomobject]@{Text='event';Stream='stdout';Truncated=$false;Length=5}}}
            $script:stream | Add-Member ScriptMethod Dispose {}
            Mock Start-OutputPumpStream {$script:stream}
            $events=@(Invoke-OutputPumpNative -Executable fixture -Environment @{REMOVED=$null} -State @{Count=0} -OnRecord {param($r,$s) $s.Count++} -Parse {param($s,$c) @{Count=$s.Count}} -Debug 5>&1)
            $results=@($events | Where-Object {$_ -isnot [Management.Automation.DebugRecord]})
            $results.Count | Should -Be 1
            $results[0].Data.Count | Should -Be 3
            $messages=($events | Where-Object {$_ -is [Management.Automation.DebugRecord]} | ForEach-Object Message) -join ' '
            $messages | Should -Match 'action=Native.OnRecord; elapsedMs=\d+\.\d{3}; count=3;'
            $messages | Should -Match 'action=Native.WaitForRecords;'
            $messages | Should -Match 'action=Native.Total;'
            Should -Invoke Start-OutputPumpStream -Exactly -Times 1 -ParameterFilter {$Environment.Contains('REMOVED') -and $null -eq $Environment.REMOVED}
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
