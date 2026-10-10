function Invoke-PumpDotnetTest {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Target, [string]$WorkingDirectory='.',
          [string]$Configuration, [string]$Framework, [string]$Filter, [string]$MtpFilterOption, [switch]$NoRestore, [switch]$NoBuild,
          [ValidateRange(1,1000)][int]$MaxItems=20, [ValidateRange(1024,8192)][int]$MaxOutputBytes=8192,
          [ValidateRange(1,86400)][int]$TimeoutSeconds=600)
    $reports=Join-Path ([IO.Path]::GetTempPath()) ('output-pump-trx-'+[guid]::NewGuid().ToString('N'))
    try {
        if($Target.StartsWith('-')){throw 'Invalid target'}
        $runner=Get-DotnetTestRunner $WorkingDirectory
        if($runner -eq 'Microsoft.Testing.Platform' -and $Filter -and $MtpFilterOption -notmatch '^--[a-z][a-z-]+$'){
            return New-PumpResult -Summary 'MTP filtering depends on the test framework.' -Outcome Unsupported -RetryAdvice CorrectRequest -ErrorInfo @{Code='MtpFilterOptionRequired';Message='Consult the selected test framework documentation and provide MtpFilterOption explicitly.'} -MaxOutputBytes $MaxOutputBytes
        }
        $arguments=@('test')
        if($runner -eq 'VSTest'){$arguments+=@($Target,'--nologo','--verbosity','quiet','--tl:off','--logger','trx','--results-directory',$reports)}
        else {
            $selector=if([IO.Path]::GetExtension($Target) -in @('.sln','.slnx')){'--solution'}else{'--project'}
            $arguments+=@($selector,$Target,'--report-trx','--results-directory',$reports)
        }
        if($Configuration){$arguments+=@('--configuration',$Configuration)};if($Framework){$arguments+=@('--framework',$Framework)}
        if($Filter){$arguments+=@($(if($runner -eq 'VSTest'){'--filter'}else{$MtpFilterOption}),$Filter)}
        if($NoRestore){$arguments+='--no-restore'};if($NoBuild){$arguments+='--no-build'}
        $capture=Invoke-PumpProcess dotnet $arguments $WorkingDirectory $TimeoutSeconds
        if($capture.StartError -or $capture.TimedOut -or $capture.Truncated){return Complete-PumpProcess $capture -Summary '.NET test execution incomplete.' -MaxItems $MaxItems -MaxOutputBytes $MaxOutputBytes}
        $files=@(Get-ChildItem -LiteralPath $reports -Filter '*.trx' -Recurse -File -ErrorAction SilentlyContinue)
        if($files.Count -eq 0) {
            $unsupported=$runner -eq 'Microsoft.Testing.Platform' -and ($capture.Stdout+$capture.Stderr) -match '(?i)(unrecognized|unknown|unsupported)[^\r\n]*report-trx|report-trx[^\r\n]*(unrecognized|unknown|unsupported)'
            return New-PumpResult -Summary '.NET tests: no structured test report available.' -Outcome $(if($unsupported){'Unsupported'}else{'Failed'}) -ExitCode $capture.ExitCode -Data @{Runner=$runner;Items=(ConvertFrom-DotnetDiagnostics ($capture.Stdout+"`n"+$capture.Stderr))} -ErrorInfo @{Code=$(if($unsupported){'TrxReporterMissing'}else{'TestReportMissing'});Message='No test counts can be established. Configure a TRX reporter or refine the target.'} -RetryAdvice CorrectRequest -MaxItems $MaxItems -MaxOutputBytes $MaxOutputBytes
        }
        $total=0L;$passed=0L;$failed=0L;$skipped=0L;$items=[Collections.Generic.List[object]]::new()
        foreach($file in $files) {
            if($file.Length -gt 4194304){return New-PumpResult -Summary '.NET test report exceeds the parsing budget.' -Outcome Failed -ExitCode $capture.ExitCode -Truncated -Reasons @('ReportLimit') -RetryAdvice NarrowRequest -ErrorInfo @{Code='ReportLimit'} -MaxOutputBytes $MaxOutputBytes}
            $settings=[Xml.XmlReaderSettings]::new();$settings.DtdProcessing=[Xml.DtdProcessing]::Prohibit;$settings.XmlResolver=$null
            $reader=[Xml.XmlReader]::Create($file.FullName,$settings)
            try {
                $doc=[Xml.XmlDocument]::new();$doc.XmlResolver=$null;$doc.Load($reader)
                $counters=$doc.SelectSingleNode('//*[local-name()="Counters"]');if(!$counters){throw 'Missing counters'}
                $reportTotal=[long]$counters.GetAttribute('total');$reportPassed=[long]$counters.GetAttribute('passed');$reportSkipped=[long]$counters.GetAttribute('notExecuted')
                if($reportTotal -lt $reportPassed+$reportSkipped){throw 'Invalid counters'}
                $total+=$reportTotal;$passed+=$reportPassed;$skipped+=$reportSkipped;$failed+=($reportTotal-$reportPassed-$reportSkipped)
                foreach($test in $doc.SelectNodes('//*[local-name()="UnitTestResult"]')) {
                    if($test.GetAttribute('outcome') -notin @('Passed','NotExecuted') -and $items.Count -lt $MaxItems+1) {
                        $message=$test.SelectSingleNode('.//*[local-name()="Message"]')
                        $items.Add(@{Test=$test.GetAttribute('testName');Outcome=$test.GetAttribute('outcome');Message=$(if($message){$message.InnerText}else{$null})})
                    }
                }
            } finally {$reader.Dispose()}
        }
        New-PumpResult -Summary ".NET tests: $passed passed, $failed failed, $skipped skipped; total $total." -Outcome $(if($capture.ExitCode -eq 0 -and $failed -eq 0 -and $total -gt 0){'Success'}else{'Failed'}) -ExitCode $capture.ExitCode -Data @{Runner=$runner;Total=$total;Passed=$passed;Failed=$failed;Skipped=$skipped;Items=$items.ToArray()} -TotalCount $failed -Truncated:($failed -gt $items.Count) -Reasons $(if($failed -gt $items.Count){@('ItemLimit')}else{@()}) -RetryAdvice Unknown -ErrorInfo $(if($capture.ExitCode -ne 0 -or $failed -gt 0 -or $total -eq 0){@{Code='TestsFailedOrEmpty'}}else{$null}) -MaxItems $MaxItems -MaxOutputBytes $MaxOutputBytes
    } catch {
        New-PumpResult -Summary '.NET test configuration or report could not be read.' -Outcome Failed -RetryAdvice CorrectRequest -ErrorInfo @{Code='TestConfigurationOrReportInvalid'} -MaxOutputBytes $MaxOutputBytes
    } finally {
        # This directory is uniquely created for this invocation, never supplied by a caller.
        if(Test-Path -LiteralPath $reports) {
            $resolved=[IO.Path]::GetFullPath($reports);$tempRoot=[IO.Path]::GetFullPath([IO.Path]::GetTempPath())
            if($resolved.StartsWith($tempRoot,[StringComparison]::OrdinalIgnoreCase) -and [IO.Path]::GetFileName($resolved).StartsWith('output-pump-trx-')){Remove-Item -LiteralPath $resolved -Recurse -Force -ErrorAction SilentlyContinue}
        }
    }
}
