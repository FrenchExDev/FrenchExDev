#requires -Version 7.4
#requires -PSEdition Core
[CmdletBinding()]
param([string]$Python='python3')
$ErrorActionPreference='Stop'
Import-Module (Join-Path $PSScriptRoot '../QuietCli.PoSh.psd1') -Force
$fixture=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../fixtures/noisy-job.py'))
$checks=[Collections.Generic.List[object]]::new()
function Check([string]$Name,[scriptblock]$Body) {
    try { & $Body; $checks.Add(@{Name=$Name;Passed=$true}) }
    catch { $checks.Add(@{Name=$Name;Passed=$false;Exception=$_.Exception.GetType().Name}) }
}
function Assert([bool]$Condition,[string]$Message='Assertion failed') { if (!$Condition) { throw $Message } }
function Document([string[]]$NativeArgs,[int[]]$Codes=@(0),[int]$Limit=262144) {
    Invoke-QuietCli -FilePath $Python -ArgumentList (@($fixture)+$NativeArgs) -SuccessExitCodes $Codes `
        -MaxCaptureCharacters $Limit -Parse {param($capture) ConvertFrom-Json $capture.Stdout -ErrorAction Stop}
}
Check 'Data preserves object, scalar, array and null shapes' {
    $object=New-QuietResult -Data ([pscustomobject]@{Answer=42;Nested=@{Valid=$true}})
    Assert ($object.Data.Answer -eq 42 -and $object.Data.Nested.Valid)
    Assert ((New-QuietResult -Data 42).Data -eq 42)
    $array=New-QuietResult -Data @(1,2,3)
    Assert ($array.Data.Count -eq 3 -and $array.Data[1] -eq 2)
    Assert ($null -eq (New-QuietResult -Data $null).Data)
}
Check 'Arguments preserve spaces, quotes and shell-looking text' {
    $argsWanted=@('a space','a"quote',"a'quote",'$(touch SHOULD_NOT_EXIST)','--literal')
    $result=Document (@('--echo-args')+$argsWanted)
    Assert ($result.Outcome -eq 'Success')
    Assert (($result.Data.arguments | ConvertTo-Json -Compress) -eq ($argsWanted | ConvertTo-Json -Compress))
}
Check 'Native failure remains failure even with parseable JSON' {
    $result=Document @('--exit-code','7','--echo-args','value')
    Assert ($result.Outcome -eq 'Failed' -and $result.Execution.ExitCode -eq 7 -and !$result.Execution.AnalysisComplete)
}
Check 'Caller can explicitly accept a nonzero functional exit code' {
    $result=Document @('--exit-code','7','--echo-args','value') -Codes @(0,7)
    Assert ($result.Outcome -eq 'Success' -and $result.Execution.ExitCode -eq 7)
}
Check 'Missing executable returns a structured failure' {
    $result=Invoke-QuietCli -FilePath '__quiet_cli_missing_executable__' -Parse {param($c) @{Never=$true}}
    Assert ($result.Outcome -eq 'Failed' -and !$result.Execution.AnalysisComplete -and $null -eq $result.Data)
}
Check 'Document capture overflow never parses a clipped document' {
    $result=Document @('--echo-args',('x'*4000)) -Limit 1024
    Assert ($result.Outcome -eq 'Failed' -and $result.Error.Code -eq 'CaptureLimit' -and $null -eq $result.Data)
    Assert ($result.Execution.StdoutCharacters -gt 1024 -and $result.Execution.ExitCode -eq 0)
}
Check 'Large concurrent stdout and stderr are drained with exact counters' {
    $state=@{Progress=0L;Stderr=0L;Final=$null}
    $result=Invoke-QuietCli -FilePath $Python -ArgumentList @($fixture,'--records','10000') -State $state `
        -OnRecord {
            param($record,$state)
            if ($record.Stream -eq 'stderr') {$state.Stderr++;return}
            $event=ConvertFrom-Json $record.Text
            if ($event.type -eq 'progress') {$state.Progress++} else {$state.Final=$event.processed}
        } -Complete {param($state,$capture) [pscustomobject]@{Progress=$state.Progress;Stderr=$state.Stderr;Reported=$state.Final}}
    Assert ($result.Outcome -eq 'Success' -and $result.Data.Progress -eq 10000 -and $result.Data.Stderr -eq 10000)
    Assert ($result.Data.Reported -eq 10000 -and $result.Execution.AnalysisComplete)
    $json=ConvertTo-QuietJson $result -MaxOutputBytes 1024
    Assert ([Text.Encoding]::UTF8.GetByteCount($json) -le 1024)
    Assert ($json -notmatch 'example-secret|verbose progress')
}
Check 'Oversized stream record produces an uncertified failure' {
    $state=@{Seen=0}
    $result=Invoke-QuietCli -FilePath $Python -ArgumentList @($fixture,'--giant','4000') -MaxRecordCharacters 1024 `
        -State $state -OnRecord {param($r,$s) if (!$r.Truncated) {$s.Seen++}} `
        -Complete {param($s,$c) @{Seen=$s.Seen}}
    Assert ($result.Outcome -eq 'Failed' -and $result.Error.Code -eq 'RecordLimit' -and $result.Execution.LostRecords -eq 1)
}
Check 'Parser failure drains the command without relaunch or raw fallback' {
    $result=Invoke-QuietCli -FilePath $Python -ArgumentList @($fixture,'--records','1000') `
        -State @{} -OnRecord {param($r,$s) throw 'bad parser'} -Complete {param($s,$c) @{Never=$true}}
    Assert ($result.Outcome -eq 'Failed' -and $result.Error.Code -eq 'ParserFailed')
    Assert ($result.Execution.ExitCode -eq 0 -and $result.Execution.StdoutCharacters -gt 50000)
}
Check 'Malformed machine output does not become a success' {
    $result=Document @('--malformed')
    Assert ($result.Outcome -eq 'Failed' -and $result.Error.Code -eq 'ParserFailed')
}
Check 'Timeout is distinct from capture and output limits' {
    $result=Invoke-QuietCli -FilePath $Python -ArgumentList @($fixture,'--sleep','5','--records','1') `
        -TimeoutSeconds 1 -Parse {param($c) @{Never=$true}}
    Assert ($result.Outcome -eq 'Timeout' -and $result.Error.Code -eq 'Timeout' -and !$result.Execution.AnalysisComplete)
    Assert ($result.Execution.ElapsedMs -lt 4000)
}
Check 'Known sensitive fields are masked and omissions reported' {
    $result=New-QuietResult -Data @{Password='do-not-show';Message='token=also-secret';Answer=42}
    $json=ConvertTo-QuietJson $result
    Assert ($json -notmatch 'do-not-show|also-secret' -and $result.Data.Answer -eq 42)
    Assert ($result.Truncation.Applied)
}
Check 'An oversized projection cannot exceed the final byte budget' {
    $data=@{Values=@(1..200 | ForEach-Object {('x'*100)})}
    $result=New-QuietResult -Data $data -MaxOutputBytes 1024
    $json=ConvertTo-QuietJson $result -MaxOutputBytes 1024
    Assert ([Text.Encoding]::UTF8.GetByteCount($json) -le 1024 -and $result.Truncation.Applied)
    Assert ($null -eq $result.Data)
}
Check 'Twenty assigned failures retain aggregate counters under 1 KiB' {
    $result=Invoke-QuietTask -MaxOutputBytes 1024 {
        foreach ($i in 1..20) {
            $local=Invoke-QuietStep "step-$i" {New-QuietResult -Outcome Failed -ErrorInfo @{Code='ExpectedFailure'}}
        }
        @{Large=@(1..100 | ForEach-Object {('z'*100)})}
    }
    $json=ConvertTo-QuietJson $result -MaxOutputBytes 1024
    Assert ($result.Outcome -eq 'Failed' -and $result.Execution.Failed -eq 20 -and $result.Execution.Steps -eq 20)
    Assert ([Text.Encoding]::UTF8.GetByteCount($json) -le 1024)
}
Check 'Oversized metadata cannot defeat the transport byte limit' {
    $result=New-QuietResult -Data @{Answer=42} -MaxOutputBytes 1024 `
        -Execution @{ExitCode=('e'*2000);ElapsedMs=('m'*2000);Steps=20;Failed=20} `
        -ErrorInfo @{Code=('c'*2000);Detail=('d'*2000)}
    $json=ConvertTo-QuietJson $result -MaxOutputBytes 1024
    Assert ([Text.Encoding]::UTF8.GetByteCount($json) -le 1024)
    Assert ($result.Execution.Steps -eq 20 -and $result.Execution.Failed -eq 20)
}
Check 'Repeated emission of the same step envelope is not double-counted' {
    $result=Invoke-QuietTask {$step=Invoke-QuietStep once {New-QuietResult -Data @{Value=1}};$step;$step;@{Answer=1}}
    Assert ($result.Execution.Steps -eq 1 -and $result.Data.Answer -eq 1)
}
Check 'An assigned timeout survives the final projection' {
    $result=Invoke-QuietTask {$local=Invoke-QuietStep waited {New-QuietResult -Outcome Timeout -ErrorInfo @{Code='Timeout'}};@{Partial=$true}}
    Assert ($result.Outcome -eq 'Timeout' -and $result.Execution.Timeouts -eq 1)
}
Check 'Generated multi-stage script returns exactly one bounded document' {
    $json=@(& (Join-Path $PSScriptRoot '../examples/Generated-NoisyJob.ps1') -Python $Python -Records 100)
    Assert ($json.Count -eq 1)
    $result=ConvertFrom-Json $json[0]
    Assert ($result.Outcome -eq 'Success' -and $result.Data.Prepared -eq 100 -and $result.Data.Validated -eq 100)
    Assert ($result.Execution.Steps -eq 2 -and [Text.Encoding]::UTF8.GetByteCount($json[0]) -le 2048)
}
$passed=@($checks | Where-Object Passed).Count
$failed=$checks.Count-$passed
$result=New-QuietResult -Summary "$passed checks passed; $failed failed." -Outcome $(if ($failed) {'Failed'} else {'Success'}) `
    -Data @{PowerShell=$PSVersionTable.PSVersion.ToString();Passed=$passed;Failed=$failed;Checks=$checks.ToArray()} -MaxOutputBytes 4096
ConvertTo-QuietJson $result -MaxOutputBytes 4096
if ($failed) { exit 1 }
