<#
.SYNOPSIS
Integration harness for OutputPumpStreaming.
.DESCRIPTION
Run controlled scenarios, keep raw child-process output local and emit a bounded report of passed and failed checks.
#>


#requires -Version 7.4
param([string]$Case)
$ErrorActionPreference='Stop'
$library=Split-Path (Split-Path $PSScriptRoot)
$env:PSModulePath=(Join-Path $library '.cache/Modules')+[IO.Path]::PathSeparator+$env:PSModulePath
Import-Module (Join-Path $library OutputPump.PoSh.psd1) -DisableNameChecking
$root=Join-Path $library ('.cache/streaming-'+[guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($root)
$failures=[Collections.Generic.List[object]]::new();$passed=0
function Assert-Streaming([bool]$Condition,[string]$Message){
    <#
    .SYNOPSIS
    Check a streaming integration invariant.
    .DESCRIPTION
    Raise a controlled failure when capture, parsing or budgeting does not meet the expected contract.
    #>
    if(!$Condition){throw $Message}}
function Test-Streaming([string]$Name,[scriptblock]$Body){
    <#
    .SYNOPSIS
    Run and record a selected streaming integration scenario.
    .DESCRIPTION
    Capture scenario failures in the final report while supporting a focused scenario selection.
    #>
    if($Case -and $Name -notmatch $Case){return};try{& $Body;$script:passed++}catch{$script:failures.Add(@{Test=$Name;Message=$_.Exception.Message;Line=$_.InvocationInfo.ScriptLineNumber})}}
try {
    $marker=Join-Path $root completed.txt
    $flood=Join-Path $root flood.ps1
    [IO.File]::WriteAllText($flood,@'
param($Marker)
$line='x'*256
for($index=0;$index -lt 20000;$index++){[Console]::Out.WriteLine($line);[Console]::Error.WriteLine($line)}
[IO.File]::WriteAllText($Marker,'completed')
exit 7
'@)
    $arguments=@('-NoProfile','-NonInteractive','-File',$flood,$marker)
    Test-Streaming 'Both streams beyond capture limits are analyzed through EOF before returning' {
        $state=@{Out=0;Err=0;Items=[Collections.Generic.List[object]]::new()}
        $result=Invoke-OutputPumpNative -Executable (Join-Path $PSHOME pwsh.exe) -ArgumentList $arguments -TimeoutSeconds 60 -State $state -OnRecord {
            param($record,$state)
            if($record.Stream -eq 'stdout'){$state.Out++;if($state.Items.Count -lt 2){$state.Items.Add(@{Text=$record.Text})}}else{$state.Err++}
        } -Parse {param($state,$capture) @{Out=$state.Out;Err=$state.Err;Items=$state.Items.ToArray();_PumpMeta=@{TotalCount=$state.Out;Truncated=$true;Reasons=@('ItemLimit')}}} -SuccessCodes @(7) -MaxOutputBytes 1024
        Assert-Streaming ($result.Outcome -eq 'Success' -and $result.Data.Out -eq 20000 -and $result.Data.Err -eq 20000) 'Full stream counts were lost.'
        Assert-Streaming ((Test-Path -LiteralPath $marker) -and $result.ExitCode -eq 7 -and $result.Truncation.Applied) 'Preview truncation stopped the process.'
        Assert-Streaming ([Text.Encoding]::UTF8.GetByteCount((ConvertTo-OutputPumpJson $result -MaxOutputBytes 1024)) -le 1024) 'Final envelope exceeded its budget.'
    }
    Test-Streaming 'Callback failure drains the command and preserves its exit code' {
        Remove-Item -LiteralPath $marker
        $result=Invoke-OutputPumpNative -Executable (Join-Path $PSHOME pwsh.exe) -ArgumentList $arguments -TimeoutSeconds 60 -State @{} -OnRecord {throw 'private parser failure'} -Parse {throw 'not reached'}
        Assert-Streaming ($result.Error.Code -eq 'UnrecognizedFormat' -and $result.ExitCode -eq 7 -and (Test-Path -LiteralPath $marker)) 'Parser failure abandoned or relaunched the command.'
    }
    Test-Streaming 'Oversized records remain bounded, signal incomplete analysis, and do not stop the command' {
        $giant=Join-Path $root giant.ps1
        [IO.File]::WriteAllText($giant,'param($Marker);[Console]::WriteLine(("x"*2000000));[Console]::WriteLine("last");[IO.File]::WriteAllText($Marker,"completed")')
        $result=Invoke-OutputPumpNative -Executable (Join-Path $PSHOME pwsh.exe) -ArgumentList @('-NoProfile','-File',$giant,$marker) -State @{Lines=0} -MaxRecordCharacters 1024 -OnRecord {param($r,$s) $s.Lines++} -Parse {param($s,$c) @{ObservedLines=$s.Lines}}
        Assert-Streaming ($result.Error.Code -eq 'RecordLimit' -and $result.Data.ObservedLines -eq 2 -and $null -eq $result.Truncation.TotalCount -and !$result.Data.AnalysisComplete -and $result.ExitCode -eq 0) 'Giant records were silently treated as complete.'
    }
    Test-Streaming 'Expiration terminates a producer under backpressure without relaunch' {
        Remove-Item -LiteralPath $marker
        $result=Invoke-OutputPumpNative -Executable (Join-Path $PSHOME pwsh.exe) -ArgumentList $arguments -TimeoutSeconds 1 -State @{} -OnRecord {Start-Sleep -Milliseconds 5} -Parse {param($s,$c) @{}}
        Assert-Streaming ($result.Outcome -eq 'Timeout' -and $result.RetryAdvice -eq 'Unknown' -and !(Test-Path -LiteralPath $marker)) 'Deadline did not terminate the backpressured producer.'
    }
    $environmentProbe=Join-Path $root environment.ps1
    [IO.File]::WriteAllText($environmentProbe,@'
$values=[Environment]::GetEnvironmentVariables()
[pscustomobject]@{
    Inherited=($env:OUTPUTPUMP_TEST_KEEP -eq 'parent-kept')
    Added=($env:OUTPUTPUMP_TEST_ADDED -eq 'child été')
    Overridden=($env:OUTPUTPUMP_TEST_OVERRIDE -eq 'child-value')
    Removed=(!$values.Contains('OUTPUTPUMP_TEST_REMOVED'))
    Empty=($values.Contains('OUTPUTPUMP_TEST_EMPTY') -and $values['OUTPUTPUMP_TEST_EMPTY'] -eq '')
    DefaultOverridden=($env:TERM -eq 'custom-term')
    DefaultRemoved=(!$values.Contains('NO_COLOR'))
    CustomValueReceived=($env:OUTPUTPUMP_TEST_SECRET -eq 'private-child-environment-value')
} | ConvertTo-Json -Compress
'@)
    foreach($mode in @('Buffered','Streaming')) {
        Test-Streaming "Child environment is isolated and timed in $mode mode" {
            $saved=@{}
            foreach($name in @('OUTPUTPUMP_TEST_KEEP','OUTPUTPUMP_TEST_OVERRIDE','OUTPUTPUMP_TEST_REMOVED')){$saved[$name]=[Environment]::GetEnvironmentVariable($name)}
            try {
                [Environment]::SetEnvironmentVariable('OUTPUTPUMP_TEST_KEEP','parent-kept')
                [Environment]::SetEnvironmentVariable('OUTPUTPUMP_TEST_OVERRIDE','parent-value')
                [Environment]::SetEnvironmentVariable('OUTPUTPUMP_TEST_REMOVED','parent-removed')
                $parameters=@{Executable=(Join-Path $PSHOME pwsh.exe);ArgumentList=@('-NoProfile','-File',$environmentProbe);WorkingDirectory=$root;
                    Environment=@{OUTPUTPUMP_TEST_ADDED='child été';OUTPUTPUMP_TEST_OVERRIDE='child-value';OUTPUTPUMP_TEST_REMOVED=$null;OUTPUTPUMP_TEST_EMPTY='';TERM='custom-term';NO_COLOR=$null;OUTPUTPUMP_TEST_SECRET='private-child-environment-value'};Debug=$true}
                if($mode -eq 'Streaming') {
                    $parameters.State=@{Report=$null}
                    $parameters.OnRecord={param($r,$s) if($r.Stream -eq 'stdout'){$s.Report=ConvertFrom-Json $r.Text}}
                    $parameters.Parse={param($s,$c) @{Report=$s.Report}}
                }else{$parameters.Parse={param($c) @{Report=(ConvertFrom-Json $c.Stdout)}}}
                $events=@(Invoke-OutputPumpNative @parameters 5>&1)
                $results=@($events | Where-Object {$_ -isnot [Management.Automation.DebugRecord]})
                Assert-Streaming ($results.Count -eq 1 -and $results[0].Outcome -eq 'Success') 'Environment invocation did not return one successful envelope.'
                foreach($name in @('Inherited','Added','Overridden','Removed','Empty','DefaultOverridden','DefaultRemoved','CustomValueReceived')){Assert-Streaming ($results[0].Data.Report.$name -eq $true) ('Child environment invariant failed: '+$name)}
                Assert-Streaming ($env:OUTPUTPUMP_TEST_OVERRIDE -eq 'parent-value' -and $env:OUTPUTPUMP_TEST_REMOVED -eq 'parent-removed') 'Child overrides changed the parent environment.'
                $messages=($events | Where-Object {$_ -is [Management.Automation.DebugRecord]} | ForEach-Object Message) -join ' '
                Assert-Streaming ($messages -notmatch 'private-child-environment-value|child été|custom-term') 'Debug traces exposed environment values.'
                foreach($phase in @('Setup','Start','StdoutDrain','StderrDrain','Run','Elapsed')){Assert-Streaming ($messages -match ("action=Native\.$phase; elapsedMs=\d+\.\d{3};")) ('Missing native phase timing: '+$phase)}
            }finally{foreach($name in $saved.Keys){[Environment]::SetEnvironmentVariable($name,$saved[$name])}}
        }
    }
    Test-Streaming 'Invalid environment entries fail before the native command starts' {
        foreach($streaming in @($false,$true)) {
            $parameters=@{Executable=(Join-Path $PSHOME pwsh.exe);ArgumentList=@('-NoProfile','-File',$environmentProbe);Environment=@{'INVALID=NAME'='private-child-environment-value'};Parse={@{Items=@()}}}
            if($streaming){$parameters.State=@{};$parameters.OnRecord={}}
            $result=Invoke-OutputPumpNative @parameters
            Assert-Streaming ($result.Outcome -eq 'Failed' -and $result.Error.Code -eq 'StartFailed' -and $null -eq $result.ExitCode) 'Invalid environment entry launched a native command.'
            Assert-Streaming ((ConvertTo-OutputPumpJson $result) -notmatch 'private-child-environment-value|INVALID=NAME') 'Invalid environment diagnostics exposed an entry.'
        }
    }
    Test-Streaming 'Legacy native overloads preserve their environment defaults' {
        $defaults=Join-Path $root defaults.ps1
        [IO.File]::WriteAllText($defaults,'[Console]::WriteLine(($env:NO_COLOR -eq "1" -and $env:TERM -eq "dumb" -and $env:GIT_TERMINAL_PROMPT -eq "0"))')
        $arguments=@('-NoProfile','-File',$defaults)
        $capture=[FrenchExDev.OutputPumps.BoundedProcess]::Run((Join-Path $PSHOME pwsh.exe),$arguments,$root,30,4096)
        Assert-Streaming ($capture.ExitCode -eq 0 -and $capture.Stdout.Trim() -eq 'True') 'Legacy buffered overload lost its defaults.'
        $stream=[FrenchExDev.OutputPumps.StreamingProcess]::new((Join-Path $PSHOME pwsh.exe),$arguments,$root,30,1024,[char]10)
        try {
            $record=$stream.Next();$end=$stream.Next()
            Assert-Streaming ($record.Text.Trim() -eq 'True' -and $null -eq $end -and $stream.Result.ExitCode -eq 0) 'Legacy streaming overload lost its defaults.'
        }finally{$stream.Dispose()}
    }
    $configuration=Join-Path $root pumps.yaml
    [IO.File]::WriteAllText($configuration,"version: 2`nroots:`n - path: '$((Split-Path $library).Replace("'","''"))'`npumps: {}`n")
    $setup=Setup-OutputPumps -ConfigurationPath $configuration -InventoryPath (Join-Path $root registry.json) -ProviderId ripgrep
    Assert-Streaming ($setup.Outcome -eq 'Success') 'Provider setup failed.'
    Test-Streaming 'Ripgrep reports an exact count beyond the former four Mi-character capture' {
        $fixture=Join-Path $root matches.txt
        [IO.File]::WriteAllLines($fixture,@(1..12000 | ForEach-Object {'needle '+('x'*300)}))
        Assert-Streaming ([bool](Get-OutputPump -Id ripgrep.search)) 'Missing search coverage.'
        $call=New-OutputPumpScript -SoftwareId ripgrep -Function Find-PumpText -Parameters @{Pattern='needle';Path=@($fixture);WorkingDirectory=$root;MaxItems=1;MaxOutputBytes=1024}
        $result=ConvertFrom-Json -InputObject (& $call.Data.Items[0].ScriptPath)
        Assert-Streaming ($result.Outcome -eq 'Success' -and $result.Data.Complete -and $result.Data.MatchLines -eq 12000 -and $result.Truncation.TotalCount -eq 12000 -and @($result.Data.Items).Count -eq 1 -and $result.Truncation.Applied) 'Search total depended on the preview limit.'
    }
    Test-Streaming 'Generated provider unit and registration tests run without its native CLI' {
        $provider=Join-Path $root FrenchExDev.OPumper.AuditFixture.PoSh
        $generated=New-OPumperModule -Name AuditFixture -Executable deliberately-missing-audit-cli -Path $provider
        Assert-Streaming ($generated.Outcome -eq 'Success') 'Provider generation failed.'
        $runner=Join-Path $root generated-tests.ps1
        [IO.File]::WriteAllText($runner,@'
param($Engine,$Tests)
$ErrorActionPreference='Stop'
[Console]::OutputEncoding=[Text.UTF8Encoding]::new($false)
Import-Module $Engine -Global -DisableNameChecking
Import-Module Pester -RequiredVersion 6.2.0
$configuration=New-PesterConfiguration
$configuration.Run.Path=$Tests
$configuration.Run.PassThru=$true
$configuration.Run.Parallel=$false
$configuration.Output.Verbosity='None'
$report=Invoke-Pester -Configuration $configuration *>&1 | Where-Object {$_.PSObject.Properties['PassedCount'] -and $_.PSObject.Properties['FailedCount']}
[pscustomobject]@{Passed=$report.PassedCount;Failed=$report.FailedCount;DiscoveryErrors=$report.FailedContainersCount;Errors=@($report.FailedContainers | ForEach-Object ErrorRecord | ForEach-Object {$_.Exception.Message} | Select-Object -First 2);Items=@($report.Failed | Select-Object -First 3 | ForEach-Object {@{Test=$_.ExpandedPath;Message=$_.ErrorRecord.Exception.Message}})} | ConvertTo-Json -Depth 6 -Compress
'@)
        $result=Invoke-OutputPumpNative -Executable (Join-Path $PSHOME pwsh.exe) -ArgumentList @('-NoProfile','-File',$runner,(Join-Path $library OutputPump.PoSh.psd1),(Join-Path $provider test)) -Parse {param($capture) @{Report=(ConvertFrom-Json $capture.Stdout)}}
        Assert-Streaming ($result.Outcome -eq 'Success' -and $result.Data.Report.Passed -eq 2 -and $result.Data.Report.Failed -eq 0 -and $result.Data.Report.DiscoveryErrors -eq 0) ('Generated streaming tests failed: '+(ConvertTo-Json $result.Data -Depth 8 -Compress))
    }
    Test-Streaming 'Generated task caller emits one bounded report in a fresh process and preserves arguments' {
        $value='été ''file $(throw "never")'
        $call=New-OutputPumpScript -TaskScriptBlock {
            param($value)
            $local=Invoke-OutputPumpStep first {New-OutputPumpResult -Summary 'Local failure.' -Outcome Failed -ErrorInfo @{Code='FixtureFailure'}}
            @{Value=$value}
        } -ArgumentList @($value) -MaxOutputBytes 1024 -ScriptDirectory $root
        $previous=$env:USERPROFILE
        try {
            $env:USERPROFILE=Join-Path $root home;[void][IO.Directory]::CreateDirectory($env:USERPROFILE)
            $result=Invoke-OutputPumpNative -Executable (Join-Path $PSHOME pwsh.exe) -ArgumentList @('-NoProfile','-File',$call.Data.Items[0].ScriptPath) -Parse {param($c) @{Report=(ConvertFrom-Json $c.Stdout)}}
        }finally{$env:USERPROFILE=$previous}
        Assert-Streaming ($result.Outcome -eq 'Success' -and $result.Data.Report.Outcome -eq 'Failed' -and $result.Data.Report.Data.Status.Failed -eq 1 -and $result.Data.Report.Data.Items[0].Value -eq $value) 'Fresh task caller lost an argument or an assigned intermediate failure.'
        Assert-Streaming (!(Test-Path -LiteralPath $call.Data.Items[0].ScriptPath)) 'Generated task caller did not remove itself.'
    }
}catch{$failures.Add(@{Test='Streaming fixtures';Message=$_.Exception.Message;Line=$_.InvocationInfo.ScriptLineNumber})}
finally {
    $boundary=[IO.Path]::GetFullPath((Join-Path $library '.cache'))+[IO.Path]::DirectorySeparatorChar
    if(![IO.Path]::GetFullPath($root).StartsWith($boundary,[StringComparison]::OrdinalIgnoreCase)){throw 'Unsafe integration cleanup.'}
    Remove-Item -LiteralPath $root -Recurse -Force -ErrorAction SilentlyContinue
}
New-OutputPumpResult -Summary 'Streaming and task integration completed.' -Outcome $(if($failures.Count){'Failed'}else{'Success'}) -Data @{Passed=$passed;Failed=$failures.Count;Items=$failures.ToArray()}
